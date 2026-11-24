using System.Globalization;
using LocalInvoiceManager.Domain.Calendar;

namespace LocalInvoiceManager.Domain.Localization;

/// <summary>
/// Server-side text in English or Persian. Source strings are Persian; <see cref="En"/> maps them to English.
/// The language comes from the request (Accept-Language, set by middleware) and falls back to the app setting
/// (<see cref="DefaultLanguage"/>), which background jobs use. The calendar always follows the app setting.
/// </summary>
public static class Loc
{
    private static readonly AsyncLocal<string?> RequestLanguage = new();

    /// <summary>App setting; updated at start-up and whenever settings change.</summary>
    public static string DefaultLanguage { get; set; } = "en";
    public static CalendarKind DefaultCalendar { get; set; } = CalendarKind.Gregorian;

    public static string Language => RequestLanguage.Value ?? DefaultLanguage;
    public static bool IsPersian => Language == "fa";
    public static CalendarSystem Calendar => CalendarSystem.For(DefaultCalendar);

    /// <summary>Sets the language for the current request / async flow ("en" or "fa"; anything else is ignored).</summary>
    public static void UseLanguage(string? language) => RequestLanguage.Value = Normalize(language);

    public static string? Normalize(string? language)
    {
        if (string.IsNullOrWhiteSpace(language)) return null;
        var l = language.Trim().ToLowerInvariant();
        return l.StartsWith("fa") ? "fa" : l.StartsWith("en") ? "en" : null;
    }

    /// <summary>Translates a Persian source string. <c>{0}</c>-style placeholders are filled from <paramref name="args"/>.</summary>
    public static string T(string fa, params object?[] args)
    {
        var s = IsPersian ? fa : En.Map.GetValueOrDefault(fa, fa);
        return args.Length == 0 ? s : string.Format(CultureInfo.InvariantCulture, s, args);
    }

    /// <summary>Translates a message that may already be final text (used for error details); unknown text is returned unchanged.</summary>
    public static string Translate(string message) => IsPersian ? message : En.Map.GetValueOrDefault(message, message);
}
