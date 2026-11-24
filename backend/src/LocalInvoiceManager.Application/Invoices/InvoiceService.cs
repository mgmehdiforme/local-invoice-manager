using FluentValidation;
using LocalInvoiceManager.Application.Abstractions;
using LocalInvoiceManager.Application.Common;
using LocalInvoiceManager.Application.Documents;
using LocalInvoiceManager.Application.Settings;
using LocalInvoiceManager.Domain;
using LocalInvoiceManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LocalInvoiceManager.Application.Invoices;

public sealed class InvoiceService(
    IAppDbContext db,
    IBusinessClock clock,
    AuditLogger audit,
    NumberingService numbering,
    IValidator<InvoiceInput> validator)
{
    public async Task<PagedResult<InvoiceListItem>> ListAsync(InvoiceQuery q, CancellationToken ct)
    {
        var query = db.Invoices.AsNoTracking().AsQueryable();
        if (q.Status is { } st) query = query.Where(i => i.Status == st);
        if (q.PaymentStatus is { } ps) query = query.Where(i => i.PaymentStatus == ps && i.Status == InvoiceStatus.Issued);
        if (q.CustomerId is { } cid) query = query.Where(i => i.CustomerId == cid);
        if (q.From is { } from) query = query.Where(i => i.IssueDate >= from);
        if (q.To is { } to) query = query.Where(i => i.IssueDate <= to);
        if (q.Overdue == true || q.Unpaid == true)
            query = query.Where(i => i.Status == InvoiceStatus.Issued && i.PaymentStatus != PaymentStatus.Paid);

        var today = clock.Today;
        var rows = await query.OrderByDescending(i => i.IssueDate).ThenByDescending(i => i.Id).ToListAsync(ct);
        return rows
            .Where(i => q.Overdue != true || i.IsOverdue(today))
            .Where(i => Text.Matches(q.Search, i.InvoiceNumber, i.CustomerName))
            .Select(i => ToListItem(i, today))
            .ToPaged(q);
    }

    public async Task<InvoiceDto> GetAsync(int id, CancellationToken ct)
    {
        var inv = await LoadAsync(id, ct, tracking: false);
        return await ToDtoAsync(inv, ct);
    }

    public async Task<TotalsDto> CalculateAsync(CalculateInput input, CancellationToken ct)
    {
        var s = await db.GetAsync(ct);
        return DocumentMath.ToTotals(DocumentMath.Calculate(input.Items, input.DiscountType, input.DiscountValue, input.TaxRate, s.ResolveCurrency(input.CurrencyCode).DecimalPlaces));
    }

    public async Task<InvoiceDto> CreateDraftAsync(InvoiceInput input, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(input, ct);
        var s = await db.GetAsync(ct);
        var inv = new Invoice { Status = InvoiceStatus.Draft, CreatedAtUtc = clock.UtcNow };
        await ApplyAsync(inv, input, s, ct);
        db.Invoices.Add(inv);
        await db.SaveChangesAsync(ct);
        return await GetAsync(inv.Id, ct);
    }

    public async Task<InvoiceDto> UpdateDraftAsync(int id, InvoiceInput input, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(input, ct);
        var inv = await LoadAsync(id, ct);
        inv.EnsureDraft();
        var s = await db.GetAsync(ct);
        db.InvoiceItems.RemoveRange(inv.Items);
        await ApplyAsync(inv, input, s, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task DeleteDraftAsync(int id, CancellationToken ct)
    {
        var inv = await LoadAsync(id, ct);
        if (inv.Status != InvoiceStatus.Draft)
            throw new DomainException("invoice.not_draft", "فاکتور صادرشده حذف نمی‌شود؛ در صورت نیاز آن را ابطال کنید.");
        db.Invoices.Remove(inv);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Issues a draft: refreshes snapshots, recalculates, and assigns the next number — all in one write transaction
    /// (SQLite BEGIN IMMEDIATE), so two issues can never get the same number.
    /// </summary>
    public async Task<InvoiceDto> IssueAsync(int id, CancellationToken ct)
    {
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var inv = await LoadAsync(id, ct);
            inv.EnsureDraft();
            if (inv.Items.Count == 0)
                throw new DomainException("invoice.no_items", "فاکتور باید حداقل یک ردیف داشته باشد.");
            var customer = await db.Customers.FindAsync([inv.CustomerId], ct) ?? throw new NotFoundException("Customer", inv.CustomerId);
            var s = await db.GetAsync(ct);

            // Recalculate with the currency settings that will be frozen on the invoice.
            inv.CopyCurrencyFrom(s);
            var lines = inv.Items.OrderBy(i => i.LineNo).Select(DocumentMath.ToInput).ToList();
            var r = DocumentMath.Calculate(lines, inv.DiscountType, inv.DiscountValue, inv.TaxRate, inv.CurrencyDecimalPlaces);
            db.InvoiceItems.RemoveRange(inv.Items);
            DocumentMath.Apply(inv, inv.Items, lines, r);
            if (inv.Total <= 0)
                throw new DomainException("invoice.zero_total", "مبلغ فاکتور باید بیشتر از صفر باشد.");

            inv.CustomerName = customer.Name;
            inv.CustomerSnapshotJson = Snapshots.ToJson(CustomerSnapshot.From(customer));
            inv.SellerSnapshotJson = Snapshots.ToJson(SellerSnapshot.From(s));
            inv.InvoiceNumber = await numbering.NextInvoiceNumberAsync(inv.IssueDate, ct);
            inv.Status = InvoiceStatus.Issued;
            inv.IssuedAtUtc = clock.UtcNow;
            inv.UpdatedAtUtc = clock.UtcNow;
            inv.RefreshPayments();
            audit.Log("Invoice", inv.Id, "Issued", new { inv.InvoiceNumber, inv.Total });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        return await GetAsync(id, ct);
    }

    public async Task<InvoiceDto> CancelAsync(int id, string? reason, CancellationToken ct)
    {
        var inv = await LoadAsync(id, ct);
        if (inv.Status != InvoiceStatus.Issued)
            throw new DomainException("invoice.not_issued", "فقط فاکتور صادرشده قابل ابطال است.");
        if (inv.Payments.Any(p => p.Status == PaymentRecordStatus.Active))
            throw new DomainException("invoice.has_payments", "این فاکتور پرداخت ثبت‌شده دارد. ابتدا پرداخت‌ها را برگشت بزنید.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("invoice.cancel_reason", "دلیل ابطال را بنویسید.");
        inv.Status = InvoiceStatus.Cancelled;
        inv.CancelledAtUtc = clock.UtcNow;
        inv.CancellationReason = reason.Trim();
        inv.UpdatedAtUtc = clock.UtcNow;
        audit.Log("Invoice", inv.Id, "Cancelled", new { inv.InvoiceNumber, reason });
        await db.SaveChangesAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<InvoiceDto> DuplicateAsync(int id, CancellationToken ct)
    {
        var src = await LoadAsync(id, ct, tracking: false);
        var s = await db.GetAsync(ct);
        var today = clock.Today;
        var input = new InvoiceInput
        {
            CustomerId = src.CustomerId,
            IssueDate = today,
            DueDate = today.AddDays(s.DefaultDueDays),
            DiscountType = src.DiscountType,
            DiscountValue = src.DiscountValue,
            TaxRate = src.TaxRate,
            Notes = src.Notes,
            PaymentTerms = src.PaymentTerms,
            TemplateKey = src.TemplateKey,
            CurrencyCode = src.CurrencyCode,
            Items = src.Items.OrderBy(i => i.LineNo).Select(DocumentMath.ToInput).ToList(),
        };
        var copy = new Invoice { Status = InvoiceStatus.Draft, CreatedAtUtc = clock.UtcNow, DuplicatedFromId = src.Id };
        await ApplyAsync(copy, input, s, ct, allowInactiveCustomer: true);
        db.Invoices.Add(copy);
        await db.SaveChangesAsync(ct);
        return await GetAsync(copy.Id, ct);
    }

    /// <summary>Default values for a new invoice form.</summary>
    public async Task<object> DefaultsAsync(CancellationToken ct)
    {
        var s = await db.GetAsync(ct);
        var today = clock.Today;
        return new
        {
            IssueDate = today,
            DueDate = s.DefaultDueDays > 0 ? today.AddDays(s.DefaultDueDays) : (DateOnly?)null,
            TaxRate = s.DefaultTaxRate,
            s.DefaultPaymentTerms,
            s.DefaultNotes,
            TemplateKey = s.DefaultTemplate,
            s.CurrencyCode,
        };
    }

    internal async Task<Invoice> LoadAsync(int id, CancellationToken ct, bool tracking = true)
    {
        var q = db.Invoices.Include(i => i.Items).Include(i => i.Payments).AsQueryable();
        if (!tracking) q = q.AsNoTracking();
        return await q.FirstOrDefaultAsync(i => i.Id == id, ct) ?? throw new NotFoundException("Invoice", id);
    }

    internal async Task ApplyAsync(Invoice inv, InvoiceInput input, AppSettings s, CancellationToken ct, bool allowInactiveCustomer = false)
    {
        var customer = await db.Customers.FindAsync([input.CustomerId], ct)
            ?? throw new DomainException("invoice.customer", "مشتری انتخاب‌شده پیدا نشد.");
        if (!customer.IsActive && !allowInactiveCustomer && inv.CustomerId != customer.Id)
            throw new DomainException("invoice.customer_archived", "این مشتری بایگانی شده است.");

        var productIds = input.Items.Where(i => i.ProductId.HasValue).Select(i => i.ProductId!.Value).Distinct().ToList();
        if (productIds.Count > 0)
        {
            var existing = await db.Products.Where(p => productIds.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct);
            if (existing.Count != productIds.Count)
                throw new DomainException("invoice.product", "یکی از کالاهای انتخاب‌شده پیدا نشد.");
        }

        inv.CopyCurrencyFrom(s, input.CurrencyCode);
        var r = DocumentMath.Calculate(input.Items, input.DiscountType, input.DiscountValue, input.TaxRate, inv.CurrencyDecimalPlaces);
        inv.CustomerId = customer.Id;
        inv.CustomerName = customer.Name;
        inv.CustomerSnapshotJson = Snapshots.ToJson(CustomerSnapshot.From(customer));
        inv.IssueDate = input.IssueDate;
        inv.DueDate = input.DueDate;
        inv.DiscountType = input.DiscountType;
        inv.DiscountValue = input.DiscountType == DiscountType.None ? 0 : input.DiscountValue;
        inv.TaxRate = input.TaxRate;
        inv.Notes = Text.Clean(input.Notes);
        inv.PaymentTerms = Text.Clean(input.PaymentTerms);
        inv.TemplateKey = input.TemplateKey ?? s.DefaultTemplate;
        inv.UpdatedAtUtc = clock.UtcNow;
        DocumentMath.Apply(inv, inv.Items, input.Items, r);
    }

    internal async Task<InvoiceDto> ToDtoAsync(Invoice i, CancellationToken ct)
    {
        var today = clock.Today;
        // Drafts show the current seller details; issued invoices show what was frozen at issue time.
        var seller = i.Status == InvoiceStatus.Draft || i.SellerSnapshotJson is null
            ? SellerSnapshot.From(await db.GetAsync(ct))
            : Snapshots.FromJson<SellerSnapshot>(i.SellerSnapshotJson)!;
        var customer = Snapshots.FromJson<CustomerSnapshot>(i.CustomerSnapshotJson)
            ?? new CustomerSnapshot(i.CustomerName, null, null, null, null, null, null, null, null, null);
        string? quotationNumber = i.SourceQuotationId is { } qid
            ? await db.Quotations.Where(q => q.Id == qid).Select(q => q.QuotationNumber).FirstOrDefaultAsync(ct)
            : null;
        return new InvoiceDto(
            i.Id, i.InvoiceNumber, i.Status, i.PaymentStatus, i.CustomerId, customer, seller, i.IssueDate, i.DueDate,
            CurrencyDto.From(i), i.DiscountType, i.DiscountValue, i.TaxRate, i.Subtotal, i.DiscountAmount,
            i.TaxableAmount, i.TaxAmount, i.Total, i.PaidAmount, i.Balance, i.IsOverdue(today), i.Notes, i.PaymentTerms,
            i.TemplateKey, i.Items.OrderBy(x => x.LineNo).Select(DocumentMath.ToDto).ToList(),
            i.Payments.OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.Id)
                .Select(p => ToPaymentDto(p, i)).ToList(),
            i.SourceQuotationId, quotationNumber, i.RecurringInvoiceId, i.DuplicatedFromId, i.IssuedAtUtc, i.CancelledAtUtc,
            i.CancellationReason, i.CreatedAtUtc, i.UpdatedAtUtc);
    }

    public static PaymentDto ToPaymentDto(Payment p, Invoice? i) => new(
        p.Id, p.InvoiceId, i?.InvoiceNumber, i?.CustomerName, p.Amount, p.PaymentDate, p.Method, p.Reference, p.Notes,
        p.Status, p.ReversedAtUtc, p.ReversalReason, p.CreatedAtUtc, i?.CurrencyCode ?? "");

    public static InvoiceListItem ToListItem(Invoice i, DateOnly today) => new(
        i.Id, i.InvoiceNumber, i.Status, i.PaymentStatus, i.CustomerId, i.CustomerName, i.IssueDate, i.DueDate,
        i.Total, i.PaidAmount, i.Balance, i.IsOverdue(today), CurrencyDto.From(i), i.UpdatedAtUtc);
}
