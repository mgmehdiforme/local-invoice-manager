using LocalInvoiceManager.Domain.Calendar;
using LocalInvoiceManager.Domain.Entities;
using LocalInvoiceManager.Domain;

namespace Domain.Tests;

public class JalaliTests
{
    [Fact]
    public void Converts_known_dates()
    {
        Assert.Equal((1405, 8, 24), Jalali.ToJalali(new DateOnly(2026, 11, 15)));
        Assert.Equal(new DateOnly(2026, 3, 21), Jalali.FromJalali(1405, 1, 1));
        Assert.Equal("1405/08/24", Jalali.Format(new DateOnly(2026, 11, 15)));
    }

    [Fact]
    public void Leap_years()
    {
        Assert.True(Jalali.IsLeapYear(1403));
        Assert.False(Jalali.IsLeapYear(1404));
        Assert.Equal(new DateOnly(2025, 3, 20), Jalali.FromJalali(1403, 12, 30));
        Assert.Throws<ArgumentOutOfRangeException>(() => Jalali.FromJalali(1404, 12, 30));
    }

    [Fact]
    public void Year_boundary_at_nowruz()
    {
        Assert.Equal(1404, Jalali.Year(new DateOnly(2026, 3, 20)));
        Assert.Equal(1405, Jalali.Year(new DateOnly(2026, 3, 21)));
    }

    [Fact]
    public void Add_months_clamps_day()
    {
        var d = Jalali.FromJalali(1405, 6, 31);
        Assert.Equal(Jalali.FromJalali(1405, 7, 30), Jalali.AddMonths(d, 1));
        Assert.Equal(Jalali.FromJalali(1406, 1, 31), Jalali.AddMonths(Jalali.FromJalali(1405, 1, 31), 12));
        Assert.Equal(Jalali.FromJalali(1405, 12, 29), Jalali.AddMonths(Jalali.FromJalali(1405, 11, 30), 1));
    }

    [Fact]
    public void Persian_digits() => Assert.Equal("۱۴۰۵/۰۸/۲۴", Jalali.ToPersianDigits("1405/08/24"));

    [Fact]
    public void Recurring_monthly_follows_jalali_months()
    {
        var r = new RecurringInvoice { Frequency = RecurrenceFrequency.Monthly };
        Assert.Equal(Jalali.FromJalali(1405, 2, 1), r.Advance(Jalali.FromJalali(1405, 1, 1), CalendarSystem.Jalali));
        r.Frequency = RecurrenceFrequency.Quarterly;
        Assert.Equal(Jalali.FromJalali(1405, 4, 1), r.Advance(Jalali.FromJalali(1405, 1, 1), CalendarSystem.Jalali));
    }

    [Fact]
    public void Recurring_monthly_follows_gregorian_months_and_clamps_day()
    {
        var r = new RecurringInvoice { Frequency = RecurrenceFrequency.Monthly };
        Assert.Equal(new DateOnly(2026, 2, 28), r.Advance(new DateOnly(2026, 1, 31), CalendarSystem.Gregorian));
        r.Frequency = RecurrenceFrequency.Yearly;
        Assert.Equal(new DateOnly(2029, 2, 28), r.Advance(new DateOnly(2028, 2, 29), CalendarSystem.Gregorian));
    }

    [Fact]
    public void Calendar_system_formats_and_names_months()
    {
        var d = new DateOnly(2026, 11, 15);
        Assert.Equal("2026-11-15", CalendarSystem.Gregorian.Format(d));
        Assert.Equal("1405/08/24", CalendarSystem.Jalali.Format(d));
        Assert.Equal("15 November 2026", CalendarSystem.Gregorian.FormatLong(d, "en"));
        Assert.Equal("24 Aban 1405", CalendarSystem.Jalali.FormatLong(d, "en"));
        Assert.Equal((new DateOnly(2026, 2, 1), new DateOnly(2026, 2, 28)), CalendarSystem.Gregorian.MonthRange(2026, 2));
    }

    [Fact]
    public void Currency_resolution_uses_enabled_list()
    {
        var s = new AppSettings { CurrencyCode = "USD", CurrencySymbol = "$", CurrencyDecimalPlaces = 2,
            CurrenciesJson = CurrencyDef.ToJson([new("USD", "$", 2, SymbolPosition.Before), new("EUR", "€", 2, SymbolPosition.Before)]) };
        Assert.Equal("USD", s.ResolveCurrency(null).Code);
        Assert.Equal("€", s.ResolveCurrency("eur").Symbol);
        Assert.Throws<DomainException>(() => s.ResolveCurrency("GBP"));
    }

    [Fact]
    public void Overdue_is_calculated_not_stored()
    {
        var inv = new Invoice { Status = InvoiceStatus.Issued, Total = 100, PaidAmount = 0, DueDate = new DateOnly(2026, 1, 1) };
        Assert.True(inv.IsOverdue(new DateOnly(2026, 1, 2)));
        Assert.False(inv.IsOverdue(new DateOnly(2026, 1, 1)));
        inv.PaidAmount = 100;
        Assert.False(inv.IsOverdue(new DateOnly(2026, 1, 2)));
        inv.PaidAmount = 0; inv.Status = InvoiceStatus.Cancelled;
        Assert.False(inv.IsOverdue(new DateOnly(2026, 1, 2)));
    }
}
