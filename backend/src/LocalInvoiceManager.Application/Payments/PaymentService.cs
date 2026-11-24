using FluentValidation;
using LocalInvoiceManager.Application.Abstractions;
using LocalInvoiceManager.Application.Common;
using LocalInvoiceManager.Application.Invoices;
using LocalInvoiceManager.Domain;
using LocalInvoiceManager.Domain.Calculation;
using LocalInvoiceManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LocalInvoiceManager.Application.Payments;

public sealed record PaymentInput
{
    public decimal Amount { get; init; }
    public DateOnly PaymentDate { get; init; }
    public PaymentMethod Method { get; init; } = PaymentMethod.BankTransfer;
    public string? Reference { get; init; }
    public string? Notes { get; init; }
}

public sealed record PaymentQuery : PageQuery
{
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
    public int? CustomerId { get; init; }
    public PaymentMethod? Method { get; init; }
    public bool? IncludeReversed { get; init; }
}

public sealed class PaymentInputValidator : AbstractValidator<PaymentInput>
{
    public PaymentInputValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("مبلغ پرداخت باید بیشتر از صفر باشد.");
        RuleFor(x => x.PaymentDate).NotEmpty().WithMessage("تاریخ پرداخت را وارد کنید.");
        RuleFor(x => x.Reference).MaximumLength(100);
        RuleFor(x => x.Notes).MaximumLength(1000);
    }
}

public sealed class PaymentService(IAppDbContext db, IBusinessClock clock, AuditLogger audit, InvoiceService invoices, IValidator<PaymentInput> validator)
{
    /// <summary>Records a (partial) payment. Runs in one write transaction so two payments can never overpay an invoice.</summary>
    public async Task<InvoiceDto> AddAsync(int invoiceId, PaymentInput input, CancellationToken ct)
    {
        await validator.ValidateAndThrowAsync(input, ct);
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var inv = await invoices.LoadAsync(invoiceId, ct);
            if (inv.Status != InvoiceStatus.Issued)
                throw new DomainException("payment.invoice_not_issued", "پرداخت فقط برای فاکتور صادرشده ثبت می‌شود.");
            if (Money.Round(input.Amount, inv.CurrencyDecimalPlaces) != input.Amount)
                throw new DomainException("payment.precision", "تعداد رقم اعشار مبلغ بیشتر از تنظیم ارز است.");
            var remaining = inv.Total - inv.PaidAmount;
            if (remaining <= 0)
                throw new DomainException("payment.already_paid", "این فاکتور کامل پرداخت شده است.");
            if (input.Amount > remaining)
                throw new DomainException("payment.exceeds_balance", "مبلغ پرداخت از مانده فاکتور بیشتر است.");
            if (input.PaymentDate < inv.IssueDate)
                throw new DomainException("payment.before_issue", "تاریخ پرداخت نمی‌تواند قبل از تاریخ صدور فاکتور باشد.");

            var payment = new Payment
            {
                InvoiceId = inv.Id,
                Amount = input.Amount,
                PaymentDate = input.PaymentDate,
                Method = input.Method,
                Reference = Text.Clean(input.Reference),
                Notes = Text.Clean(input.Notes),
                CreatedAtUtc = clock.UtcNow,
            };
            inv.Payments.Add(payment);
            inv.RefreshPayments();
            inv.UpdatedAtUtc = clock.UtcNow;
            await db.SaveChangesAsync(ct);
            audit.Log("Payment", payment.Id, "Added", new { inv.InvoiceNumber, payment.Amount, payment.Method });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        return await invoices.GetAsync(invoiceId, ct);
    }

    /// <summary>Payments are never deleted; a reversal keeps the record and its reason.</summary>
    public async Task<InvoiceDto> ReverseAsync(int paymentId, string? reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("payment.reverse_reason", "دلیل برگشت پرداخت را بنویسید.");
        int invoiceId;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            var payment = await db.Payments.FirstOrDefaultAsync(p => p.Id == paymentId, ct) ?? throw new NotFoundException("Payment", paymentId);
            if (payment.Status == PaymentRecordStatus.Reversed)
                throw new DomainException("payment.already_reversed", "این پرداخت قبلاً برگشت خورده است.");
            var inv = await invoices.LoadAsync(payment.InvoiceId, ct);
            var tracked = inv.Payments.First(p => p.Id == paymentId);
            tracked.Status = PaymentRecordStatus.Reversed;
            tracked.ReversedAtUtc = clock.UtcNow;
            tracked.ReversalReason = reason.Trim();
            inv.RefreshPayments();
            inv.UpdatedAtUtc = clock.UtcNow;
            audit.Log("Payment", paymentId, "Reversed", new { inv.InvoiceNumber, tracked.Amount, reason });
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            invoiceId = inv.Id;
        }
        return await invoices.GetAsync(invoiceId, ct);
    }

    public async Task<PagedResult<PaymentDto>> ListAsync(PaymentQuery q, CancellationToken ct)
    {
        var query = db.Payments.AsNoTracking().Include(p => p.Invoice).AsQueryable();
        if (q.IncludeReversed != true) query = query.Where(p => p.Status == PaymentRecordStatus.Active);
        if (q.From is { } from) query = query.Where(p => p.PaymentDate >= from);
        if (q.To is { } to) query = query.Where(p => p.PaymentDate <= to);
        if (q.CustomerId is { } cid) query = query.Where(p => p.Invoice!.CustomerId == cid);
        if (q.Method is { } m) query = query.Where(p => p.Method == m);
        var rows = await query.OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.Id).ToListAsync(ct);
        return rows
            .Where(p => Text.Matches(q.Search, p.Invoice?.InvoiceNumber, p.Invoice?.CustomerName, p.Reference))
            .Select(p => InvoiceService.ToPaymentDto(p, p.Invoice))
            .ToPaged(q);
    }
}
