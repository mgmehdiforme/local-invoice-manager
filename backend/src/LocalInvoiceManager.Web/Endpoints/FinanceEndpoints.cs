using LocalInvoiceManager.Application.Payments;
using LocalInvoiceManager.Application.Reports;
using Microsoft.AspNetCore.Mvc;

namespace LocalInvoiceManager.Web.Endpoints;

public static class FinanceEndpoints
{
    public static void MapFinance(this RouteGroupBuilder api)
    {
        api.MapPost("/invoices/{id:int}/payments", (int id, PaymentInput input, PaymentService s, CancellationToken ct) => s.AddAsync(id, input, ct)).WithTags("Payments");
        api.MapPost("/payments/{id:int}/reverse", (int id, InvoiceEndpoints.ReasonRequest req, PaymentService s, CancellationToken ct) => s.ReverseAsync(id, req.Reason, ct)).WithTags("Payments");
        api.MapGet("/payments", ([AsParameters] PaymentQuery q, PaymentService s, CancellationToken ct) => s.ListAsync(q, ct)).WithTags("Payments");

        var d = api.MapGroup("/dashboard").WithTags("Dashboard");
        d.MapGet("/summary", (string? currency, ReportService r, CancellationToken ct) => r.SummaryAsync(currency, ct));
        d.MapGet("/revenue", (int? months, string? currency, ReportService r, CancellationToken ct) => r.MonthlyAsync(months ?? 12, currency, ct));
        d.MapGet("/overdue", (ReportService r, CancellationToken ct) => r.OverdueAsync(ct));

        var g = api.MapGroup("/reports").WithTags("Reports");
        g.MapGet("/revenue", (DateOnly from, DateOnly to, string? groupBy, string? currency, ReportService r, CancellationToken ct) => r.RevenueAsync(from, to, groupBy ?? "month", currency, ct));
        g.MapGet("/customers", (DateOnly? from, DateOnly? to, string? currency, ReportService r, CancellationToken ct) => r.CustomersAsync(from, to, currency, ct));
        g.MapGet("/revenue.csv", async (DateOnly from, DateOnly to, string? groupBy, string? currency, ReportService r, CancellationToken ct) =>
            Results.File(ReportService.RevenueCsv(await r.RevenueAsync(from, to, groupBy ?? "month", currency, ct)), "text/csv; charset=utf-8", $"revenue-{from:yyyyMMdd}-{to:yyyyMMdd}.csv"));
        g.MapGet("/customers.csv", async (DateOnly? from, DateOnly? to, string? currency, ReportService r, CancellationToken ct) =>
            Results.File(ReportService.CustomersCsv(await r.CustomersAsync(from, to, currency, ct)), "text/csv; charset=utf-8", "customers-report.csv"));
    }
}
