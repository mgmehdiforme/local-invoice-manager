using System.Net;
using System.Text.Json.Nodes;

namespace Integration.Tests;

public class MasterDataTests
{
    [Fact]
    public async Task Customer_crud_search_and_archive()
    {
        using var app = new TestApp();
        var c = await app.SignedInClient();

        var bad = await c.Post("/api/v1/customers", new { name = "", email = "not-an-email" });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var ali = await c.Post<JsonObject>("/api/v1/customers", new { name = "علی رضایی", mobile = "09121234567", email = "ali@example.com" });
        await c.Post<JsonObject>("/api/v1/customers", new { name = "شرکت كيان", companyName = "Kian Co" }); // Arabic ي/ك typed
        var id = ali["id"]!.GetValue<int>();

        // Search normalizes Persian/Arabic letters and digits.
        var found = await c.Get<JsonObject>("/api/v1/customers?search=کیان");
        Assert.Equal(1, found["total"]!.GetValue<int>());
        var byPhone = await c.Get<JsonObject>("/api/v1/customers?search=۰۹۱۲");
        Assert.Equal("علی رضایی", byPhone["items"]![0]!["name"]!.GetValue<string>());

        var updated = await c.Put<JsonObject>($"/api/v1/customers/{id}", new { name = "علی رضایی", city = "تهران" });
        Assert.Equal("تهران", updated["city"]!.GetValue<string>());
        Assert.Null(updated["email"]); // cleared

        (await c.Delete($"/api/v1/customers/{id}")).EnsureSuccessStatusCode();
        Assert.Equal(1, (await c.Get<JsonObject>("/api/v1/customers"))["total"]!.GetValue<int>());
        Assert.Equal(2, (await c.Get<JsonObject>("/api/v1/customers?includeInactive=true"))["total"]!.GetValue<int>());
        (await c.Post($"/api/v1/customers/{id}/restore")).EnsureSuccessStatusCode();
        Assert.Equal(2, (await c.Get<JsonObject>("/api/v1/customers"))["total"]!.GetValue<int>());

        Assert.Equal(HttpStatusCode.NotFound, (await c.Get("/api/v1/customers/9999")).StatusCode);
        var balance = await c.Get<JsonObject>($"/api/v1/customers/{id}/balance");
        Assert.Equal(0, balance["balance"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task Product_crud_and_unique_sku()
    {
        using var app = new TestApp();
        var c = await app.SignedInClient();

        var p = await c.Post<JsonObject>("/api/v1/products", new { name = "طراحی سایت", kind = "Service", unit = "پروژه", defaultPrice = 25_000_000, sku = "WEB-1" });
        Assert.Equal("Service", p["kind"]!.GetValue<string>());

        var dup = await c.Post("/api/v1/products", new { name = "دیگر", unit = "عدد", sku = "WEB-1" });
        Assert.Equal("product.duplicate_sku", await ApiClient.Code(dup));

        var neg = await c.Post("/api/v1/products", new { name = "x", unit = "عدد", defaultPrice = -1 });
        Assert.Equal(HttpStatusCode.BadRequest, neg.StatusCode);

        var id = p["id"]!.GetValue<int>();
        var upd = await c.Put<JsonObject>($"/api/v1/products/{id}", new { name = "طراحی سایت", unit = "پروژه", defaultPrice = 30_000_000, isTaxable = false, sku = "WEB-1" });
        Assert.Equal(30_000_000m, upd["defaultPrice"]!.GetValue<decimal>());
        Assert.False(upd["isTaxable"]!.GetValue<bool>());

        (await c.Delete($"/api/v1/products/{id}")).EnsureSuccessStatusCode();
        Assert.Equal(0, (await c.Get<JsonObject>("/api/v1/products"))["total"]!.GetValue<int>());
    }
}
