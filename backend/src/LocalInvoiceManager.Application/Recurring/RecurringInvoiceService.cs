using LocalInvoiceManager.Domain.Localization;
using FluentValidation;
using LocalInvoiceManager.Application.Abstractions;
using LocalInvoiceManager.Application.Common;
using LocalInvoiceManager.Application.Documents;
using LocalInvoiceManager.Application.Invoices;
using LocalInvoiceManager.Application.Notifications;
using LocalInvoiceManager.Application.Settings;
using LocalInvoiceManager.Domain;
using LocalInvoiceManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LocalInvoiceManager.Application.Recurring;

public sealed record RecurringTemplate
{
    /// <summary>One of the enabled currencies; null = the default currency (or, when editing, the document's current one).</summary>
    public string? CurrencyCode { get; init; }
    public DiscountType DiscountType { get; init; }
    public decimal DiscountValue { get; init; }
    public decimal TaxRate { get; init; }
    public string? Notes { get; init; }
    public string? PaymentTerms { get; init; }
    public string? TemplateKey { get; init; }
    public List<LineInput> Items { get; init; } = [];
}

public sealed record RecurringInput
{
    public string Name { get; init; } = "";
    public int CustomerId { get; init; }
    public RecurrenceFrequency Frequency { get; init; } = RecurrenceFrequency.Monthly;
    public DateOnly StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public bool AutoIssue { get; init; }
    public bool IsActive { get; init; } = true;
    public int DueDays { get; init; } = 30;
    public RecurringTemplate Template { get; init; } = new();
}

public sealed record RecurringDto(int Id, string Name, int CustomerId, string CustomerName, RecurrenceFrequency Frequency, DateOnly StartDate,
    DateOnly? EndDate, DateOnly NextRunDate, bool AutoIssue, bool IsActive, int DueDays, RecurringTemplate Template, decimal Total,
    DateTime? LastRunAtUtc, int? LastInvoiceId, int GeneratedCount);

public sealed class RecurringValidator : AbstractValidator<RecurringInput>
{
    public RecurringValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("یک نام برای این قرارداد بنویسید.").MaximumLength(200);
        RuleFor(x => x.CustomerId).GreaterThan(0).WithMessage("مشتری را انتخاب کنید.");
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.EndDate).GreaterThanOrEqualTo(x => x.StartDate).When(x => x.EndDate.HasValue).WithMessage("تاریخ پایان قبل از شروع است.");
        RuleFor(x => x.DueDays).InclusiveBetween(0, 365);
        RuleFor(x => x.Template.Items).Must(i => i.Count is >= 1 and <= 200).WithMessage("حداقل یک ردیف لازم است.");
        RuleForEach(x => x.Template.Items).SetValidator(new LineInputValidator());
        RuleFor(x => x.Template.TaxRate).InclusiveBetween(0, 100);
    }
}

/// <summary>Invoices that repeat on a schedule in the app calendar (e.g. the 1st of every month). The daily job generates due ones.</summary>
public sealed class RecurringInvoiceService(
    IAppDbContext db, IBusinessClock clock, AuditLogger audit, InvoiceService invoices, NotificationService notifications,
    IValidator<RecurringInput> validator, ILogger<RecurringInvoiceService> logger)
{
    public const int MaxCatchUp = 12;

    public async Task<IReadOnlyList<RecurringDto>> ListAsync(CancellationToken ct)
    {
        var rows = await db.RecurringInvoices.AsNoTracking().Include(r => r.Customer).OrderBy(r => r.NextRunDate).ToListAsync(ct);
        var counts = await db.Invoices.Where(i => i.RecurringInvoiceId != null).GroupBy(i => i.RecurringInvoiceId!.Value)
            .Select(g => new { g.Key, C = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.C, ct);
        var s = await db.GetAsync(ct);
        return rows.Select(r => ToDto(r, counts.GetValueOrDefault(r.Id), s)).ToList();
    }

    public async Task<RecurringDto> GetAsync(int id, CancellationToken ct) =>
        (await ListAsync(ct)).FirstOrDefault(r => r.Id == id) ?? throw new NotFoundException("RecurringInvoice", id);

    public async Task<RecurringDto> CreateAsync(RecurringInput input, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(input, ct);
        var r = new RecurringInvoice { CreatedAtUtc = clock.UtcNow, NextRunDate = input.StartDate };
        await ApplyAsync(r, input, ct);
        db.RecurringInvoices.Add(r);
        await db.SaveChangesAsync(ct);
        return await GetAsync(r.Id, ct);
    }

    public async Task<RecurringDto> UpdateAsync(int id, RecurringInput input, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(input, ct);
        var r = await db.RecurringInvoices.FindAsync([id], ct) ?? throw new NotFoundException("RecurringInvoice", id);
        if (r.LastRunAtUtc is null || input.StartDate > r.NextRunDate) r.NextRunDate = input.StartDate;
        await ApplyAsync(r, input, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var r = await db.RecurringInvoices.FindAsync([id], ct) ?? throw new NotFoundException("RecurringInvoice", id);
        db.RecurringInvoices.Remove(r); // generated invoices keep existing (FK set null)
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Generates every due run (catching up at most 12 missed runs per schedule). Returns created invoice ids.</summary>
    public async Task<IReadOnlyList<int>> RunDueAsync(CancellationToken ct)
    {
        var today = clock.Today;
        var due = await db.RecurringInvoices.Where(r => r.IsActive && r.NextRunDate <= today).Select(r => r.Id).ToListAsync(ct);
        var created = new List<int>();
        foreach (var id in due)
        {
            for (var i = 0; i < MaxCatchUp; i++)
            {
                var r = await db.RecurringInvoices.FindAsync([id], ct);
                if (r is null || !r.IsActive || r.NextRunDate > today) break;
                try { created.Add(await GenerateAsync(r, ct)); }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Recurring invoice {Id} failed", id);
                    notifications.AddInApp(NotificationType.RecurringCreated, Loc.T("ساخت فاکتور تکرارشونده ناموفق بود"), $"{r.Name}: {ex.Message}");
                    await db.SaveChangesAsync(ct);
                    break;
                }
            }
        }
        return created;
    }

    public async Task<int> RunNowAsync(int id, CancellationToken ct)
    {
        var r = await db.RecurringInvoices.FindAsync([id], ct) ?? throw new NotFoundException("RecurringInvoice", id);
        return await GenerateAsync(r, ct);
    }

    private async Task<int> GenerateAsync(RecurringInvoice r, CancellationToken ct)
    {
        var t = Snapshots.FromJson<RecurringTemplate>(r.TemplateJson) ?? new RecurringTemplate();
        var s = await db.GetAsync(ct);
        var issueDate = r.NextRunDate;
        var inv = new Invoice { Status = InvoiceStatus.Draft, CreatedAtUtc = clock.UtcNow, RecurringInvoiceId = r.Id };
        await invoices.ApplyAsync(inv, new InvoiceInput
        {
            CustomerId = r.CustomerId, IssueDate = issueDate, DueDate = r.DueDays > 0 ? issueDate.AddDays(r.DueDays) : null,
            DiscountType = t.DiscountType, DiscountValue = t.DiscountValue, TaxRate = t.TaxRate, Notes = t.Notes,
            PaymentTerms = t.PaymentTerms, TemplateKey = t.TemplateKey, Items = t.Items, CurrencyCode = t.CurrencyCode,
        }, s, ct, allowInactiveCustomer: true);
        db.Invoices.Add(inv);
        r.LastRunAtUtc = clock.UtcNow;
        r.NextRunDate = r.Advance(r.NextRunDate, s.CalendarSystem);
        if (r.EndDate is { } end && r.NextRunDate > end) r.IsActive = false;
        await db.SaveChangesAsync(ct);
        r.LastInvoiceId = inv.Id;
        audit.Log("RecurringInvoice", r.Id, "Generated", new { InvoiceId = inv.Id, issueDate });

        string label;
        if (r.AutoIssue)
        {
            await db.SaveChangesAsync(ct);
            var issued = await invoices.IssueAsync(inv.Id, ct);
            label = Loc.T("فاکتور {0} صادر شد", issued.InvoiceNumber);
        }
        else label = Loc.T("پیش‌نویس فاکتور ساخته شد؛ بررسی و صادر کنید");
        notifications.AddInApp(NotificationType.RecurringCreated, Loc.T("فاکتور تکرارشونده: {0}", r.Name), $"{label} ({Fmt.Date(issueDate)}).", inv.Id);
        await db.SaveChangesAsync(ct);
        return inv.Id;
    }

    private async Task ApplyAsync(RecurringInvoice r, RecurringInput input, CancellationToken ct)
    {
        var customer = await db.Customers.FindAsync([input.CustomerId], ct) ?? throw new DomainException("recurring.customer", "مشتری پیدا نشد.");
        r.Name = input.Name.Trim();
        r.CustomerId = customer.Id;
        r.Frequency = input.Frequency;
        r.StartDate = input.StartDate;
        r.EndDate = input.EndDate;
        r.AutoIssue = input.AutoIssue;
        r.IsActive = input.IsActive;
        r.DueDays = input.DueDays;
        r.TemplateJson = Snapshots.ToJson(input.Template);
        r.UpdatedAtUtc = clock.UtcNow;
    }

    private static RecurringDto ToDto(RecurringInvoice r, int count, AppSettings s)
    {
        var t = Snapshots.FromJson<RecurringTemplate>(r.TemplateJson) ?? new RecurringTemplate();
        decimal total = 0;
        try { total = DocumentMath.Calculate(t.Items, t.DiscountType, t.DiscountValue, t.TaxRate, s.ResolveCurrency(t.CurrencyCode).DecimalPlaces).Total; }
        catch (DomainException) { /* invalid template shows 0 */ }
        return new RecurringDto(r.Id, r.Name, r.CustomerId, r.Customer?.Name ?? "", r.Frequency, r.StartDate, r.EndDate, r.NextRunDate,
            r.AutoIssue, r.IsActive, r.DueDays, t, total, r.LastRunAtUtc, r.LastInvoiceId, count);
    }
}
