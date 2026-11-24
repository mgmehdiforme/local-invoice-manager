using System.Globalization;

namespace LocalInvoiceManager.Domain.Calendar;

/// <summary>
/// One API over the Gregorian and Jalali (Persian) calendars. Dates are stored as Gregorian <see cref="DateOnly"/>;
/// this class turns them into year/month/day in the calendar chosen in settings.
/// </summary>
public sealed class CalendarSystem
{
    public static readonly CalendarSystem Gregorian = new(CalendarKind.Gregorian, new GregorianCalendar());
    public static readonly CalendarSystem Jalali = new(CalendarKind.Jalali, new PersianCalendar());

    public static CalendarSystem For(CalendarKind kind) => kind == CalendarKind.Jalali ? Jalali : Gregorian;

    private static readonly string[] JalaliFa = Calendar.Jalali.MonthNames;
    private static readonly string[] JalaliEn =
        ["Farvardin", "Ordibehesht", "Khordad", "Tir", "Mordad", "Shahrivar", "Mehr", "Aban", "Azar", "Dey", "Bahman", "Esfand"];
    private static readonly string[] GregorianEn =
        ["January", "February", "March", "April", "May", "June", "July", "August", "September", "October", "November", "December"];
    private static readonly string[] GregorianFa =
        ["ژانویه", "فوریه", "مارس", "آوریل", "مه", "ژوئن", "ژوئیه", "اوت", "سپتامبر", "اکتبر", "نوامبر", "دسامبر"];

    private readonly System.Globalization.Calendar _cal;

    private CalendarSystem(CalendarKind kind, System.Globalization.Calendar cal)
    {
        Kind = kind;
        _cal = cal;
    }

    public CalendarKind Kind { get; }

    public (int Year, int Month, int Day) Parts(DateOnly date)
    {
        var dt = date.ToDateTime(TimeOnly.MinValue);
        return (_cal.GetYear(dt), _cal.GetMonth(dt), _cal.GetDayOfMonth(dt));
    }

    public int Year(DateOnly date) => Parts(date).Year;

    public DateOnly FromParts(int year, int month, int day) =>
        DateOnly.FromDateTime(_cal.ToDateTime(year, month, day, 0, 0, 0, 0));

    public int DaysInMonth(int year, int month) => _cal.GetDaysInMonth(year, month);

    /// <summary>Adds months in this calendar, clamping the day to the target month's length (31 Jan + 1 month = 28/29 Feb).</summary>
    public DateOnly AddMonths(DateOnly date, int months)
    {
        var (y, m, d) = Parts(date);
        var total = y * 12 + (m - 1) + months;
        var ny = total / 12;
        var nm = total % 12 + 1;
        return FromParts(ny, nm, Math.Min(d, DaysInMonth(ny, nm)));
    }

    public (DateOnly Start, DateOnly End) MonthRange(int year, int month) =>
        (FromParts(year, month, 1), FromParts(year, month, DaysInMonth(year, month)));

    public DateOnly MonthStart(DateOnly date)
    {
        var (y, m, _) = Parts(date);
        return FromParts(y, m, 1);
    }

    public string MonthName(int month, string language) => Kind == CalendarKind.Jalali
        ? (language == "fa" ? JalaliFa : JalaliEn)[month - 1]
        : (language == "fa" ? GregorianFa : GregorianEn)[month - 1];

    /// <summary>1405/08/24 (Jalali) or 2026-11-15 (Gregorian), Latin digits.</summary>
    public string Format(DateOnly date)
    {
        var (y, m, d) = Parts(date);
        return Kind == CalendarKind.Jalali ? $"{y:0000}/{m:00}/{d:00}" : $"{y:0000}-{m:00}-{d:00}";
    }

    /// <summary>"24 Aban 1405" / "15 November 2026".</summary>
    public string FormatLong(DateOnly date, string language)
    {
        var (y, m, d) = Parts(date);
        return $"{d} {MonthName(m, language)} {y}";
    }
}
