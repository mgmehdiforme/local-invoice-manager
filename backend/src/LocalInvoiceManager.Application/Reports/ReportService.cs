using LocalInvoiceManager.Application.Settings;
using System.Text;
using LocalInvoiceManager.Application.Abstractions;
using LocalInvoiceManager.Application.Invoices;
using LocalInvoiceManager.Domain;
using LocalInvoiceManager.Domain.Calendar;
using LocalInvoiceManager.Domain.Localization;
using LocalInvoiceManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LocalInvoiceManager.Application.Reports;

public sealed record DashboardSummary(
    int IssuedCount, int DraftCount, decimal TotalInvoiced, decimal TotalReceived, decimal Outstanding,
    int OverdueCount, decimal OverdueAmount, decimal MonthInvoiced, decimal MonthReceived, string MonthLabel,
    int CustomerCount, int DueSoonCount, decimal DueSoonAmount);

public sealed record RevenuePoint(string Key, string Label, DateOnly Start, DateOnly End, decimal Invoiced, decimal Tax, decimal Received, int InvoiceCount);

public sealed record CustomerReportRow(int CustomerId, string CustomerName, int InvoiceCount, decimal Invoiced, decimal Paid, decimal Balance, decimal Overdue);

/// <summary>
/// Dashboard and reports. Only issued invoices count (drafts and cancelled invoices are excluded);
/// "received" counts active payments by payment date. All sums are decimal math in .NET.
/// Amounts in different currencies are never added together: every figure is for one currency
/// (the <c>currency</c> argument, or the default currency). Months follow the app calendar.
/// </summary>
public sealed class ReportService(IAppDbContext db, IBusinessClock clock)
{
    private sealed record InvRow(int Id, int CustomerId, string CustomerName, DateOnly IssueDate, DateOnly? DueDate, decimal Total, decimal TaxAmount, decimal PaidAmount);
    private sealed record PayRow(DateOnly PaymentDate, decimal Amount, int CustomerId);

    /// <summary>The currency to report on: the requested one, or the default currency.</summary>
    private async Task<string> CurrencyAsync(string? currency, CancellationToken ct) =>
        string.IsNullOrWhiteSpace(currency) ? (await db.GetAsync(ct)).CurrencyCode : currency.Trim().ToUpperInvariant();

    private async Task<List<InvRow>> IssuedAsync(string currency, CancellationToken ct) =>
        await db.Invoices.AsNoTracking().Where(i => i.Status == InvoiceStatus.Issued && i.CurrencyCode == currency)
            .Select(i => new InvRow(i.Id, i.CustomerId, i.CustomerName, i.IssueDate, i.DueDate, i.Total, i.TaxAmount, i.PaidAmount))
            .ToListAsync(ct);

    private async Task<List<PayRow>> PaymentsAsync(string currency, CancellationToken ct) =>
        await db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentRecordStatus.Active && p.Invoice!.Status == InvoiceStatus.Issued && p.Invoice.CurrencyCode == currency)
            .Select(p => new PayRow(p.PaymentDate, p.Amount, p.Invoice!.CustomerId))
            .ToListAsync(ct);

    public async Task<DashboardSummary> SummaryAsync(string? currency, CancellationToken ct)
    {
        var today = clock.Today;
        var s = await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(ct);
        var dueSoonDays = s?.ReminderDaysBefore is > 0 ? s.ReminderDaysBefore : 7;
        var code = await CurrencyAsync(currency, ct);
        var inv = await IssuedAsync(code, ct);
        var pays = await PaymentsAsync(code, ct);
        var drafts = await db.Invoices.CountAsync(i => i.Status == InvoiceStatus.Draft, ct);
        var customers = await db.Customers.CountAsync(c => c.IsActive, ct);
        var cal = Loc.Calendar;
        var (jy, jm, _) = cal.Parts(today);
        var (mStart, mEnd) = cal.MonthRange(jy, jm);

        var open = inv.Where(i => i.Total - i.PaidAmount > 0).ToList();
        var overdue = open.Where(i => i.DueDate is { } d && d < today).ToList();
        var dueSoon = open.Where(i => i.DueDate is { } d && d >= today && d <= today.AddDays(dueSoonDays)).ToList();

        return new DashboardSummary(
            inv.Count, drafts, inv.Sum(i => i.Total), pays.Sum(p => p.Amount), open.Sum(i => i.Total - i.PaidAmount),
            overdue.Count, overdue.Sum(i => i.Total - i.PaidAmount),
            inv.Where(i => i.IssueDate >= mStart && i.IssueDate <= mEnd).Sum(i => i.Total),
            pays.Where(p => p.PaymentDate >= mStart && p.PaymentDate <= mEnd).Sum(p => p.Amount),
            $"{cal.MonthName(jm, Loc.Language)} {jy}", customers, dueSoon.Count, dueSoon.Sum(i => i.Total - i.PaidAmount));
    }

    /// <summary>Last N months (app calendar) including the current one, oldest first.</summary>
    public async Task<IReadOnlyList<RevenuePoint>> MonthlyAsync(int months, string? currency, CancellationToken ct)
    {
        months = Math.Clamp(months, 1, 36);
        var cal = Loc.Calendar;
        var thisMonth = cal.MonthStart(clock.Today);
        var (y, m, _) = cal.Parts(thisMonth);
        return await RevenueAsync(cal.AddMonths(thisMonth, -(months - 1)), cal.MonthRange(y, m).End, "month", currency, ct);
    }

    public async Task<IReadOnlyList<RevenuePoint>> RevenueAsync(DateOnly from, DateOnly to, string groupBy, string? currency, CancellationToken ct)
    {
        if (to < from) (from, to) = (to, from);
        if (to.DayNumber - from.DayNumber > 3660) to = from.AddDays(3660);
        var code = await CurrencyAsync(currency, ct);
        var inv = (await IssuedAsync(code, ct)).Where(i => i.IssueDate >= from && i.IssueDate <= to).ToList();
        var pays = (await PaymentsAsync(code, ct)).Where(p => p.PaymentDate >= from && p.PaymentDate <= to).ToList();
        var cal = Loc.Calendar;

        var buckets = new List<(string Key, string Label, DateOnly Start, DateOnly End)>();
        if (groupBy == "day")
        {
            for (var d = from; d <= to && buckets.Count < 400; d = d.AddDays(1))
                buckets.Add((d.ToString("yyyy-MM-dd"), cal.Format(d), d, d));
        }
        else
        {
            var (y, m, _) = cal.Parts(from);
            while (true)
            {
                var (start, end) = cal.MonthRange(y, m);
                if (start > to) break;
                buckets.Add(($"{y}-{m:00}", $"{cal.MonthName(m, Loc.Language)} {y}", start < from ? from : start, end > to ? to : end));
                if (++m > 12) { m = 1; y++; }
            }
        }

        return buckets.Select(b =>
        {
            var bi = inv.Where(i => i.IssueDate >= b.Start && i.IssueDate <= b.End).ToList();
            return new RevenuePoint(b.Key, b.Label, b.Start, b.End, bi.Sum(i => i.Total), bi.Sum(i => i.TaxAmount),
                pays.Where(p => p.PaymentDate >= b.Start && p.PaymentDate <= b.End).Sum(p => p.Amount), bi.Count);
        }).ToList();
    }

    public async Task<IReadOnlyList<CustomerReportRow>> CustomersAsync(DateOnly? from, DateOnly? to, string? currency, CancellationToken ct)
    {
        var today = clock.Today;
        var inv = (await IssuedAsync(await CurrencyAsync(currency, ct), ct)).Where(i => (from == null || i.IssueDate >= from) && (to == null || i.IssueDate <= to)).ToList();
        return inv.GroupBy(i => i.CustomerId)
            .Select(g => new CustomerReportRow(
                g.Key, g.OrderByDescending(i => i.IssueDate).First().CustomerName, g.Count(), g.Sum(i => i.Total), g.Sum(i => i.PaidAmount),
                g.Sum(i => Math.Max(0, i.Total - i.PaidAmount)),
                g.Where(i => i.DueDate is { } d && d < today).Sum(i => Math.Max(0, i.Total - i.PaidAmount))))
            .OrderByDescending(r => r.Invoiced)
            .ToList();
    }

    public async Task<IReadOnlyList<InvoiceListItem>> OverdueAsync(CancellationToken ct)
    {
        var today = clock.Today;
        var rows = await db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Issued && i.PaymentStatus != PaymentStatus.Paid && i.DueDate != null && i.DueDate < today)
            .OrderBy(i => i.DueDate)
            .ToListAsync(ct);
        return rows.Select(i => InvoiceService.ToListItem(i, today)).ToList();
    }

    // ---- CSV (UTF-8 with BOM so Excel opens non-Latin text correctly); headers in the app language ----

    public static byte[] RevenueCsv(IEnumerable<RevenuePoint> rows) => Csv(
        [Loc.T("دوره"), Loc.T("از"), Loc.T("تا"), Loc.T("تعداد فاکتور"), Loc.T("مبلغ فاکتورها"), Loc.T("مالیات"), Loc.T("دریافتی")],
        rows.Select(r => new[] { r.Label, Loc.Calendar.Format(r.Start), Loc.Calendar.Format(r.End), r.InvoiceCount.ToString(), r.Invoiced.ToString("0.####"), r.Tax.ToString("0.####"), r.Received.ToString("0.####") }));

    public static byte[] CustomersCsv(IEnumerable<CustomerReportRow> rows) => Csv(
        [Loc.T("مشتری"), Loc.T("تعداد فاکتور"), Loc.T("مبلغ فاکتورها"), Loc.T("پرداخت‌شده"), Loc.T("مانده"), Loc.T("سررسید گذشته")],
        rows.Select(r => new[] { r.CustomerName, r.InvoiceCount.ToString(), r.Invoiced.ToString("0.####"), r.Paid.ToString("0.####"), r.Balance.ToString("0.####"), r.Overdue.ToString("0.####") }));

    private static byte[] Csv(string[] header, IEnumerable<string[]> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(',', header.Select(Escape)));
        foreach (var r in rows) sb.AppendLine(string.Join(',', r.Select(Escape)));
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }

    private static string Escape(string v)
    {
        // Neutralise spreadsheet formulas (CSV injection) and quote when needed.
        if (v.Length > 0 && "=+-@".Contains(v[0]) && !decimal.TryParse(v, out _)) v = "'" + v;
        return v.IndexOfAny([',', '"', '\n', '\r']) >= 0 ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
    }
}
