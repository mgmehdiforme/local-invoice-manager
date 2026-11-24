using LocalInvoiceManager.Domain.Calculation;

namespace LocalInvoiceManager.Domain.Entities;

/// <summary>Fields shared by invoices and quotations (not an EF entity itself; each table gets its own columns).</summary>
public abstract class SalesDocument
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public DateOnly IssueDate { get; set; }

    public string CustomerName { get; set; } = "";
    public string? CustomerSnapshotJson { get; set; }
    public string? SellerSnapshotJson { get; set; }

    // Frozen copy of the document's currency (set by CopyCurrencyFrom). Empty code = not set yet → default currency.
    public string CurrencyCode { get; set; } = "";
    public string CurrencySymbol { get; set; } = "";
    public int CurrencyDecimalPlaces { get; set; }
    public string ThousandsSeparator { get; set; } = ",";
    public string DecimalSeparator { get; set; } = ".";
    public SymbolPosition CurrencySymbolPosition { get; set; } = SymbolPosition.Before;

    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal TaxRate { get; set; }

    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxableAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }

    public string? Notes { get; set; }
    public string? PaymentTerms { get; set; }
    public string TemplateKey { get; set; } = "modern";

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Freezes the currency on the document. <paramref name="code"/> null = keep the document's current code.</summary>
    public void CopyCurrencyFrom(AppSettings s, string? code = null)
    {
        var c = s.ResolveCurrency(code ?? CurrencyCode);
        CurrencyCode = c.Code;
        CurrencySymbol = c.Symbol;
        CurrencyDecimalPlaces = c.DecimalPlaces;
        ThousandsSeparator = s.ThousandsSeparator;
        DecimalSeparator = s.DecimalSeparator;
        CurrencySymbolPosition = c.SymbolPosition;
    }
}

public abstract class SalesDocumentLine
{
    public int Id { get; set; }
    public int LineNo { get; set; }
    public int? ProductId { get; set; }
    public Product? Product { get; set; }
    public string Description { get; set; } = "";
    public string? Sku { get; set; }
    public string Unit { get; set; } = "عدد";
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public DiscountType DiscountType { get; set; }
    public decimal DiscountValue { get; set; }
    public decimal DiscountAmount { get; set; }
    public bool IsTaxable { get; set; } = true;
    public decimal LineTotal { get; set; }

    public CalcLine ToCalcLine() => new(Quantity, UnitPrice, DiscountType, DiscountValue, IsTaxable);
}

public class Invoice : SalesDocument
{
    public string? InvoiceNumber { get; set; }
    public InvoiceStatus Status { get; set; } = InvoiceStatus.Draft;
    public DateOnly? DueDate { get; set; }
    public decimal PaidAmount { get; set; }
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;

    public int? SourceQuotationId { get; set; }
    public int? RecurringInvoiceId { get; set; }
    public int? DuplicatedFromId { get; set; }

    public DateTime? IssuedAtUtc { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancellationReason { get; set; }

    public List<InvoiceItem> Items { get; set; } = [];
    public List<Payment> Payments { get; set; } = [];

    public decimal Balance => Status == InvoiceStatus.Issued ? Math.Max(0, Total - PaidAmount) : 0;

    public bool IsOverdue(DateOnly today) =>
        Status == InvoiceStatus.Issued && Total - PaidAmount > 0 && DueDate is { } due && due < today;

    public void EnsureDraft()
    {
        if (Status != InvoiceStatus.Draft)
            throw new DomainException("invoice.not_draft", "فقط فاکتور پیش‌نویس قابل ویرایش است.");
    }

    /// <summary>Recomputes PaidAmount and PaymentStatus from active payments.</summary>
    public void RefreshPayments()
    {
        PaidAmount = Payments.Where(p => p.Status == PaymentRecordStatus.Active).Sum(p => p.Amount);
        PaymentStatus = InvoiceCalculator.PaymentStatusFor(Total, PaidAmount);
    }
}

public class InvoiceItem : SalesDocumentLine
{
    public int InvoiceId { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public decimal Amount { get; set; }
    public DateOnly PaymentDate { get; set; }
    public PaymentMethod Method { get; set; }
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public PaymentRecordStatus Status { get; set; } = PaymentRecordStatus.Active;
    public DateTime? ReversedAtUtc { get; set; }
    public string? ReversalReason { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class Quotation : SalesDocument
{
    public string QuotationNumber { get; set; } = "";
    public QuotationStatus Status { get; set; } = QuotationStatus.Draft;
    public DateOnly? ValidUntil { get; set; }
    public int? ConvertedInvoiceId { get; set; }
    public DateTime? AcceptedAtUtc { get; set; }
    public List<QuotationItem> Items { get; set; } = [];

    public bool IsEditable => Status is QuotationStatus.Draft or QuotationStatus.Sent;
}

public class QuotationItem : SalesDocumentLine
{
    public int QuotationId { get; set; }
}
