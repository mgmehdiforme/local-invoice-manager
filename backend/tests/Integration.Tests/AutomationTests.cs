using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using LocalInvoiceManager.Application.Notifications;

namespace Integration.Tests;

public class AutomationTests
{
    [Fact]
    public async Task Backup_verify_and_restore_round_trip()
    {
        using var app = new TestApp();
        var c = await app.SignedInClient();
        var (cid, pid) = await InvoiceTests.SeedAsync(c);
        var d = await c.Post<JsonObject>("/api/v1/invoices", InvoiceTests.Draft(cid, pid));
        await c.Post<JsonObject>($"/api/v1/invoices/{d["id"]}/issue");

        var b = await c.Post<JsonObject>("/api/v1/backup");
        var name = b["fileName"]!.GetValue<string>();
        Assert.Matches(@"^invoice-backup-\d{4}-\d\d-\d\d-\d{6}-manual\.zip$", name);
        var verified = await c.Post<JsonObject>($"/api/v1/backup/{name}/verify");
        Assert.True(verified["isValid"]!.GetValue<bool>());
        Assert.Equal(1, verified["invoiceCount"]!.GetValue<int>());

        // Change data after the backup...
        await c.Post<JsonObject>("/api/v1/customers", new { name = "مشتری بعد از پشتیبان" });
        var d2 = await c.Post<JsonObject>("/api/v1/invoices", InvoiceTests.Draft(cid, pid));
        await c.Post<JsonObject>($"/api/v1/invoices/{d2["id"]}/issue");
        Assert.Equal(2, (await c.Get<JsonObject>("/api/v1/customers"))["total"]!.GetValue<int>());

        // ...download it, then restore by upload.
        var zip = await (await c.Get($"/api/v1/backup/{name}")).Content.ReadAsByteArrayAsync();
        var form = new MultipartFormDataContent { { new ByteArrayContent(zip) { Headers = { ContentType = new MediaTypeHeaderValue("application/zip") } }, "file", name } };
        var restored = await c.Post<JsonObject>("/api/v1/backup/restore", form);
        Assert.True(restored["isValid"]!.GetValue<bool>());

        Assert.Equal(1, (await c.Get<JsonObject>("/api/v1/customers"))["total"]!.GetValue<int>());
        Assert.Equal(1, (await c.Get<JsonObject>("/api/v1/invoices"))["total"]!.GetValue<int>());
        // Numbering continues from the restored state.
        var d3 = await c.Post<JsonObject>("/api/v1/invoices", InvoiceTests.Draft(cid, pid));
        Assert.Equal("INV-1405-0002", (await c.Post<JsonObject>($"/api/v1/invoices/{d3["id"]}/issue"))["invoiceNumber"]!.GetValue<string>());

        // A safety backup was taken before restoring.
        var list = await c.Get<JsonObject>("/api/v1/backup");
        Assert.Contains(list["items"]!.AsArray(), i => i!["kind"]!.GetValue<string>() == "PreRestore");
    }

    [Fact]
    public async Task Tampered_or_foreign_backups_are_rejected()
    {
        using var app = new TestApp();
        var c = await app.SignedInClient();
        var name = (await c.Post<JsonObject>("/api/v1/backup"))["fileName"]!.GetValue<string>();
        var zip = await (await c.Get($"/api/v1/backup/{name}")).Content.ReadAsByteArrayAsync();

        // Modify the settings file inside the archive: hash check must fail.
        using var ms = new MemoryStream();
        ms.Write(zip);
        using (var archive = new ZipArchive(ms, ZipArchiveMode.Update, leaveOpen: true))
        {
            var e = archive.GetEntry("settings.json")!;
            e.Delete();
            using var w = new StreamWriter(archive.CreateEntry("settings.json").Open());
            w.Write("{\"tampered\":true}");
        }
        var bad = new MultipartFormDataContent { { new ByteArrayContent(ms.ToArray()), "file", "x.zip" } };
        var res = await c.Post<JsonObject>("/api/v1/backup/verify", bad);
        Assert.False(res["isValid"]!.GetValue<bool>());

        var notZip = new MultipartFormDataContent { { new ByteArrayContent("hello"u8.ToArray()), "file", "x.zip" } };
        Assert.False((await c.Post<JsonObject>("/api/v1/backup/verify", notZip))["isValid"]!.GetValue<bool>());
        var restore = await c.Post("/api/v1/backup/restore", new MultipartFormDataContent { { new ByteArrayContent("hello"u8.ToArray()), "file", "x.zip" } });
        Assert.Equal("backup.invalid", await ApiClient.Code(restore));

        Assert.Equal("backup.name", await ApiClient.Code(await c.Post("/api/v1/backup/..%2F..%2Fetc%2Fpasswd/verify")));
    }

    [Fact]
    public async Task Retention_keeps_newest_backups()
    {
        using var app = new TestApp();
        var c = await app.SignedInClient();
        var s = await c.Get<JsonObject>("/api/v1/settings");
        s["backupRetentionCount"] = 2;
        await c.Put<JsonObject>("/api/v1/settings", s);
        for (var i = 0; i < 4; i++) { await c.Post<JsonObject>("/api/v1/backup"); await Task.Delay(1100); }
        Assert.Equal(2, (await c.Get<JsonObject>("/api/v1/backup"))["items"]!.AsArray().Count);
    }

    [Fact]
    public async Task Reminders_are_created_once_and_listed()
    {
        using var app = new TestApp();
        var c = await app.SignedInClient();
        var (cid, pid) = await InvoiceTests.SeedAsync(c);
        var d = await c.Post<JsonObject>("/api/v1/invoices", InvoiceTests.Draft(cid, pid, due: "2026-11-20"));
        await c.Post<JsonObject>($"/api/v1/invoices/{d["id"]}/issue");

        Assert.Equal(1, (await c.Post<JsonObject>("/api/v1/notifications/run-reminders"))["created"]!.GetValue<int>());
        Assert.Equal(0, (await c.Post<JsonObject>("/api/v1/notifications/run-reminders"))["created"]!.GetValue<int>()); // dedup
        Assert.Equal(1, (await c.Get<JsonObject>("/api/v1/notifications/count"))["unread"]!.GetValue<int>());
        var list = await c.Get<JsonArray>("/api/v1/notifications");
        Assert.Equal("Overdue", list[0]!["type"]!.GetValue<string>());
        await c.Post("/api/v1/notifications/read-all");
        Assert.Equal(0, (await c.Get<JsonObject>("/api/v1/notifications/count"))["unread"]!.GetValue<int>());
    }

    [Fact]
    public async Task Recurring_invoices_follow_jalali_months_and_catch_up()
    {
        using var app = new TestApp();
        var c = await app.SignedInClient();
        var (cid, pid) = await InvoiceTests.SeedAsync(c);
        var r = await c.Post<JsonObject>("/api/v1/recurring-invoices", new
        {
            name = "پشتیبانی ماهانه", customerId = cid, frequency = "Monthly", startDate = "2026-08-23", autoIssue = true, dueDays = 10,
            template = new { taxRate = 10, items = new[] { new { productId = pid, description = "پشتیبانی", unit = "ماه", quantity = 1, unitPrice = 1_000_000, discountType = "None", discountValue = 0, isTaxable = true } } },
        }); // 1 Shahrivar 1405
        Assert.Equal(1_100_000m, r["total"]!.GetValue<decimal>());

        var created = await c.Post<JsonObject>("/api/v1/recurring-invoices/run-due");
        // 1 Shahrivar, 1 Mehr, 1 Aban (all on/before today) — catch-up creates each one, then the schedule waits for 1 Azar.
        Assert.True(created["created"]!.AsArray().Count >= 3);
        var after = await c.Get<JsonObject>($"/api/v1/recurring-invoices/{r["id"]}");
        var next = after["nextRunDate"]!.GetValue<string>();
        Assert.EndsWith("-22", next); // 1st of a Jalali month falls on the 21st–23rd Gregorian
        var invoices = await c.Get<JsonObject>("/api/v1/invoices?status=Issued");
        Assert.Equal(created["created"]!.AsArray().Count, invoices["total"]!.GetValue<int>());
        Assert.Empty((await c.Post<JsonObject>("/api/v1/recurring-invoices/run-due"))["created"]!.AsArray());
    }

    [Fact]
    public async Task Email_requires_smtp_and_share_builds_whatsapp_link()
    {
        using var app = new TestApp();
        var c = await app.SignedInClient();
        var (cid, pid) = await InvoiceTests.SeedAsync(c);
        var d = await c.Post<JsonObject>("/api/v1/invoices", InvoiceTests.Draft(cid, pid));
        await c.Post<JsonObject>($"/api/v1/invoices/{d["id"]}/issue");

        Assert.Equal("email.not_configured", await ApiClient.Code(await c.Post($"/api/v1/invoices/{d["id"]}/email", new { to = "a@b.com" })));
        var share = await c.Get<JsonObject>($"/api/v1/invoices/{d["id"]}/share");
        Assert.Equal("989121234567", share["phone"]!.GetValue<string>());
        Assert.StartsWith("https://wa.me/989121234567?text=", share["whatsappUrl"]!.GetValue<string>());
        Assert.Contains("INV-1405-0001", share["text"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("09121234567", "989121234567")]
    [InlineData("+98 912 123 4567", "989121234567")]
    [InlineData("۰۹۱۲۱۲۳۴۵۶۷", "989121234567")]
    [InlineData("00989121234567", "989121234567")]
    [InlineData("021-88776655", null)]
    public void Mobile_normalization(string raw, string? expected) => Assert.Equal(expected, NotificationService.NormalizeMobile(raw));
}
