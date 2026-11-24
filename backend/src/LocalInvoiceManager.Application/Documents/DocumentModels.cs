using System.Text.Json;
using FluentValidation;
using LocalInvoiceManager.Application.Common;
using LocalInvoiceManager.Domain;
using LocalInvoiceManager.Domain.Calculation;
using LocalInvoiceManager.Domain.Entities;

namespace LocalInvoiceManager.Application.Documents;

/// <summary>A line as entered by the user (invoice or quotation).</summary>
public sealed record LineInput
{
    public int? ProductId { get; init; }
    public string Description { get; init; } = "";
    public string? Sku { get; init; }
    public string Unit { get; init; } = "عدد";
    public decimal Quantity { get; init; } = 1;
    public decimal UnitPrice { get; init; }
    public DiscountType DiscountType { get; init; }
    public decimal DiscountValue { get; init; }
    public bool IsTaxable { get; init; } = true;
}

public sealed record LineDto(
    int Id, int LineNo, int? ProductId, string Description, string? Sku, string Unit, decimal Quantity, decimal UnitPrice,
    DiscountType DiscountType, decimal DiscountValue, decimal DiscountAmount, bool IsTaxable, decimal LineTotal);

/// <summary>Totals the server calculated (live preview).</summary>
public sealed record TotalsDto(
    IReadOnlyList<decimal> LineTotals, IReadOnlyList<decimal> LineDiscounts,
    decimal Subtotal, decimal DiscountAmount, decimal TaxableAmount, decimal TaxAmount, decimal Total);

public sealed record CurrencyDto(string CurrencyCode, string CurrencySymbol, int CurrencyDecimalPlaces, string ThousandsSeparator, string DecimalSeparator, SymbolPosition CurrencySymbolPosition)
{
    public static CurrencyDto From(SalesDocument d) => new(d.CurrencyCode, d.CurrencySymbol, d.CurrencyDecimalPlaces, d.ThousandsSeparator, d.DecimalSeparator, d.CurrencySymbolPosition);
}

public sealed record CustomerSnapshot(
    string Name, string? CompanyName, string? Phone, string? Mobile, string? Email, string? Address, string? City,
    string? PostalCode, string? TaxId, string? NationalId)
{
    public static CustomerSnapshot From(Customer c) =>
        new(c.Name, c.CompanyName, c.Phone, c.Mobile, c.Email, c.Address, c.City, c.PostalCode, c.TaxId, c.NationalId);
}

public sealed record SellerSnapshot(
    string CompanyName, string? LegalName, string? EconomicCode, string? NationalId, string? RegistrationNumber,
    string? Phone, string? Email, string? Website, string? Address, string? PostalCode, int? LogoFileId,
    string? BankName, string? BankAccountNumber, string? BankCardNumber, string? BankIban, string? BankAccountHolder,
    string TaxLabel, string AccentColor)
{
    public static SellerSnapshot From(AppSettings s) => new(
        s.CompanyName, s.LegalName, s.EconomicCode, s.NationalId, s.RegistrationNumber, s.Phone, s.Email, s.Website,
        s.Address, s.PostalCode, s.LogoFileId, s.BankName, s.BankAccountNumber, s.BankCardNumber, s.BankIban,
        s.BankAccountHolder, s.TaxLabel, s.AccentColor);
}

public static class Snapshots
{
    public static string ToJson<T>(T value) => JsonSerializer.Serialize(value, JsonDefaults.Options);
    public static T? FromJson<T>(string? json) => string.IsNullOrEmpty(json) ? default : JsonSerializer.Deserialize<T>(json, JsonDefaults.Options);
}

public sealed class LineInputValidator : AbstractValidator<LineInput>
{
    public LineInputValidator()
    {
        RuleFor(x => x.Description).NotEmpty().WithMessage("شرح ردیف را وارد کنید.").MaximumLength(1000);
        RuleFor(x => x.Unit).NotEmpty().WithMessage("واحد را وارد کنید.").MaximumLength(30);
        RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("تعداد باید بیشتر از صفر باشد.")
            .Must(q => decimal.Round(q, 3) == q).WithMessage("تعداد حداکثر سه رقم اعشار دارد.")
            .LessThan(1_000_000_000m);
        RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("قیمت نمی‌تواند منفی باشد.").LessThan(1_000_000_000_000_000m);
        RuleFor(x => x.DiscountValue).GreaterThanOrEqualTo(0).WithMessage("تخفیف نمی‌تواند منفی باشد.");
        RuleFor(x => x.DiscountValue).LessThanOrEqualTo(100).When(x => x.DiscountType == DiscountType.Percent).WithMessage("درصد تخفیف حداکثر ۱۰۰ است.");
    }
}

/// <summary>Shared calculation + line mapping for invoices and quotations.</summary>
public static class DocumentMath
{
    public static CalcResult Calculate(IReadOnlyList<LineInput> lines, DiscountType discountType, decimal discountValue, decimal taxRate, int decimals) =>
        InvoiceCalculator.Calculate(
            lines.Select(l => new CalcLine(l.Quantity, l.UnitPrice, l.DiscountType, l.DiscountValue, l.IsTaxable)).ToList(),
            discountType, discountValue, taxRate, decimals);

    public static TotalsDto ToTotals(CalcResult r) => new(
        r.Lines.Select(l => l.LineTotal).ToList(), r.Lines.Select(l => l.DiscountAmount).ToList(),
        r.Subtotal, r.DiscountAmount, r.TaxableAmount, r.TaxAmount, r.Total);

    /// <summary>Copies lines and totals onto a document. Line items always store their own description/unit/price (snapshot).</summary>
    public static void Apply<TLine>(SalesDocument doc, List<TLine> target, IReadOnlyList<LineInput> lines, CalcResult r) where TLine : SalesDocumentLine, new()
    {
        target.Clear();
        for (var i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            target.Add(new TLine
            {
                LineNo = i + 1,
                ProductId = l.ProductId,
                Description = l.Description.Trim(),
                Sku = Text.Clean(l.Sku),
                Unit = l.Unit.Trim(),
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
                DiscountType = l.DiscountType,
                DiscountValue = l.DiscountType == DiscountType.None ? 0 : l.DiscountValue,
                DiscountAmount = r.Lines[i].DiscountAmount,
                IsTaxable = l.IsTaxable,
                LineTotal = r.Lines[i].LineTotal,
            });
        }
        doc.Subtotal = r.Subtotal;
        doc.DiscountAmount = r.DiscountAmount;
        doc.TaxableAmount = r.TaxableAmount;
        doc.TaxAmount = r.TaxAmount;
        doc.Total = r.Total;
    }

    public static LineInput ToInput(SalesDocumentLine l) => new()
    {
        ProductId = l.ProductId, Description = l.Description, Sku = l.Sku, Unit = l.Unit, Quantity = l.Quantity,
        UnitPrice = l.UnitPrice, DiscountType = l.DiscountType, DiscountValue = l.DiscountValue, IsTaxable = l.IsTaxable,
    };

    public static LineDto ToDto(SalesDocumentLine l) => new(
        l.Id, l.LineNo, l.ProductId, l.Description, l.Sku, l.Unit, l.Quantity, l.UnitPrice, l.DiscountType,
        l.DiscountValue, l.DiscountAmount, l.IsTaxable, l.LineTotal);
}
