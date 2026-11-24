using System.Globalization;

namespace LocalInvoiceManager.Domain.Calendar;

/// <summary>Jalali (Persian/Solar Hijri) helpers. Canonical storage stays Gregorian; Jalali is for display, numbering and schedules.</summary>
public static class Jalali
{
    private static readonly PersianCalendar Calendar = new();

    public static readonly string[] MonthNames =
        ["فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"];

    public static int Year(DateOnly date) => Calendar.GetYear(date.ToDateTime(TimeOnly.MinValue));

    public static (int Year, int Month, int Day) ToJalali(DateOnly date)
    {
        var dt = date.ToDateTime(TimeOnly.MinValue);
        return (Calendar.GetYear(dt), Calendar.GetMonth(dt), Calendar.GetDayOfMonth(dt));
    }

    public static DateOnly FromJalali(int year, int month, int day)
    {
        if (month is < 1 or > 12) throw new ArgumentOutOfRangeException(nameof(month));
        var maxDay = Calendar.GetDaysInMonth(year, month);
        if (day < 1 || day > maxDay) throw new ArgumentOutOfRangeException(nameof(day));
        return DateOnly.FromDateTime(Calendar.ToDateTime(year, month, day, 0, 0, 0, 0));
    }

    public static bool IsLeapYear(int year) => Calendar.IsLeapYear(year);

    public static int DaysInMonth(int year, int month) => Calendar.GetDaysInMonth(year, month);

    /// <summary>Adds Jalali months, clamping the day to the target month's length (e.g. 31 Shahrivar + 1 month = 30 Mehr).</summary>
    public static DateOnly AddMonths(DateOnly date, int months)
    {
        var (y, m, d) = ToJalali(date);
        var total = (y * 12 + (m - 1)) + months;
        var ny = total / 12;
        var nm = total % 12 + 1;
        var nd = Math.Min(d, DaysInMonth(ny, nm));
        return FromJalali(ny, nm, nd);
    }

    /// <summary>yyyy/MM/dd with Latin digits, e.g. 1405/08/24.</summary>
    public static string Format(DateOnly date)
    {
        var (y, m, d) = ToJalali(date);
        return $"{y:0000}/{m:00}/{d:00}";
    }

    public static string FormatLong(DateOnly date)
    {
        var (y, m, d) = ToJalali(date);
        return $"{d} {MonthNames[m - 1]} {y}";
    }

    public static (DateOnly Start, DateOnly End) MonthRange(int year, int month) =>
        (FromJalali(year, month, 1), FromJalali(year, month, DaysInMonth(year, month)));

    private static readonly char[] PersianDigits = ['۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹'];

    public static string ToPersianDigits(string value)
    {
        var chars = value.ToCharArray();
        for (var i = 0; i < chars.Length; i++)
            if (chars[i] is >= '0' and <= '9') chars[i] = PersianDigits[chars[i] - '0'];
        return new string(chars);
    }
}
