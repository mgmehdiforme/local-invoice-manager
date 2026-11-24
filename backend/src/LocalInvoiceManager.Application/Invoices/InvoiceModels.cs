using FluentValidation;
using LocalInvoiceManager.Application.Common;
using LocalInvoiceManager.Application.Documents;
using LocalInvoiceManager.Domain;

namespace LocalInvoiceManager.Application.Invoices;

public sealed record InvoiceInput
{
    /// <summary>One of the enabled currencies; null = the default currency (or, when editing, the document's current one).</summary>
    public string? CurrencyCode { get; init; }
    public int CustomerId { get; init; }
    public DateOnly IssueDate { get; init; }
    public DateOnly? DueDate { get; init; }
    public DiscountType DiscountType { get; init; }
    public decimal DiscountValue { get; init; }
    public decimal TaxRate { get; init; }
    public string? Notes { get; init; }
    public string? PaymentTerms { get; init; }
    public string? TemplateKey { get; init; }
    public List<LineInput> Items { get; init; } = [];
}

/// <summary>Payload for live totals; the customer is not needed.</summary>
public sealed record CalculateInput
{
    /// <summary>One of the enabled currencies; null = the default currency (or, when editing, the document's current one).</summary>
    public string? CurrencyCode { get; init; }
    public DiscountType DiscountType { get; init; }
    public decimal DiscountValue { get; init; }
    public decimal TaxRate { get; init; }
    public List<LineInput> Items { get; init; } = [];
}

public sealed record PaymentDto(
    int Id, int InvoiceId, string? InvoiceNumber, string? CustomerName, decimal Amount, DateOnly PaymentDate,
    PaymentMethod Method, string? Reference, string? Notes, PaymentRecordStatus Status, DateTime? ReversedAtUtc,
    string? ReversalReason, DateTime CreatedAtUtc, string CurrencyCode);

public sealed record InvoiceListItem(
    int Id, string? InvoiceNumber, InvoiceStatus Status, PaymentStatus PaymentStatus, int CustomerId, string CustomerName,
    DateOnly IssueDate, DateOnly? DueDate, decimal Total, decimal PaidAmount, decimal Balance, bool IsOverdue,
    CurrencyDto Currency, DateTime UpdatedAtUtc);

public sealed record InvoiceDto(
    int Id, string? InvoiceNumber, InvoiceStatus Status, PaymentStatus PaymentStatus, int CustomerId,
    CustomerSnapshot Customer, SellerSnapshot Seller, DateOnly IssueDate, DateOnly? DueDate, CurrencyDto Currency,
    DiscountType DiscountType, decimal DiscountValue, decimal TaxRate, decimal Subtotal, decimal DiscountAmount,
    decimal TaxableAmount, decimal TaxAmount, decimal Total, decimal PaidAmount, decimal Balance, bool IsOverdue,
    string? Notes, string? PaymentTerms, string TemplateKey, IReadOnlyList<LineDto> Items, IReadOnlyList<PaymentDto> Payments,
    int? SourceQuotationId, string? SourceQuotationNumber, int? RecurringInvoiceId, int? DuplicatedFromId,
    DateTime? IssuedAtUtc, DateTime? CancelledAtUtc, string? CancellationReason, DateTime CreatedAtUtc, DateTime UpdatedAtUtc);

public sealed record InvoiceQuery : PageQuery
{
    public InvoiceStatus? Status { get; init; }
    public PaymentStatus? PaymentStatus { get; init; }
    public bool? Overdue { get; init; }
    /// <summary>Issued and not fully paid (includes overdue).</summary>
    public bool? Unpaid { get; init; }
    public int? CustomerId { get; init; }
    public DateOnly? From { get; init; }
    public DateOnly? To { get; init; }
}

public sealed class InvoiceInputValidator : AbstractValidator<InvoiceInput>
{
    public InvoiceInputValidator()
    {
        RuleFor(x => x.CustomerId).GreaterThan(0).WithMessage("مشتری را انتخاب کنید.");
        RuleFor(x => x.IssueDate).NotEmpty().WithMessage("تاریخ صدور را وارد کنید.");
        RuleFor(x => x.DueDate).GreaterThanOrEqualTo(x => x.IssueDate).When(x => x.DueDate.HasValue)
            .WithMessage("سررسید نمی‌تواند قبل از تاریخ صدور باشد.");
        RuleFor(x => x.TaxRate).InclusiveBetween(0, 100).WithMessage("نرخ مالیات باید بین ۰ تا ۱۰۰ باشد.");
        RuleFor(x => x.DiscountValue).GreaterThanOrEqualTo(0).WithMessage("تخفیف نمی‌تواند منفی باشد.");
        RuleFor(x => x.DiscountValue).LessThanOrEqualTo(100).When(x => x.DiscountType == DiscountType.Percent).WithMessage("درصد تخفیف حداکثر ۱۰۰ است.");
        RuleFor(x => x.Items).Must(i => i.Count <= 200).WithMessage("حداکثر ۲۰۰ ردیف مجاز است.");
        RuleForEach(x => x.Items).SetValidator(new LineInputValidator());
        RuleFor(x => x.Notes).MaximumLength(2000);
        RuleFor(x => x.PaymentTerms).MaximumLength(2000);
        RuleFor(x => x.TemplateKey).Must(t => t is null || Settings.Templates.Keys.Contains(t)).WithMessage("قالب نامعتبر است.");
    }
}
