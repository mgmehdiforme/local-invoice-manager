using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace Integration.Tests;

public class PaymentAndReportTests
{
    private static async Task<(ApiClient c, int invoiceId, int customerId)> IssuedInvoiceAsync(TestApp app, decimal price = 1_000_000, string due = "2099-01-01")
    {
        var c = await app.SignedInClient();
        var (cid, _) = await InvoiceTests.SeedAsync(c);
        var d = await c.Post<JsonObject>("/api/v1/invoices", new
        {
            customerId = cid, issueDate = "2026-11-15", dueDate = due, taxRate = 0,
            items = new[] { new { description = "خدمت", unit = "عدد", quantity = 1, unitPrice = price, discountType = "None", discountValue = 0, isTaxable = true } },
        });
        var id = d["id"]!.GetValue<int>();
        await c.Post<JsonObject>($"/api/v1/invoices/{id}/issue");
        return (c, id, cid);
    }

    private static object Pay(decimal amount, string date = "2026-11-16", string method = "BankTransfer") => new { amount, paymentDate = date, method, reference = "TRX-1" };

    [Fact]
    public async Task Partial_multiple_payments_and_reversal()
    {
        using var app = new TestApp();
        var (c, id, _) = await IssuedInvoiceAsync(app);

        var r1 = await c.Post<JsonObject>($"/api/v1/invoices/{id}/payments", Pay(400_000));
        Assert.Equal("PartiallyPaid", r1["paymentStatus"]!.GetValue<string>());
        Assert.Equal(600_000m, r1["balance"]!.GetValue<decimal>());

        Assert.Equal("payment.exceeds_balance", await ApiClient.Code(await c.Post($"/api/v1/invoices/{id}/payments", Pay(600_001))));
        Assert.Equal("payment.before_issue", await ApiClient.Code(await c.Post($"/api/v1/invoices/{id}/payments", Pay(1, "2026-11-01"))));
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Post($"/api/v1/invoices/{id}/payments", Pay(0))).StatusCode);
        Assert.Equal("payment.precision", await ApiClient.Code(await c.Post($"/api/v1/invoices/{id}/payments", Pay(10.5m))));

        var r2 = await c.Post<JsonObject>($"/api/v1/invoices/{id}/payments", Pay(600_000, method: "Cash"));
        Assert.Equal("Paid", r2["paymentStatus"]!.GetValue<string>());
        Assert.Equal(0m, r2["balance"]!.GetValue<decimal>());
        Assert.Equal("payment.already_paid", await ApiClient.Code(await c.Post($"/api/v1/invoices/{id}/payments", Pay(1))));

        // Cannot cancel while payments are active.
        Assert.Equal("invoice.has_payments", await ApiClient.Code(await c.Post($"/api/v1/invoices/{id}/cancel", new { reason = "x" })));

        var paymentId = r2["payments"]![0]!["id"]!.GetValue<int>();
        Assert.Equal("payment.reverse_reason", await ApiClient.Code(await c.Post($"/api/v1/payments/{paymentId}/reverse", new { reason = " " })));
        var r3 = await c.Post<JsonObject>($"/api/v1/payments/{paymentId}/reverse", new { reason = "چک برگشت خورد" });
        Assert.Equal("PartiallyPaid", r3["paymentStatus"]!.GetValue<string>());
        Assert.Equal(400_000m, r3["paidAmount"]!.GetValue<decimal>());
        // History is kept: the reversed payment is still listed with its reason.
        var reversed = r3["payments"]!.AsArray().Single(p => p!["id"]!.GetValue<int>() == paymentId)!;
        Assert.Equal("Reversed", reversed["status"]!.GetValue<string>());
        Assert.Equal("چک برگشت خورد", reversed["reversalReason"]!.GetValue<string>());
        Assert.Equal("payment.already_reversed", await ApiClient.Code(await c.Post($"/api/v1/payments/{paymentId}/reverse", new { reason = "x" })));

        Assert.Equal(1, (await c.Get<JsonObject>("/api/v1/payments"))["total"]!.GetValue<int>());
        Assert.Equal(2, (await c.Get<JsonObject>("/api/v1/payments?includeReversed=true"))["total"]!.GetValue<int>());
    }

    [Fact]
    public async Task Concurrent_payments_never_overpay()
    {
        using var app = new TestApp();
        var (c, id, _) = await IssuedInvoiceAsync(app);
        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => c.Post($"/api/v1/invoices/{id}/payments", Pay(300_000))));
        Assert.Equal(3, results.Count(r => r.IsSuccessStatusCode)); // 3 × 300,000 fits in 1,000,000; the 4th would overpay
        var inv = await c.Get<JsonObject>($"/api/v1/invoices/{id}");
        Assert.Equal(900_000m, inv["paidAmount"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task Payments_only_for_issued_invoices()
    {
        using var app = new TestApp();
        var c = await app.SignedInClient();
        var (cid, _) = await InvoiceTests.SeedAsync(c);
        var d = await c.Post<JsonObject>("/api/v1/invoices", InvoiceTests.Draft(cid));
        Assert.Equal("payment.invoice_not_issued", await ApiClient.Code(await c.Post($"/api/v1/invoices/{d["id"]}/payments", Pay(1))));
    }

    [Fact]
    public async Task Dashboard_and_reports_match_hand_calculation()
    {
        using var app = new TestApp();
        var (c, id1, cid) = await IssuedInvoiceAsync(app, 1_000_000, "2026-11-20"); // overdue (past due)
        await c.Post<JsonObject>($"/api/v1/invoices/{id1}/payments", Pay(250_000));

        // second invoice, fully paid; third invoice cancelled (excluded); one draft (excluded)
        var d2 = await c.Post<JsonObject>("/api/v1/invoices", new { customerId = cid, issueDate = "2026-11-15", taxRate = 10, items = new[] { new { description = "x", unit = "عدد", quantity = 2, unitPrice = 500_000, discountType = "None", discountValue = 0, isTaxable = true } } });
        await c.Post<JsonObject>($"/api/v1/invoices/{d2["id"]}/issue");
        await c.Post<JsonObject>($"/api/v1/invoices/{d2["id"]}/payments", Pay(1_100_000));
        var d3 = await c.Post<JsonObject>("/api/v1/invoices", InvoiceTests.Draft(cid));
        await c.Post<JsonObject>($"/api/v1/invoices/{d3["id"]}/issue");
        await c.Post<JsonObject>($"/api/v1/invoices/{d3["id"]}/cancel", new { reason = "test" });
        await c.Post<JsonObject>("/api/v1/invoices", InvoiceTests.Draft(cid));

        var s = await c.Get<JsonObject>("/api/v1/dashboard/summary");
        Assert.Equal(2, s["issuedCount"]!.GetValue<int>());
        Assert.Equal(1, s["draftCount"]!.GetValue<int>());
        Assert.Equal(2_100_000m, s["totalInvoiced"]!.GetValue<decimal>());
        Assert.Equal(1_350_000m, s["totalReceived"]!.GetValue<decimal>());
        Assert.Equal(750_000m, s["outstanding"]!.GetValue<decimal>());
        Assert.Equal(1, s["overdueCount"]!.GetValue<int>());
        Assert.Equal(750_000m, s["overdueAmount"]!.GetValue<decimal>());

        var overdue = await c.Get<JsonArray>("/api/v1/dashboard/overdue");
        Assert.Single(overdue);

        var rev = await c.Get<JsonArray>("/api/v1/reports/revenue?from=2026-10-23&to=2026-12-21&groupBy=month"); // Aban + Azar 1405
        Assert.Equal(2, rev.Count);
        Assert.Equal("آبان 1405", rev[0]!["label"]!.GetValue<string>());
        Assert.Equal(2_100_000m, rev[0]!["invoiced"]!.GetValue<decimal>());
        Assert.Equal(100_000m, rev[0]!["tax"]!.GetValue<decimal>());
        Assert.Equal(1_350_000m, rev[0]!["received"]!.GetValue<decimal>());

        var cust = await c.Get<JsonArray>("/api/v1/reports/customers");
        Assert.Equal(750_000m, cust[0]!["balance"]!.GetValue<decimal>());

        var monthly = await c.Get<JsonArray>("/api/v1/dashboard/revenue?months=6");
        Assert.Equal(6, monthly.Count);

        var csv = await c.Get("/api/v1/reports/customers.csv");
        var bytes = await csv.Content.ReadAsByteArrayAsync();
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]); // BOM for Excel
        Assert.Contains("علی رضایی", System.Text.Encoding.UTF8.GetString(bytes));
    }
}
