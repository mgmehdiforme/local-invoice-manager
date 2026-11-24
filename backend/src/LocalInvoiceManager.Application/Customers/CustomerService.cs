using LocalInvoiceManager.Application.Settings;
using FluentValidation;
using LocalInvoiceManager.Application.Abstractions;
using LocalInvoiceManager.Application.Common;
using LocalInvoiceManager.Domain;
using LocalInvoiceManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LocalInvoiceManager.Application.Customers;

public sealed record CustomerInput
{
    public string Name { get; init; } = "";
    public string? CompanyName { get; init; }
    public string? Phone { get; init; }
    public string? Mobile { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }
    public string? PostalCode { get; init; }
    public string? TaxId { get; init; }
    public string? NationalId { get; init; }
    public string? Notes { get; init; }
}

public sealed record CustomerDto(
    int Id, string Name, string? CompanyName, string? Phone, string? Mobile, string? Email, string? Address,
    string? City, string? PostalCode, string? TaxId, string? NationalId, string? Notes, bool IsActive,
    DateTime CreatedAtUtc, decimal Balance, decimal OverdueAmount, int InvoiceCount);

/// <summary>Top-level figures are in the default currency; <see cref="ByCurrency"/> lists every currency the customer was invoiced in.</summary>
public sealed record CustomerBalance(decimal TotalInvoiced, decimal TotalPaid, decimal Balance, decimal OverdueAmount, int InvoiceCount, int OverdueCount,
    IReadOnlyList<CurrencyBalance> ByCurrency);

public sealed record CurrencyBalance(string CurrencyCode, decimal TotalInvoiced, decimal TotalPaid, decimal Balance, decimal OverdueAmount);

public sealed record CustomerQuery : PageQuery
{
    public bool? IncludeInactive { get; init; }
}

public sealed class CustomerValidator : AbstractValidator<CustomerInput>
{
    public CustomerValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("نام مشتری را وارد کنید.").MaximumLength(200);
        RuleFor(x => x.CompanyName).MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithMessage("ایمیل معتبر نیست.");
        RuleFor(x => x.Phone).MaximumLength(30).Matches(@"^[0-9۰-۹+\-\s()]*$").WithMessage("شماره تلفن فقط شامل عدد باشد.");
        RuleFor(x => x.Mobile).MaximumLength(30).Matches(@"^[0-9۰-۹+\-\s()]*$").WithMessage("شماره موبایل فقط شامل عدد باشد.");
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.PostalCode).MaximumLength(20);
        RuleFor(x => x.TaxId).MaximumLength(30);
        RuleFor(x => x.NationalId).MaximumLength(30);
    }
}

public sealed class CustomerService(IAppDbContext db, IBusinessClock clock, IValidator<CustomerInput> validator)
{
    public async Task<PagedResult<CustomerDto>> ListAsync(CustomerQuery q, CancellationToken ct)
    {
        var customers = await db.Customers.AsNoTracking()
            .Where(c => q.IncludeInactive == true || c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
        var balances = await BalancesAsync(null, ct);
        return customers
            .Where(c => Text.Matches(q.Search, c.Name, c.CompanyName, c.Phone, c.Mobile, c.Email, c.TaxId, c.NationalId))
            .Select(c => ToDto(c, balances.GetValueOrDefault(c.Id)))
            .ToPaged(q);
    }

    public async Task<CustomerDto> GetAsync(int id, CancellationToken ct)
    {
        var c = await db.Customers.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Customer", id);
        var balances = await BalancesAsync(id, ct);
        return ToDto(c, balances.GetValueOrDefault(id));
    }

    public async Task<CustomerDto> CreateAsync(CustomerInput input, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(input, ct);
        var c = new Customer { CreatedAtUtc = clock.UtcNow };
        Apply(c, input);
        db.Customers.Add(c);
        await db.SaveChangesAsync(ct);
        return await GetAsync(c.Id, ct);
    }

    public async Task<CustomerDto> UpdateAsync(int id, CustomerInput input, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(input, ct);
        var c = await db.Customers.FindAsync([id], ct) ?? throw new NotFoundException("Customer", id);
        Apply(c, input);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    /// <summary>Customers are archived, never deleted: issued invoices keep their snapshot and history.</summary>
    public async Task SetActiveAsync(int id, bool active, CancellationToken ct)
    {
        var c = await db.Customers.FindAsync([id], ct) ?? throw new NotFoundException("Customer", id);
        c.IsActive = active;
        c.UpdatedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<CustomerBalance> GetBalanceAsync(int id, CancellationToken ct)
    {
        _ = await db.Customers.FindAsync([id], ct) ?? throw new NotFoundException("Customer", id);
        return (await BalancesAsync(id, ct)).GetValueOrDefault(id) ?? new CustomerBalance(0, 0, 0, 0, 0, 0, []);
    }

    /// <summary>
    /// Balance per customer from issued invoices (decimal math in .NET, not SQLite). Main figures are in the default
    /// currency; other currencies are listed separately and never added to them.
    /// </summary>
    private async Task<Dictionary<int, CustomerBalance>> BalancesAsync(int? customerId, CancellationToken ct)
    {
        var today = clock.Today;
        var defaultCode = (await db.GetAsync(ct)).CurrencyCode;
        var rows = await db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Issued && (customerId == null || i.CustomerId == customerId))
            .Select(i => new { i.CustomerId, i.CurrencyCode, i.Total, i.PaidAmount, i.DueDate })
            .ToListAsync(ct);
        return rows.GroupBy(r => r.CustomerId).ToDictionary(g => g.Key, g =>
        {
            var byCurrency = g.GroupBy(r => r.CurrencyCode).Select(cg => new CurrencyBalance(
                    cg.Key, cg.Sum(r => r.Total), cg.Sum(r => r.PaidAmount), cg.Sum(r => Math.Max(0, r.Total - r.PaidAmount)),
                    cg.Where(r => r.Total - r.PaidAmount > 0 && r.DueDate is { } d && d < today).Sum(r => r.Total - r.PaidAmount)))
                .OrderBy(c => c.CurrencyCode == defaultCode ? 0 : 1).ThenBy(c => c.CurrencyCode).ToList();
            var main = g.Where(r => r.CurrencyCode == defaultCode).ToList();
            var overdue = main.Where(r => r.Total - r.PaidAmount > 0 && r.DueDate is { } d && d < today).ToList();
            return new CustomerBalance(
                main.Sum(r => r.Total), main.Sum(r => r.PaidAmount), main.Sum(r => Math.Max(0, r.Total - r.PaidAmount)),
                overdue.Sum(r => r.Total - r.PaidAmount), g.Count(), g.Count(r => r.Total - r.PaidAmount > 0 && r.DueDate is { } d && d < today),
                byCurrency);
        });
    }

    private void Apply(Customer c, CustomerInput i)
    {
        c.Name = i.Name.Trim();
        c.CompanyName = Text.Clean(i.CompanyName);
        c.Phone = Text.Clean(i.Phone);
        c.Mobile = Text.Clean(i.Mobile);
        c.Email = Text.Clean(i.Email);
        c.Address = Text.Clean(i.Address);
        c.City = Text.Clean(i.City);
        c.PostalCode = Text.Clean(i.PostalCode);
        c.TaxId = Text.Clean(i.TaxId);
        c.NationalId = Text.Clean(i.NationalId);
        c.Notes = Text.Clean(i.Notes);
        c.UpdatedAtUtc = clock.UtcNow;
    }

    private static CustomerDto ToDto(Customer c, CustomerBalance? b) => new(
        c.Id, c.Name, c.CompanyName, c.Phone, c.Mobile, c.Email, c.Address, c.City, c.PostalCode, c.TaxId,
        c.NationalId, c.Notes, c.IsActive, c.CreatedAtUtc, b?.Balance ?? 0, b?.OverdueAmount ?? 0, b?.InvoiceCount ?? 0);
}
