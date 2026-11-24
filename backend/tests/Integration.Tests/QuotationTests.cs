using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace Integration.Tests;

public class QuotationTests
{
    private static object Q(int customerId, int? productId = null) => new
    {
        customerId, issueDate = "2026-11-15", validUntil = "2026-11-30", discountType = "Fixed", discountValue = 100_000, taxRate = 10,
        items = new object[]
        {
            new { productId, description = "طراحی لوگو", unit = "پروژه", quantity = 1, unitPrice = 1_000_000, discountType = "Percent", discountValue = 10, isTaxable = true },
            new { description = "مشاوره", unit = "ساعت", quantity = 2, unitPrice = 150_000, discountType = "None", discountValue = 0, isTaxable = false },
        },
    };

    [Fact]
    public async Task Quotation_lifecycle_and_single_conversion()
    {
        using var app = new TestApp();
        var c = await app.SignedInClient();
        var (cid, pid) = await InvoiceTests.SeedAsync(c);

        var q = await c.Post<JsonObject>("/api/v1/quotations", Q(cid, pid));
        var id = q["id"]!.GetValue<int>();
        Assert.Equal("QUO-1405-0001", q["quotationNumber"]!.GetValue<string>());
        Assert.Equal("Draft", q["status"]!.GetValue<string>());
        var total = q["total"]!.GetValue<decimal>();

        Assert.Equal("quotation.not_accepted", await ApiClient.Code(await c.Post($"/api/v1/quotations/{id}/convert")));
        await c.Post<JsonObject>($"/api/v1/quotations/{id}/send");
        await c.Put<JsonObject>($"/api/v1/quotations/{id}", Q(cid, pid)); // still editable when sent
        var accepted = await c.Post<JsonObject>($"/api/v1/quotations/{id}/accept");
        Assert.Equal("Accepted", accepted["status"]!.GetValue<string>());
        Assert.Equal("quotation.locked", await ApiClient.Code(await c.Put($"/api/v1/quotations/{id}", Q(cid, pid))));

        // Two simultaneous conversions: exactly one wins.
        var results = await Task.WhenAll(c.Post($"/api/v1/quotations/{id}/convert"), c.Post($"/api/v1/quotations/{id}/convert"));
        Assert.Single(results, r => r.IsSuccessStatusCode);
        var inv = await results.Single(r => r.IsSuccessStatusCode).Content.ReadFromJsonAsync<JsonObject>();
        Assert.Equal("Draft", inv!["status"]!.GetValue<string>());
        Assert.Equal(id, inv["sourceQuotationId"]!.GetValue<int>());
        Assert.Equal("QUO-1405-0001", inv["sourceQuotationNumber"]!.GetValue<string>());
        Assert.Equal(total, inv["total"]!.GetValue<decimal>()); // lines and pricing preserved
        Assert.Equal(2, inv["items"]!.AsArray().Count);
        Assert.Equal("Percent", inv["items"]![0]!["discountType"]!.GetValue<string>());

        var after = await c.Get<JsonObject>($"/api/v1/quotations/{id}");
        Assert.Equal("Converted", after["status"]!.GetValue<string>());
        Assert.Equal(inv["id"]!.GetValue<int>(), after["convertedInvoiceId"]!.GetValue<int>());
        Assert.Equal("quotation.already_converted", await ApiClient.Code(await c.Post($"/api/v1/quotations/{id}/convert")));

        var pdf = await c.Get($"/api/v1/quotations/{id}/pdf");
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);

        var dup = await c.Post<JsonObject>($"/api/v1/quotations/{id}/duplicate");
        Assert.Equal("QUO-1405-0002", dup["quotationNumber"]!.GetValue<string>());
        Assert.Equal("Draft", dup["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Rejected_and_draft_rules()
    {
        using var app = new TestApp();
        var c = await app.SignedInClient();
        var (cid, _) = await InvoiceTests.SeedAsync(c);
        var q = await c.Post<JsonObject>("/api/v1/quotations", Q(cid));
        var id = q["id"]!.GetValue<int>();
        await c.Post<JsonObject>($"/api/v1/quotations/{id}/reject");
        Assert.Equal("quotation.invalid_status", await ApiClient.Code(await c.Post($"/api/v1/quotations/{id}/accept")));
        Assert.Equal("quotation.not_draft", await ApiClient.Code(await c.Delete($"/api/v1/quotations/{id}")));

        var d = await c.Post<JsonObject>("/api/v1/quotations", Q(cid));
        (await c.Delete($"/api/v1/quotations/{d["id"]}")).EnsureSuccessStatusCode();
        Assert.Equal(1, (await c.Get<JsonObject>("/api/v1/quotations"))["total"]!.GetValue<int>());
    }

    [Fact]
    public async Task Customer_specific_prices()
    {
        using var app = new TestApp();
        var c = await app.SignedInClient();
        var (cid, pid) = await InvoiceTests.SeedAsync(c);
        var saved = await c.Put<JsonArray>($"/api/v1/customers/{cid}/prices", new[] { new { productId = pid, price = 800_000 } });
        Assert.Equal(800_000m, saved[0]!["price"]!.GetValue<decimal>());
        var products = await c.Get<JsonObject>($"/api/v1/products?customerId={cid}");
        Assert.Equal(800_000m, products["items"]![0]!["customerPrice"]!.GetValue<decimal>());
        Assert.Null((await c.Get<JsonObject>("/api/v1/products"))["items"]![0]!["customerPrice"]);
        Assert.Equal("price.negative", await ApiClient.Code(await c.Put($"/api/v1/customers/{cid}/prices", new[] { new { productId = pid, price = -1 } })));
        Assert.Empty(await c.Put<JsonArray>($"/api/v1/customers/{cid}/prices", Array.Empty<object>()));
    }
}
