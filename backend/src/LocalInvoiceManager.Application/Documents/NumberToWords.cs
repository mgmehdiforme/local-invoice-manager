using LocalInvoiceManager.Domain.Localization;

namespace LocalInvoiceManager.Application.Documents;

/// <summary>
/// Writes amounts in words for the PDF: Persian ("یک میلیون و دویست و پنجاه هزار") or English
/// ("One million two hundred fifty thousand").
/// </summary>
public static class NumberToWords
{
    /// <summary>
    /// The "amount in words" line for a document total, in the current language. Persian is shown only for whole-number
    /// currencies; English adds cents as a fraction, as on a cheque ("… and 50/100 USD").
    /// </summary>
    public static string? ForAmount(decimal total, CurrencyDto c)
    {
        if (Loc.IsPersian)
            return c.CurrencyDecimalPlaces == 0 ? $"{Convert(total)} {c.CurrencySymbol}" : null;
        var whole = Math.Truncate(Math.Abs(total));
        var words = English(total);
        if (c.CurrencyDecimalPlaces == 0) return $"{words} {c.CurrencyCode}";
        var scale = (decimal)Math.Pow(10, c.CurrencyDecimalPlaces);
        var fraction = (long)Math.Round((Math.Abs(total) - whole) * scale);
        return $"{words} and {fraction.ToString().PadLeft(c.CurrencyDecimalPlaces, '0')}/{(long)scale} {c.CurrencyCode}";
    }

    private static readonly string[] EnOnes =
        ["", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen",
         "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"];
    private static readonly string[] EnTens = ["", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"];
    private static readonly string[] EnScales = ["", "thousand", "million", "billion", "trillion", "quadrillion"];

    /// <summary>Whole part in English words, first letter capitalized: 1234 → "One thousand two hundred thirty-four".</summary>
    public static string English(decimal amount)
    {
        var n = (long)Math.Floor(Math.Abs(amount));
        if (n == 0) return "Zero";
        var parts = new List<string>();
        var scale = 0;
        while (n > 0 && scale < EnScales.Length)
        {
            var chunk = (int)(n % 1000);
            if (chunk > 0) parts.Insert(0, EnScales[scale].Length > 0 ? $"{EnThree(chunk)} {EnScales[scale]}" : EnThree(chunk));
            n /= 1000;
            scale++;
        }
        var text = (amount < 0 ? "minus " : "") + string.Join(" ", parts);
        return char.ToUpperInvariant(text[0]) + text[1..];
    }

    private static string EnThree(int n)
    {
        var parts = new List<string>();
        if (n >= 100) { parts.Add($"{EnOnes[n / 100]} hundred"); n %= 100; }
        if (n >= 20) parts.Add(n % 10 > 0 ? $"{EnTens[n / 10]}-{EnOnes[n % 10]}" : EnTens[n / 10]);
        else if (n > 0) parts.Add(EnOnes[n]);
        return string.Join(" ", parts);
    }

    private static readonly string[] Ones = ["", "یک", "دو", "سه", "چهار", "پنج", "شش", "هفت", "هشت", "نه"];
    private static readonly string[] Teens = ["ده", "یازده", "دوازده", "سیزده", "چهارده", "پانزده", "شانزده", "هفده", "هجده", "نوزده"];
    private static readonly string[] Tens = ["", "", "بیست", "سی", "چهل", "پنجاه", "شصت", "هفتاد", "هشتاد", "نود"];
    private static readonly string[] Hundreds = ["", "یکصد", "دویست", "سیصد", "چهارصد", "پانصد", "ششصد", "هفتصد", "هشتصد", "نهصد"];
    private static readonly string[] Scales = ["", "هزار", "میلیون", "میلیارد", "هزار میلیارد", "میلیون میلیارد"];

    public static string Convert(decimal amount)
    {
        var n = (long)Math.Floor(Math.Abs(amount));
        if (n == 0) return "صفر";
        var parts = new List<string>();
        var scale = 0;
        while (n > 0 && scale < Scales.Length)
        {
            var chunk = (int)(n % 1000);
            if (chunk > 0)
            {
                var words = Three(chunk);
                parts.Insert(0, Scales[scale].Length > 0 ? $"{words} {Scales[scale]}" : words);
            }
            n /= 1000;
            scale++;
        }
        return (amount < 0 ? "منفی " : "") + string.Join(" و ", parts);
    }

    private static string Three(int n)
    {
        var parts = new List<string>();
        if (n >= 100) { parts.Add(Hundreds[n / 100]); n %= 100; }
        if (n >= 20) { parts.Add(Tens[n / 10]); n %= 10; if (n > 0) parts.Add(Ones[n]); }
        else if (n >= 10) parts.Add(Teens[n - 10]);
        else if (n > 0) parts.Add(Ones[n]);
        return string.Join(" و ", parts);
    }
}
