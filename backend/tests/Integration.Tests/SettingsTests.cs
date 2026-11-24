using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Net.Http.Json;

namespace Integration.Tests;

public class SettingsTests
{
    [Fact]
    public async Task Update_settings_and_validate()
    {
        using var app = new TestApp();
        var c = await app.SignedInClient();
        var s = await c.Get<JsonObject>("/api/v1/settings");
        Assert.Equal("IRR", s["currencyCode"]!.GetValue<string>());
        Assert.StartsWith("INV-14", s["nextInvoiceNumberPreview"]!.GetValue<string>());

        s["companyName"] = "فروشگاه نمونه";
        s["bankIban"] = "IR 1234 5678 9012 3456 7890 1234";
        s["smtpPassword"] = "secret";
        var saved = await c.Put<JsonObject>("/api/v1/settings", s);
        Assert.Equal("فروشگاه نمونه", saved["companyName"]!.GetValue<string>());
        Assert.Equal("IR123456789012345678901234", saved["bankIban"]!.GetValue<string>());
        Assert.True(saved["smtpPasswordSet"]!.GetValue<bool>());
        Assert.Null(saved["smtpPassword"]?.GetValue<string>()); // never returned

        s["defaultTaxRate"] = 150;
        s["accentColor"] = "red";
        var bad = await c.Put("/api/v1/settings", s);
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var errors = (await bad.Content.ReadFromJsonAsync<JsonObject>())!["errors"]!.AsObject();
        Assert.True(errors.ContainsKey("defaultTaxRate"));
        Assert.True(errors.ContainsKey("accentColor"));
    }

    [Fact]
    public async Task Logo_upload_checks_real_file_type()
    {
        using var app = new TestApp();
        var c = await app.SignedInClient();

        var fake = new MultipartFormDataContent { { new ByteArrayContent("<script>"u8.ToArray()) { Headers = { ContentType = new MediaTypeHeaderValue("image/png") } }, "file", "logo.png" } };
        Assert.Equal("logo.type", await ApiClient.Code(await c.Post("/api/v1/settings/logo", fake)));

        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 0];
        var ok = new MultipartFormDataContent { { new ByteArrayContent(png), "file", "../../etc/passwd" } };
        (await c.Post("/api/v1/settings/logo", ok)).EnsureSuccessStatusCode();
        var logo = await c.Get("/api/v1/settings/logo");
        Assert.Equal("image/png", logo.Content.Headers.ContentType!.MediaType);
        Assert.True((await c.Get<JsonObject>("/api/v1/settings"))["hasLogo"]!.GetValue<bool>());

        (await c.Delete("/api/v1/settings/logo")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await c.Get("/api/v1/settings/logo")).StatusCode);
    }
}
