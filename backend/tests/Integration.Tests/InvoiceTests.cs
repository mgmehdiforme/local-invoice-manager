using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace Integration.Tests;

public class InvoiceTests
{
    internal static async Task<(int customerId, int productId)> SeedAsync(ApiClient c)
    {
        var cust = await c.Post<JsonObject>("/api/v1/customers", new { name = "علی رضایی", mobile = "09121234567", address = "تهران" });
        var prod = await c.Post<JsonObject>("/api/v1/products", new { name = "طراحی لوگو", unit = "پروژه", defaultPrice = 1_000_000 });
        return (cust["id"]!.GetValue<int>(), prod["id"]!.GetValue<int>());
    }

    internal static object Draft(int customerId, int? productId = null, decimal price = 1_000_000, decimal qty = 2, string? due = "2099-01-01") => new
    {
        customerId,
        issueDate = "2026-11-15",
        dueDate = due,
        discountType = "Percent",
        discountValue = 10,
        taxRate = 10,
        notes = "با تشکر",
        items = new object[]
        {
            new { productId, description = "طراحی لوگو", unit = "پروژه", quantity = qty, unitPrice = price, discountType = "None", discountValue = 0, isTaxable = true },
            new { description = "Custom line — English & فارسی", unit = "ساعت", quantity = 1.5, unitPrice = 200_000, discountType = "Fixed", discountValue = 50_000, isTaxable = false },
        },
    };

    [Fact]
    public async Task Draft_issue_cancel_lifecycle()
    {
        using var app = new TestApp();
        var c = await app.SignedInClient();
        var (cid, pid) = await SeedAsync(c);

        var draft = await c.Post<JsonObject>("/api/v1/invoices", Draft(cid, pid));
        var id = draft["id"]!.GetValue<int>();
        Assert.Equal("Draft", draft["status"]!.GetValue<string>());
        Assert.Null(draft["invoiceNumber"]);
        // 2×1,000,000 + (1.5×200,000 − 50,000) = 2,250,000; −10% = 225,000; tax base: taxable 2,000,000 − 200,000 share = 1,800,000; tax 180,000
        Assert.Equal(2_250_000m, draft["subtotal"]!.GetValue<decimal>());
        Assert.Equal(225_000m, draft["discountAmount"]!.GetValue<decimal>());
        Assert.Equal(180_000m, draft["taxAmount"]!.GetValue<decimal>());
        Assert.Equal(2_205_000m, draft["total"]!.GetValue<decimal>());

        // Live calculation endpoint returns the same totals.
        var calc = await c.Post<JsonObject>("/api/v1/invoices/calculate", Draft(cid, pid));
        Assert.Equal(2_205_000m, calc["total"]!.GetValue<decimal>());

        // Drafts do not consume numbers.
        var issued = await c.Post<JsonObject>($"/api/v1/invoices/{id}/issue");
        Assert.Equal("Issued", issued["status"]!.GetValue<string>());
        Assert.Equal("INV-1405-0001", issued["invoiceNumber"]!.GetValue<string>());

        // Issued invoices are read-only.
        Assert.Equal("invoice.not_draft", await ApiClient.Code(await c.Put($"/api/v1/invoices/{id}", Draft(cid, pid))));
        Assert.Equal("invoice.not_draft", await ApiClient.Code(await c.Delete($"/api/v1/invoices/{id}")));
        Assert.Equal("invoice.not_draft", await ApiClient.Code(await c.Post($"/api/v1/invoices/{id}/issue")));

        Assert.Equal("invoice.cancel_reason", await ApiClient.Code(await c.Post($"/api/v1/invoices/{id}/cancel", new { reason = "" })));
        var cancelled = await c.Post<JsonObject>($"/api/v1/invoices/{id}/cancel", new { reason = "صدور اشتباه" });
        Assert.Equal("Cancelled", cancelled["status"]!.GetValue<string>());
        Assert.Equal("INV-1405-0001", cancelled["invoiceNumber"]!.GetValue<string>());

        // The cancelled number is never reused.
        var d2 = await c.Post<JsonObject>("/api/v1/invoices", Draft(cid, pid));
        var i2 = await c.Post<JsonObject>($"/api/v1/invoices/{d2["id"]}/issue");
        Assert.Equal("INV-1405-0002", i2["invoiceNumber"]!.GetValue<string>());
    }

    [Fact]
    public async Task Draft_can_be_edited_and_deleted()
    {
        using var app = new TestApp();
        var c = await app.SignedInClient();
        var (cid, _) = await SeedAsync(c);
        var d = await c.Post<JsonObject>("/api/v1/invoices", Draft(cid));
        var id = d["id"]!.GetValue<int>();
        var upd = await c.Put<JsonObject>($"/api/v1/invoices/{id}", Draft(cid, price: 500_000, qty: 1));
        Assert.Equal(2, upd["items"]!.AsArray().Count);
        Assert.Equal(500_000m, upd["items"]![0]!["unitPrice"]!.GetValue<decimal>());
        (await c.Delete($"/api/v1/invoices/{id}")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await c.Get($"/api/v1/invoices/{id}")).StatusCode);
    }

    [Fact]
    public async Task Validation_errors_are_reported_per_field()
    {
        using var app = new TestApp();
        var c = await app.SignedInClient();
        var (cid, _) = await SeedAsync(c);
        var res = await c.Post("/api/v1/invoices", new
        {
            customerId = cid, issueDate = "2026-11-15", dueDate = "2026-11-01", discountType = "Percent", discountValue = 120, taxRate = 10,
            items = new[] { new { description = "", unit = "عدد", quantity = 0, unitPrice = -5, discountType = "None", discountValue = 0, isTaxable = true } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        var errors = (await res.Content.ReadFromJsonAsync<JsonObject>())!["errors"]!.AsObject();
        Assert.True(errors.ContainsKey("dueDate"));
        Assert.True(errors.ContainsKey("discountValue"));
        Assert.True(errors.ContainsKey("items[0].description"));
        Assert.True(errors.ContainsKey("items[0].quantity"));
        Assert.True(errors.ContainsKey("items[0].unitPrice"));
    }

    [Fact]
    public async Task Issue_requires_items_and_positive_total()
    {
        using var app = new TestApp();
        var c = await app.SignedInClient();
        var (cid, _) = await SeedAsync(c);
        var empty = await c.Post<JsonObject>("/api/v1/invoices", new { customerId = cid, issueDate = "2026-11-15", taxRate = 0, items = Array.Empty<object>() });
        Assert.Equal("invoice.no_items", await ApiClient.Code(await c.Post($"/api/v1/invoices/{empty["id"]}/issue")));
    }

    [Fact]
    public async Task Concurrent_issues_get_unique_sequential_numbers()
    {
        using var app = new TestApp();
        var c = await app.SignedInClient();
        var (cid, pid) = await SeedAsync(c);
        var ids = new List<int>();
        for (var i = 0; i < 12; i++)
            ids.Add((await c.Post<JsonObject>("/api/v1/invoices", Draft(cid, pid)))["id"]!.GetValue<int>());

        var results = await Task.WhenAll(ids.Select(id => c.Post($"/api/v1/invoices/{id}/issue")));
        Assert.All(results, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        var numbers = new List<string>();
        foreach (var r in results) numbers.Add((await r.Content.ReadFromJsonAsync<JsonObject>())!["invoiceNumber"]!.GetValue<string>());
        Assert.Equal(12, numbers.Distinct().Count());
        Assert.Equal(Enumerable.Range(1, 12).Select(n => $"INV-1405-{n:0000}"), numbers.Order());
    }

    [Fact]
    public async Task Issued_invoice_keeps_snapshots_after_master_data_changes()
    {
        using var app = new TestApp();
        var c = await app.SignedInClient();
        var (cid, pid) = await SeedAsync(c);
        var s = await c.Get<JsonObject>("/api/v1/settings");
        s["companyName"] = "فروشنده اول";
        await c.Put<JsonObject>("/api/v1/settings", s);

        var d = await c.Post<JsonObject>("/api/v1/invoices", Draft(cid, pid));
        var id = d["id"]!.GetValue<int>();
        await c.Post<JsonObject>($"/api/v1/invoices/{id}/issue");

        await c.Put<JsonObject>($"/api/v1/customers/{cid}", new { name = "نام جدید مشتری", mobile = "09000000000" });
        await c.Put<JsonObject>($"/api/v1/products/{pid}", new { name = "نام جدید کالا", unit = "عدد", defaultPrice = 9 });
        (await c.Delete($"/api/v1/products/{pid}")).EnsureSuccessStatusCode(); // archived product
        s["companyName"] = "فروشنده دوم";
        s["currencySymbol"] = "تومان";
        await c.Put<JsonObject>("/api/v1/settings", s);

        var inv = await c.Get<JsonObject>($"/api/v1/invoices/{id}");
        Assert.Equal("علی رضایی", inv["customer"]!["name"]!.GetValue<string>());
        Assert.Equal("09121234567", inv["customer"]!["mobile"]!.GetValue<string>());
        Assert.Equal("طراحی لوگو", inv["items"]![0]!["description"]!.GetValue<string>());
        Assert.Equal(1_000_000m, inv["items"]![0]!["unitPrice"]!.GetValue<decimal>());
        Assert.Equal("فروشنده اول", inv["seller"]!["companyName"]!.GetValue<string>());
        Assert.Equal("ریال", inv["currency"]!["currencySymbol"]!.GetValue<string>());
        Assert.Equal(2_205_000m, inv["total"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task Duplicate_creates_new_draft()
    {
        using var app = new TestApp();
        var c = await app.SignedInClient();
        var (cid, pid) = await SeedAsync(c);
        var d = await c.Post<JsonObject>("/api/v1/invoices", Draft(cid, pid));
        var id = d["id"]!.GetValue<int>();
        await c.Post<JsonObject>($"/api/v1/invoices/{id}/issue");
        var copy = await c.Post<JsonObject>($"/api/v1/invoices/{id}/duplicate");
        Assert.Equal("Draft", copy["status"]!.GetValue<string>());
        Assert.Null(copy["invoiceNumber"]);
        Assert.Equal(id, copy["duplicatedFromId"]!.GetValue<int>());
        Assert.Equal(d["total"]!.GetValue<decimal>(), copy["total"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task List_filters_and_customer_history()
    {
        using var app = new TestApp();
        var c = await app.SignedInClient();
        var (cid, pid) = await SeedAsync(c);
        var d1 = await c.Post<JsonObject>("/api/v1/invoices", Draft(cid, pid, due: "2026-11-20"));
        await c.Post<JsonObject>($"/api/v1/invoices/{d1["id"]}/issue");
        await c.Post<JsonObject>("/api/v1/invoices", Draft(cid, pid));

        Assert.Equal(2, (await c.Get<JsonObject>("/api/v1/invoices"))["total"]!.GetValue<int>());
        Assert.Equal(1, (await c.Get<JsonObject>("/api/v1/invoices?status=Draft"))["total"]!.GetValue<int>());
        Assert.Equal(1, (await c.Get<JsonObject>("/api/v1/invoices?overdue=true"))["total"]!.GetValue<int>()); // due 2026-11-20 is in the past
        Assert.Equal(1, (await c.Get<JsonObject>("/api/v1/invoices?search=0001"))["total"]!.GetValue<int>());
        Assert.Equal(2, (await c.Get<JsonObject>($"/api/v1/customers/{cid}/invoices"))["total"]!.GetValue<int>());

        var bal = await c.Get<JsonObject>($"/api/v1/customers/{cid}/balance");
        Assert.Equal(2_205_000m, bal["balance"]!.GetValue<decimal>());
        Assert.Equal(2_205_000m, bal["overdueAmount"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task Pdf_renders_for_all_templates_including_long_multi_page()
    {
        using var app = new TestApp();
        var c = await app.SignedInClient();
        var (cid, pid) = await SeedAsync(c);
        var items = Enumerable.Range(1, 45).Select(i => (object)new
        {
            description = $"ردیف {i}: طراحی و پیاده‌سازی بخش «{i}» با توضیحات طولانی Mixed English text, numbers 12345 و علائم (٪، ؟، «»)",
            sku = $"SKU-{i}", unit = "ساعت", quantity = 1.25m, unitPrice = 123_456m, discountType = "None", discountValue = 0, isTaxable = i % 3 != 0,
        }).ToArray();
        var d = await c.Post<JsonObject>("/api/v1/invoices", new { customerId = cid, issueDate = "2026-11-15", dueDate = "2026-12-15", taxRate = 10, notes = "یادداشت آزمایشی", paymentTerms = "پرداخت ظرف ۳۰ روز", items });
        var id = d["id"]!.GetValue<int>();
        await c.Post<JsonObject>($"/api/v1/invoices/{id}/issue");

        foreach (var t in new[] { "modern", "classic", "minimal" })
        {
            var res = await c.Get($"/api/v1/invoices/{id}/pdf?template={t}");
            Assert.Equal("application/pdf", res.Content.Headers.ContentType!.MediaType);
            var bytes = await res.Content.ReadAsByteArrayAsync();
            Assert.True(bytes.Length > 10_000);
            Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
            var text = System.Text.Encoding.Latin1.GetString(bytes);
            Assert.True(System.Text.RegularExpressions.Regex.Matches(text, @"/Type\s*/Page[^s]").Count >= 2, "expected multiple pages");
            Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "lim-pdf"));
            await File.WriteAllBytesAsync(Path.Combine(Path.GetTempPath(), "lim-pdf", $"invoice-{t}.pdf"), bytes);
        }
        var dl = await c.Get($"/api/v1/invoices/{id}/pdf?download=true");
        Assert.Contains("attachment", dl.Content.Headers.ContentDisposition!.ToString());
        Assert.Contains("INV-1405-0001.pdf", dl.Content.Headers.ContentDisposition!.ToString());
    }
}
