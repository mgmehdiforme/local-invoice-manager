using System.Net;
using System.Text.Json;

namespace Integration.Tests;

public class AuthTests
{
    [Fact]
    public async Task First_run_setup_then_login_logout()
    {
        using var app = new TestApp();
        var c = app.Client();

        var status = await c.Get<JsonElement>("/api/v1/auth/status");
        Assert.False(status.GetProperty("isSetUp").GetBoolean());
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.Get("/api/v1/settings")).StatusCode);

        await c.Get("/api/v1/auth/csrf");
        var weak = await c.Post("/api/v1/auth/setup", new { password = "short" });
        Assert.Equal("auth.weak_password", await ApiClient.Code(weak));

        Assert.Equal(HttpStatusCode.NoContent, (await c.Post("/api/v1/auth/setup", new { password = "Secret123!" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await c.Get("/api/v1/settings")).StatusCode);

        // Setup cannot run twice (nobody can take over an installed app).
        Assert.Equal("auth.already_set_up", await ApiClient.Code(await c.Post("/api/v1/auth/setup", new { password = "Other1234!" })));

        await c.Post("/api/v1/auth/logout");
        Assert.Equal(HttpStatusCode.Unauthorized, (await c.Get("/api/v1/settings")).StatusCode);

        var c2 = await app.SignedInClient();
        Assert.Equal(HttpStatusCode.OK, (await c2.Get("/api/v1/settings")).StatusCode);
    }

    [Fact]
    public async Task State_changing_requests_need_xsrf_header()
    {
        using var app = new TestApp();
        var c = await app.SignedInClient();
        var res = await c.Post("/api/v1/auth/logout", xsrf: false);
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("xsrf", await ApiClient.Code(res));
    }

    [Fact]
    public async Task Five_wrong_passwords_lock_login()
    {
        using var app = new TestApp();
        (await app.SignedInClient()).Http.Dispose();
        var c = app.Client();
        await c.Get("/api/v1/auth/csrf");
        for (var i = 0; i < 5; i++)
            Assert.Equal("auth.invalid_password", await ApiClient.Code(await c.Post("/api/v1/auth/login", new { password = "wrong-password" })));
        var locked = await c.Post("/api/v1/auth/login", new { password = "Secret123!" });
        Assert.Equal((HttpStatusCode)429, locked.StatusCode);
    }

    [Fact]
    public async Task Changing_password_ends_other_sessions()
    {
        using var app = new TestApp();
        var a = await app.SignedInClient();
        var b = await app.SignedInClient();

        var wrong = await a.Post("/api/v1/auth/change-password", new { currentPassword = "nope-nope", newPassword = "NewSecret123!" });
        Assert.Equal("auth.invalid_password", await ApiClient.Code(wrong));

        (await a.Post("/api/v1/auth/change-password", new { currentPassword = "Secret123!", newPassword = "NewSecret123!" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.OK, (await a.Get("/api/v1/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await b.Get("/api/v1/settings")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await app.SignedInClient("NewSecret123!")).Get("/api/v1/settings")).StatusCode);
    }

    [Fact]
    public async Task Security_headers_and_health()
    {
        using var app = new TestApp();
        var c = app.Client();
        var res = await c.Get("/health");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("nosniff", res.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("default-src 'self'", res.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task Unknown_host_is_rejected()
    {
        using var app = new TestApp();
        var c = app.Client();
        var req = new HttpRequestMessage(HttpMethod.Get, "/health");
        req.Headers.Host = "evil.example.com";
        Assert.Equal(HttpStatusCode.BadRequest, (await c.Http.SendAsync(req)).StatusCode);
    }
}
