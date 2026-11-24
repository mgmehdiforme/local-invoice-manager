using System.Globalization;
using LocalInvoiceManager.Domain;
using LocalInvoiceManager.Domain.Calendar;
using LocalInvoiceManager.Domain.Localization;

namespace LocalInvoiceManager.Application.Documents;

public interface IPdfRenderer
{
    byte[] Render(DocumentPdfModel model);
}

public enum PdfKind { Invoice, Quotation }

public sealed record PdfLine(string No, string Description, string? Sku, string Quantity, string Unit, string UnitPrice, string Discount, string Total, bool IsTaxable);

public sealed record PdfTotalRow(string Label, string Value, bool Emphasis = false);

/// <summary>Everything the PDF needs, already formatted (digits, dates and currency in the app language and calendar).</summary>
public sealed record DocumentPdfModel
{
    public PdfKind Kind { get; init; }
    /// <summary>Right-to-left layout (Persian).</summary>
    public bool Rtl { get; init; }
    public string Title { get; init; } = "";
    public string Number { get; init; } = "";
    public string Template { get; init; } = "modern";
    public string AccentColor { get; init; } = "#0e6b5c";
    public string? Watermark { get; init; }
    public byte[]? Logo { get; init; }
    public IReadOnlyList<(string Label, string Value)> Meta { get; init; } = [];
    public SellerSnapshot Seller { get; init; } = null!;
    public CustomerSnapshot Customer { get; init; } = null!;
    public IReadOnlyList<PdfLine> Lines { get; init; } = [];
    public IReadOnlyList<PdfTotalRow> Totals { get; init; } = [];
    public string? AmountInWords { get; init; }
    public string? Notes { get; init; }
    public string? PaymentTerms { get; init; }
    public bool ShowBank { get; init; } = true;
    public bool HasTaxExemptLines { get; init; }
}

public static class Fmt
{
    /// <summary>Localizes digits: Persian digits in Persian, unchanged in English.</summary>
    public static string Fa(string? s) => s is null ? "" : Loc.IsPersian ? Jalali.ToPersianDigits(s) : s;

    public static string Date(DateOnly d) => Fa(Loc.Calendar.Format(d));

    public static string Amount(decimal value, CurrencyDto c)
    {
        var digits = Math.Clamp(c.CurrencyDecimalPlaces, 0, 4);
        var nfi = new NumberFormatInfo
        {
            NumberGroupSeparator = string.IsNullOrEmpty(c.ThousandsSeparator) ? "" : c.ThousandsSeparator,
            NumberDecimalSeparator = c.DecimalSeparator,
            NegativeSign = "-",
        };
        return Fa(value.ToString("N" + digits, nfi));
    }

    public static string Money(decimal value, CurrencyDto c) =>
        c.CurrencySymbolPosition != SymbolPosition.Before ? $"{Amount(value, c)} {c.CurrencySymbol}"
        : c.CurrencySymbol.Length == 1 ? $"{c.CurrencySymbol}{Amount(value, c)}" : $"{c.CurrencySymbol} {Amount(value, c)}";

    public static string Quantity(decimal q) =>
        Fa(q.ToString("0.###", CultureInfo.InvariantCulture).Replace(".", Loc.IsPersian ? "٫" : "."));

    public static string Percent(decimal p) =>
        Loc.IsPersian ? Fa(p.ToString("0.##", CultureInfo.InvariantCulture).Replace(".", "٫")) + "٪" : p.ToString("0.##", CultureInfo.InvariantCulture) + "%";
}
