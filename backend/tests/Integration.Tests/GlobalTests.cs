using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Integration.Tests;

/// <summary>Phase 8: English + Gregorian setup, several currencies, translated messages and documents.</summary>
public class GlobalTests
{
    private static object Line(string name, decimal price) =>
        new { description = name, unit = "pcs", quantity = 1, unitPrice = price, discountType = "None", discountValue = 0, isTaxable = true };

    private static async Task<int> CustomerAsync(ApiClient c) =>
        (await c.Post<JsonObject>("/api/v1/customers", new { name = "Acme Ltd", email = "billing@example.com", mobile = "+44 20 7946 0000" }))["id"]!.GetValue<int>();

    private static async Task<JsonObject> AddEuroAsync(ApiClient c)
    {
        var s = await c.Get<JsonObject>("/api/v1/settings");
        s["currencies"] = new JsonArray(
            new JsonObject { ["code"] = "USD", ["symbol"] = "$", ["decimalPlaces"] = 2, ["symbolPosition"] = "Before" },
            new JsonObject { ["code"] = "EUR", ["symbol"] = "€", ["decimalPlaces"] = 2, ["symbolPosition"] = "Before" });
        return await c.Put<JsonObject>("/api/v1/settings", s);
    }

    [Fact]
    public async Task English_setup_uses_gregorian_numbering_and_english_messages()
    {
        using var app = new TestApp();
        var c = await app.SignedInClient(language: "en", currencyCode: "USD");

        var status = await c.Get<JsonObject>("/api/v1/auth/status");
        Assert.Equal("en", status["language"]!.GetValue<string>());
        Assert.Equal("Gregorian", status["calendar"]!.GetValue<string>());

        var s = await c.Get<JsonObject>("/api/v1/settings");
        Assert.Equal("USD", s["currencyCode"]!.GetValue<string>());
        Assert.Equal(",", s["thousandsSeparator"]!.GetValue<string>());

        var cid = await CustomerAsync(c);
        var d = await c.Post<JsonObject>("/api/v1/invoices", new { customerId = cid, issueDate = "2026-11-15", taxRate = 0, items = new[] { Line("Design", 99.5m) } });
        var issued = await c.Post<JsonObject>($"/api/v1/invoices/{d["id"]}/issue");
        Assert.Equal("INV-2026-0001", issued["invoiceNumber"]!.GetValue<string>());
        Assert.Equal("USD", issued["currency"]!["currencyCode"]!.GetValue<string>());

        // Errors come back in English (the UI sends Accept-Language: en).
        var req = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/invoices/{d["id"]}/payments")
        {
            Content = JsonContent.Create(new { amount = 1000, paymentDate = "2026-11-16", method = "Cash" }),
        };
        req.Headers.Add("X-XSRF-TOKEN", c.Xsrf);
        req.Headers.Add("Accept-Language", "en");
        var res = await c.Http.SendAsync(req);
        var problem = await res.Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal("The payment is larger than the invoice balance.", problem!["detail"]!.GetValue<string>());

        // Persian when asked for Persian.
        var req2 = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/invoices/{d["id"]}/payments")
        {
            Content = JsonContent.Create(new { amount = 1000, paymentDate = "2026-11-16", method = "Cash" }),
        };
        req2.Headers.Add("X-XSRF-TOKEN", c.Xsrf);
        req2.Headers.Add("Accept-Language", "fa");
        var fa = await (await c.Http.SendAsync(req2)).Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal("مبلغ پرداخت از مانده فاکتور بیشتر است.", fa!["detail"]!.GetValue<string>());

        // English PDF renders, and the revenue report uses Gregorian month names.
        var pdf = await c.Get($"/api/v1/invoices/{d["id"]}/pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.StartsWith("%PDF", System.Text.Encoding.ASCII.GetString(await pdf.Content.ReadAsByteArrayAsync())[..4]);
        var revenue = await c.Get<JsonArray>("/api/v1/reports/revenue?from=2026-11-01&to=2026-11-30");
        Assert.Equal("November 2026", revenue[0]!["label"]!.GetValue<string>());
        Assert.Equal(99.5m, revenue[0]!["invoiced"]!.GetValue<decimal>());

        // WhatsApp link works with an international number.
        var share = await c.Get<JsonObject>($"/api/v1/invoices/{d["id"]}/share");
        Assert.StartsWith("https://wa.me/442079460000?", share["whatsappUrl"]!.GetValue<string>());
        Assert.Contains("Invoice INV-2026-0001 from", share["text"]!.GetValue<string>());
    }

    [Fact]
    public async Task Invoices_in_two_currencies_are_reported_separately()
    {
        using var app = new TestApp();
        var c = await app.SignedInClient(language: "en", currencyCode: "USD");
        await AddEuroAsync(c);
        var cid = await CustomerAsync(c);

        async Task<int> Issue(string currency, decimal price)
        {
            var d = await c.Post<JsonObject>("/api/v1/invoices", new { customerId = cid, issueDate = "2026-11-15", taxRate = 0, currencyCode = currency, items = new[] { Line("Work", price) } });
            var i = await c.Post<JsonObject>($"/api/v1/invoices/{d["id"]}/issue");
            Assert.Equal(currency, i["currency"]!["currencyCode"]!.GetValue<string>());
            return i["id"]!.GetValue<int>();
        }
        var usd = await Issue("USD", 100m);
        var eur = await Issue("EUR", 250m);
        await c.Post<JsonObject>($"/api/v1/invoices/{eur}/payments", new { amount = 50, paymentDate = "2026-11-16", method = "Card" });

        // A currency that is not enabled is rejected.
        var bad = await c.Post("/api/v1/invoices", new { customerId = cid, issueDate = "2026-11-15", taxRate = 0, currencyCode = "GBP", items = new[] { Line("x", 1) } });
        Assert.Equal("currency.unknown", await ApiClient.Code(bad));

        var usdSummary = await c.Get<JsonObject>("/api/v1/dashboard/summary");
        Assert.Equal(100m, usdSummary["totalInvoiced"]!.GetValue<decimal>());
        var eurSummary = await c.Get<JsonObject>("/api/v1/dashboard/summary?currency=EUR");
        Assert.Equal(250m, eurSummary["totalInvoiced"]!.GetValue<decimal>());
        Assert.Equal(50m, eurSummary["totalReceived"]!.GetValue<decimal>());

        var balance = await c.Get<JsonObject>($"/api/v1/customers/{cid}/balance");
        Assert.Equal(100m, balance["balance"]!.GetValue<decimal>()); // default currency only
        var byCurrency = balance["byCurrency"]!.AsArray();
        Assert.Equal(2, byCurrency.Count);
        Assert.Equal(200m, byCurrency.Single(x => x!["currencyCode"]!.GetValue<string>() == "EUR")!["balance"]!.GetValue<decimal>());

        var payments = await c.Get<JsonObject>("/api/v1/payments");
        Assert.Equal("EUR", payments["items"]![0]!["currencyCode"]!.GetValue<string>());

        // Duplicating keeps the currency.
        var copy = await c.Post<JsonObject>($"/api/v1/invoices/{eur}/duplicate");
        Assert.Equal("EUR", copy["currency"]!["currencyCode"]!.GetValue<string>());
        _ = usd;
    }

    [Fact]
    public async Task Switching_preferences_changes_calendar_for_new_numbers()
    {
        using var app = new TestApp();
        var c = await app.SignedInClient(language: "en", currencyCode: "USD");
        Assert.Equal(HttpStatusCode.NoContent, (await c.Put("/api/v1/settings/preferences", new { language = "fa", calendar = "Jalali" })).StatusCode);
        var status = await c.Get<JsonObject>("/api/v1/auth/status");
        Assert.Equal("fa", status["language"]!.GetValue<string>());
        var cid = await CustomerAsync(c);
        var d = await c.Post<JsonObject>("/api/v1/invoices", new { customerId = cid, issueDate = "2026-11-15", taxRate = 0, items = new[] { Line("Work", 10) } });
        var issued = await c.Post<JsonObject>($"/api/v1/invoices/{d["id"]}/issue");
        Assert.Equal("INV-1405-0001", issued["invoiceNumber"]!.GetValue<string>());
        _ = JsonSerializer.Serialize(issued);
    }
}
