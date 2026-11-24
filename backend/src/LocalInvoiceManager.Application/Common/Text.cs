using System.Globalization;
using System.Text;

namespace LocalInvoiceManager.Application.Common;

public static class Text
{
    /// <summary>Trim and turn empty strings into null.</summary>
    public static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Normalizes Persian/Arabic letters and digits so searches match however the user typed them.</summary>
    public static string NormalizeForSearch(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value.Trim())
        {
            sb.Append(ch switch
            {
                'ي' or 'ى' => 'ی',
                'ك' => 'ک',
                'ة' => 'ه',
                >= '۰' and <= '۹' => (char)('0' + (ch - '۰')),
                >= '٠' and <= '٩' => (char)('0' + (ch - '٠')),
                '‌' => ' ',
                _ => char.ToLower(ch, CultureInfo.InvariantCulture),
            });
        }
        return sb.ToString();
    }

    public static bool Matches(string? search, params string?[] fields)
    {
        var s = NormalizeForSearch(search);
        if (s.Length == 0) return true;
        return fields.Any(f => NormalizeForSearch(f).Contains(s, StringComparison.Ordinal));
    }
}
