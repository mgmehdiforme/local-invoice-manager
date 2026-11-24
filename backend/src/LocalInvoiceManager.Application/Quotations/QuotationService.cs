using FluentValidation;
using LocalInvoiceManager.Application.Abstractions;
using LocalInvoiceManager.Application.Common;
using LocalInvoiceManager.Application.Documents;
using LocalInvoiceManager.Application.Invoices;
using LocalInvoiceManager.Application.Settings;
using LocalInvoiceManager.Domain;
using LocalInvoiceManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LocalInvoiceManager.Application.Quotations;

public sealed record QuotationInput
{
    /// <summary>One of the enabled currencies; null = the default currency (or, when editing, the document's current one).</summary>
    public string? CurrencyCode { get; init; }
    public int CustomerId { get; init; }
    public DateOnly IssueDate { get; init; }
    public DateOnly? ValidUntil { get; init; }
    public DiscountType DiscountType { get; init; }
    public decimal DiscountValue { get; init; }
    public decimal TaxRate { get; init; }
    public string? Notes { get; init; }
    public string? PaymentTerms { get; init; }
    public string? TemplateKey { get; init; }
    public List<LineInput> Items { get; init; } = [];
}

public sealed record QuotationListItem(
    int Id, string QuotationNumber, QuotationStatus Status, bool IsExpired, int CustomerId, string CustomerName,
    DateOnly IssueDate, DateOnly? ValidUntil, decimal Total, CurrencyDto Currency, int? ConvertedInvoiceId);

public sealed record QuotationDto(
    int Id, string QuotationNumber, QuotationStatus Status, bool IsExpired, int CustomerId, CustomerSnapshot Customer,
    SellerSnapshot Seller, DateOnly IssueDate, DateOnly? ValidUntil, CurrencyDto Currency, DiscountType DiscountType,
    decimal DiscountValue, decimal TaxRate, decimal Subtotal, decimal DiscountAmount, decimal TaxableAmount, decimal TaxAmount,
    decimal Total, string? Notes, string? PaymentTerms, string TemplateKey, IReadOnlyList<LineDto> Items,
    int? ConvertedInvoiceId, string? ConvertedInvoiceNumber, DateTime? AcceptedAtUtc, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

public sealed record QuotationQuery : PageQuery
{
    public QuotationStatus? Status { get; init; }
    public int? CustomerId { get; init; }
}

public sealed class QuotationInputValidator : AbstractValidator<QuotationInput>
{
    public QuotationInputValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0).WithMessage("مشتری را انتخاب کنید.");
        RuleFor(x => x.IssueDate).NotEmpty().WithMessage("تاریخ را وارد کنید.");
        RuleFor(x => x.ValidUntil).GreaterThanOrEqualTo(x => x.IssueDate).When(x => x.ValidUntil.HasValue)
            .WithMessage("تاریخ اعتبار نمی‌تواند قبل از تاریخ صدور باشد.");
        RuleFor(x => x.TaxRate).InclusiveBetween(0, 100).WithMessage("نرخ مالیات باید بین ۰ تا ۱۰۰ باشد.");
        RuleFor(x => x.DiscountValue).GreaterThanOrEqualTo(0).WithMessage("تخفیف نمی‌تواند منفی باشد.");
        RuleFor(x => x.DiscountValue).LessThanOrEqualTo(100).When(x => x.DiscountType == DiscountType.Percent).WithMessage("درصد تخفیف حداکثر ۱۰۰ است.");
        RuleFor(x => x.Items).Must(i => i.Count is >= 1 and <= 200).WithMessage("پیش‌فاکتور باید بین ۱ تا ۲۰۰ ردیف داشته باشد.");
        RuleForEach(x => x.Items).SetValidator(new LineInputValidator());
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.PaymentTerms).MaximumLength(2000);
        RuleFor(x => x.TemplateKey).Must(t => t is null || Templates.Keys.Contains(t)).WithMessage("قالب نامعتبر است.");
    }
}

public sealed class QuotationService(
    IAppDbContext db, IBusinessClock clock, AuditLogger audit, NumberingService numbering,
    InvoiceService invoices, IValidator<QuotationInput> validator)
{
    public async Task<PagedResult<QuotationListItem>> ListAsync(QuotationQuery q, CancellationToken ct)
    {
        var query = db.Quotations.AsNoTracking().AsQueryable();
        if (q.Status is { } st) query = query.Where(x => x.Status == st);
        if (q.CustomerId is { } cid) query = query.Where(x => x.CustomerId == cid);
        var today = clock.Today;
        var rows = await query.OrderByDescending(x => x.IssueDate).ThenByDescending(x => x.Id).ToListAsync(ct);
        return rows.Where(x => Text.Matches(q.Search, x.QuotationNumber, x.CustomerName))
            .Select(x => new QuotationListItem(x.Id, x.QuotationNumber, x.Status, IsExpired(x, today), x.CustomerId, x.CustomerName,
                x.IssueDate, x.ValidUntil, x.Total, CurrencyDto.From(x), x.ConvertedInvoiceId))
            .ToPaged(q);
    }

    public async Task<QuotationDto> GetAsync(int id, CancellationToken ct)
    {
        var x = await LoadAsync(id, ct, tracking: false);
        var today = clock.Today;
        var seller = Snapshots.FromJson<SellerSnapshot>(x.SellerSnapshotJson) ?? SellerSnapshot.From(await db.GetAsync(ct));
        var customer = Snapshots.FromJson<CustomerSnapshot>(x.CustomerSnapshotJson)!;
        var invNo = x.ConvertedInvoiceId is { } iid ? await db.Invoices.Where(i => i.Id == iid).Select(i => i.InvoiceNumber).FirstOrDefaultAsync(ct) : null;
        return new QuotationDto(x.Id, x.QuotationNumber, x.Status, IsExpired(x, today), x.CustomerId, customer, seller, x.IssueDate,
            x.ValidUntil, CurrencyDto.From(x), x.DiscountType, x.DiscountValue, x.TaxRate, x.Subtotal, x.DiscountAmount,
            x.TaxableAmount, x.TaxAmount, x.Total, x.Notes, x.PaymentTerms, x.TemplateKey,
            x.Items.OrderBy(l => l.LineNo).Select(DocumentMath.ToDto).ToList(), x.ConvertedInvoiceId, x.ConvertedInvoiceId is null ? null : invNo ?? "پیش‌نویس",
            x.AcceptedAtUtc, x.CreatedAtUtc, x.UpdatedAtUtc);
    }

    public async Task<QuotationDto> CreateAsync(QuotationInput input, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(input, ct);
        int id;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var q = new Quotation { Status = QuotationStatus.Draft, CreatedAtUtc = clock.UtcNow };
            await ApplyAsync(q, input, ct);
            q.QuotationNumber = await numbering.NextQuotationNumberAsync(input.IssueDate, ct);
            db.Quotations.Add(q);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            id = q.Id;
        }
        return await GetAsync(id, ct);
    }

    public async Task<QuotationDto> UpdateAsync(int id, QuotationInput input, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(input, ct);
        var q = await LoadAsync(id, ct);
        if (!q.IsEditable)
            throw new DomainException("quotation.locked", "پیش‌فاکتور تأییدشده، ردشده یا تبدیل‌شده قابل ویرایش نیست.");
        db.QuotationItems.RemoveRange(q.Items);
        await ApplyAsync(q, input, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        var q = await LoadAsync(id, ct);
        if (q.Status != QuotationStatus.Draft)
            throw new DomainException("quotation.not_draft", "فقط پیش‌فاکتور پیش‌نویس حذف می‌شود.");
        db.Quotations.Remove(q);
        await db.SaveChangesAsync(ct);
    }

    public Task<QuotationDto> SendAsync(int id, CancellationToken ct) =>
        TransitionAsync(id, QuotationStatus.Sent, [QuotationStatus.Draft], "Sent", ct);

    public Task<QuotationDto> AcceptAsync(int id, CancellationToken ct) =>
        TransitionAsync(id, QuotationStatus.Accepted, [QuotationStatus.Draft, QuotationStatus.Sent], "Accepted", ct);

    public Task<QuotationDto> RejectAsync(int id, CancellationToken ct) =>
        TransitionAsync(id, QuotationStatus.Rejected, [QuotationStatus.Draft, QuotationStatus.Sent], "Rejected", ct);

    /// <summary>
    /// Creates a draft invoice with the same lines and prices. Runs once: the quotation becomes Converted and the
    /// unique index on Invoices.SourceQuotationId blocks a second conversion even under a race.
    /// </summary>
    public async Task<InvoiceDto> ConvertAsync(int id, CancellationToken ct)
    {
        int invoiceId;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var q = await LoadAsync(id, ct);
            if (q.Status == QuotationStatus.Converted || q.ConvertedInvoiceId is not null)
                throw new DomainException("quotation.already_converted", "این پیش‌فاکتور قبلاً به فاکتور تبدیل شده است.");
            if (q.Status != QuotationStatus.Accepted)
                throw new DomainException("quotation.not_accepted", "ابتدا پیش‌فاکتور را «تأیید شده» علامت بزنید.");

            var s = await db.GetAsync(ct);
            var today = clock.Today;
            var input = new InvoiceInput
            {
                CustomerId = q.CustomerId,
                IssueDate = today,
                DueDate = s.DefaultDueDays > 0 ? today.AddDays(s.DefaultDueDays) : null,
                DiscountType = q.DiscountType,
                DiscountValue = q.DiscountValue,
                TaxRate = q.TaxRate,
                Notes = q.Notes,
                PaymentTerms = q.PaymentTerms ?? s.DefaultPaymentTerms,
                TemplateKey = q.TemplateKey,
                CurrencyCode = q.CurrencyCode,
                Items = q.Items.OrderBy(l => l.LineNo).Select(DocumentMath.ToInput).ToList(),
            };
            var inv = new Invoice { Status = InvoiceStatus.Draft, CreatedAtUtc = clock.UtcNow, SourceQuotationId = q.Id };
            await invoices.ApplyAsync(inv, input, s, ct, allowInactiveCustomer: true);
            db.Invoices.Add(inv);
            await db.SaveChangesAsync(ct);

            q.Status = QuotationStatus.Converted;
            q.ConvertedInvoiceId = inv.Id;
            q.UpdatedAtUtc = clock.UtcNow;
            audit.Log("Quotation", q.Id, "Converted", new { q.QuotationNumber, InvoiceId = inv.Id });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            invoiceId = inv.Id;
        }
        return await invoices.GetAsync(invoiceId, ct);
    }

    public async Task<QuotationDto> DuplicateAsync(int id, CancellationToken ct)
    {
        var src = await LoadAsync(id, ct, tracking: false);
        var today = clock.Today;
        var validDays = src.ValidUntil is { } v ? v.DayNumber - src.IssueDate.DayNumber : 14;
        return await CreateAsync(new QuotationInput
        {
            CustomerId = src.CustomerId, IssueDate = today, ValidUntil = today.AddDays(Math.Max(0, validDays)),
            DiscountType = src.DiscountType, DiscountValue = src.DiscountValue, TaxRate = src.TaxRate, Notes = src.Notes,
            PaymentTerms = src.PaymentTerms, TemplateKey = src.TemplateKey, CurrencyCode = src.CurrencyCode,
            Items = src.Items.OrderBy(l => l.LineNo).Select(DocumentMath.ToInput).ToList(),
        }, ct);
    }

    private async Task<QuotationDto> TransitionAsync(int id, QuotationStatus to, QuotationStatus[] from, string action, CancellationToken ct)
    {
        var q = await LoadAsync(id, ct);
        if (!from.Contains(q.Status))
            throw new DomainException("quotation.invalid_status", "این تغییر وضعیت برای این پیش‌فاکتور ممکن نیست.");
        q.Status = to;
        if (to == QuotationStatus.Accepted) q.AcceptedAtUtc = clock.UtcNow;
        q.UpdatedAtUtc = clock.UtcNow;
        audit.Log("Quotation", q.Id, action, new { q.QuotationNumber });
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    private async Task<Quotation> LoadAsync(int id, CancellationToken ct, bool tracking = true)
    {
        var q = db.Quotations.Include(x => x.Items).AsQueryable();
        if (!tracking) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new NotFoundException("Quotation", id);
    }

    private async Task ApplyAsync(Quotation q, QuotationInput input, CancellationToken ct)
    {
        var customer = await db.Customers.FindAsync([input.CustomerId], ct)
            ?? throw new DomainException("quotation.customer", "مشتری انتخاب‌شده پیدا نشد.");
        var s = await db.GetAsync(ct);
        q.CopyCurrencyFrom(s, input.CurrencyCode);
        var r = DocumentMath.Calculate(input.Items, input.DiscountType, input.DiscountValue, input.TaxRate, q.CurrencyDecimalPlaces);
        q.CustomerId = customer.Id;
        q.CustomerName = customer.Name;
        q.CustomerSnapshotJson = Snapshots.ToJson(CustomerSnapshot.From(customer));
        q.SellerSnapshotJson = Snapshots.ToJson(SellerSnapshot.From(s));
        q.IssueDate = input.IssueDate;
        q.ValidUntil = input.ValidUntil;
        q.DiscountType = input.DiscountType;
        q.DiscountValue = input.DiscountType == DiscountType.None ? 0 : input.DiscountValue;
        q.TaxRate = input.TaxRate;
        q.Notes = Text.Clean(input.Notes);
        q.PaymentTerms = Text.Clean(input.PaymentTerms);
        q.TemplateKey = input.TemplateKey ?? s.DefaultTemplate;
        q.UpdatedAtUtc = clock.UtcNow;
        DocumentMath.Apply(q, q.Items, input.Items, r);
    }

    private static bool IsExpired(Quotation q, DateOnly today) =>
        q.IsEditable && q.ValidUntil is { } v && v < today;
}
