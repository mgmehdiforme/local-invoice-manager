using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.Testing.Handlers;

namespace Integration.Tests;

/// <summary>Runs the real app against a temp folder (own SQLite DB, backups) with background jobs off.</summary>
public sealed class TestApp : WebApplicationFactory<Program>
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "lim-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Storage:DataPath", Path.Combine(Root, "data"));
        builder.UseSetting("Storage:BackupPath", Path.Combine(Root, "backups"));
        builder.UseSetting("Security:SecureCookies", "false");
        builder.UseSetting("Jobs:Enabled", "false");
    }

    public ApiClient Client()
    {
        var cookies = new CookieContainer();
        var http = CreateDefaultClient(new Uri("http://localhost"), new CookieContainerHandler(cookies));
        return new ApiClient(http, cookies);
    }

    /// <summary>
    /// A client that has completed first-run setup and is signed in. Most tests were written for the Persian setup
    /// (Jalali calendar, Rial), so that is the default; pass language "en" and a currency for a global setup.
    /// </summary>
    public async Task<ApiClient> SignedInClient(string password = "Secret123!", string language = "fa", string currencyCode = "IRR")
    {
        var c = Client();
        await c.Get("/api/v1/auth/csrf");
        var status = await c.Get<JsonElement>("/api/v1/auth/status");
        var setUp = status.GetProperty("isSetUp").GetBoolean();
        (await (setUp
            ? c.Post("/api/v1/auth/login", new { password })
            : c.Post("/api/v1/auth/setup", new { password, language, currencyCode }))).EnsureSuccessStatusCode();
        return c;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(Root, true); } catch { /* temp */ }
    }
}

public sealed class ApiClient(HttpClient http, CookieContainer cookies)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    public HttpClient Http => http;

    public string? Xsrf => cookies.GetCookies(new Uri("http://localhost"))["XSRF-TOKEN"]?.Value;

    private HttpRequestMessage Req(HttpMethod m, string url, object? body, bool xsrf)
    {
        var r = new HttpRequestMessage(m, url);
        if (body is HttpContent hc) r.Content = hc;
        else if (body is not null) r.Content = JsonContent.Create(body, options: Json);
        if (xsrf && Xsrf is { } t) r.Headers.Add("X-XSRF-TOKEN", t);
        return r;
    }

    public Task<HttpResponseMessage> Get(string url) => http.SendAsync(Req(HttpMethod.Get, url, null, false));
    public async Task<T> Get<T>(string url)
    {
        var res = await Get(url);
        await EnsureOk(res);
        return (await res.Content.ReadFromJsonAsync<T>(Json))!;
    }
    public Task<HttpResponseMessage> Post(string url, object? body = null, bool xsrf = true) => http.SendAsync(Req(HttpMethod.Post, url, body ?? new { }, xsrf));
    public Task<HttpResponseMessage> Put(string url, object body) => http.SendAsync(Req(HttpMethod.Put, url, body, true));
    public Task<HttpResponseMessage> Delete(string url) => http.SendAsync(Req(HttpMethod.Delete, url, null, true));

    public async Task<T> Post<T>(string url, object? body = null)
    {
        var res = await Post(url, body);
        await EnsureOk(res);
        return (await res.Content.ReadFromJsonAsync<T>(Json))!;
    }
    public async Task<T> Put<T>(string url, object body)
    {
        var res = await Put(url, body);
        await EnsureOk(res);
        return (await res.Content.ReadFromJsonAsync<T>(Json))!;
    }

    public static async Task EnsureOk(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
            throw new HttpRequestException($"{(int)res.StatusCode}: {await res.Content.ReadAsStringAsync()}");
    }

    public static async Task<string> Code(HttpResponseMessage res) =>
        (await res.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString()!;
}
