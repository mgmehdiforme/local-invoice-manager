using LocalInvoiceManager.Application.Abstractions;
using LocalInvoiceManager.Domain.Calendar;
using LocalInvoiceManager.Domain.Entities;

namespace LocalInvoiceManager.Application.Settings;

/// <summary>
/// Allocates document numbers like INV-1405-0001. Must be called inside the caller's transaction;
/// the unique index on the number column is the final guard against duplicates.
/// </summary>
public sealed class NumberingService(IAppDbContext db, IBusinessClock clock)
{
    public async Task<string> NextInvoiceNumberAsync(DateOnly issueDate, CancellationToken ct)
    {
        var s = await db.GetAsync(ct);
        return await AllocateAsync(s.InvoicePrefix, s, issueDate, ct);
    }

    public async Task<string> NextQuotationNumberAsync(DateOnly issueDate, CancellationToken ct)
    {
        var s = await db.GetAsync(ct);
        return await AllocateAsync(s.QuotationPrefix, s, issueDate, ct);
    }

    public async Task<string> PreviewInvoiceNumberAsync(CancellationToken ct)
    {
        var s = await db.GetAsync(ct);
        var key = Key(s.InvoicePrefix, s, clock.Today);
        var seq = await db.NumberSequences.FindAsync([key], ct);
        return Format(s.InvoicePrefix, s, clock.Today, seq?.NextValue ?? 1);
    }

    private async Task<string> AllocateAsync(string prefix, AppSettings s, DateOnly date, CancellationToken ct)
    {
        var key = Key(prefix, s, date);
        var seq = await db.NumberSequences.FindAsync([key], ct);
        if (seq is null)
        {
            seq = new NumberSequence { Key = key, NextValue = 1 };
            db.NumberSequences.Add(seq);
        }
        var value = seq.NextValue;
        seq.NextValue++;
        return Format(prefix, s, date, value);
    }

    private static string Key(string prefix, AppSettings s, DateOnly date) =>
        s.ResetNumberingYearly ? $"{prefix}-{s.CalendarSystem.Year(date)}" : prefix;

    public static string Format(string prefix, AppSettings s, DateOnly date, int value)
    {
        var number = value.ToString().PadLeft(s.NumberPadding, '0');
        // The year follows the app calendar: INV-2026-0001 (Gregorian) or INV-1405-0001 (Jalali).
        return s.ResetNumberingYearly ? $"{prefix}-{s.CalendarSystem.Year(date)}-{number}" : $"{prefix}-{number}";
    }
}
