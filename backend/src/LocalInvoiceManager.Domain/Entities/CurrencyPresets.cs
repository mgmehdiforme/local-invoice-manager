namespace LocalInvoiceManager.Domain.Entities;

/// <summary>Known currencies, used when a currency is chosen by code only (first run). Same list as the UI.</summary>
public static class CurrencyPresets
{
    public static readonly IReadOnlyList<CurrencyDef> All =
    [
        new("USD", "$", 2, SymbolPosition.Before),
        new("EUR", "€", 2, SymbolPosition.Before),
        new("GBP", "£", 2, SymbolPosition.Before),
        new("CAD", "CA$", 2, SymbolPosition.Before),
        new("AUD", "A$", 2, SymbolPosition.Before),
        new("CHF", "CHF", 2, SymbolPosition.Before),
        new("JPY", "¥", 0, SymbolPosition.Before),
        new("CNY", "CN¥", 2, SymbolPosition.Before),
        new("INR", "₹", 2, SymbolPosition.Before),
        new("TRY", "₺", 2, SymbolPosition.Before),
        new("AED", "AED", 2, SymbolPosition.Before),
        new("IRR", "ریال", 0, SymbolPosition.After),
        new("IRT", "تومان", 0, SymbolPosition.After),
    ];

    public static CurrencyDef Find(string code)
    {
        var c = code.Trim().ToUpperInvariant();
        return All.FirstOrDefault(x => x.Code == c) ?? new CurrencyDef(c, c, 2, SymbolPosition.Before);
    }
}
