using System.Text.Json.Serialization;
using Microsoft.AspNetCore.RateLimiting;
using LocalInvoiceManager.Application;
using LocalInvoiceManager.Application.Auth;
using LocalInvoiceManager.Infrastructure;
using LocalInvoiceManager.Infrastructure.Persistence;
using LocalInvoiceManager.Web.Endpoints;
using LocalInvoiceManager.Web.Infrastructure;
using LocalInvoiceManager.Web.Jobs;
using Microsoft.AspNetCore.Http.Features;

// Docker health check: `dotnet LocalInvoiceManager.Web.dll healthcheck`
if (args.Length > 0 && args[0] == "healthcheck")
{
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    try { return (await http.GetAsync("http://localhost:8080/health")).IsSuccessStatusCode ? 0 : 1; }
    catch { return 1; }
}

// Native (non-Docker) runs: `--no-browser` skips opening the browser. Strip it before the config parser sees it.
var openBrowser = !args.Contains("--no-browser") && Environment.GetEnvironmentVariable("LIM_NO_BROWSER") is null;
args = args.Where(a => a != "--no-browser").ToArray();
var isCommand = args.Length > 0 && !args[0].StartsWith('-');

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = NativeHost.ContentRoot() });
var nativeMode = NativeHost.Configure(builder);

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    o.SerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ErrorHandler>();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddLocalSecurity(builder.Configuration);
builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = 200L * 1024 * 1024);
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 200L * 1024 * 1024);
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddFixedWindowLimiter("login", l => { l.PermitLimit = 10; l.Window = TimeSpan.FromMinutes(1); l.QueueLimit = 0; });
});
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>();
builder.AddJobs();

var app = builder.Build();

// Command line maintenance: `dotnet LocalInvoiceManager.Web.dll reset-password`
if (args.Contains("reset-password"))
{
    using var scope = app.Services.CreateScope();
    await DatabaseInitializer.InitializeAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), app.Logger);
    await scope.ServiceProvider.GetRequiredService<AuthService>().ResetPasswordAsync(CancellationToken.None);
    Console.WriteLine("Password cleared. Open the app to set a new password. Your data is unchanged.");
    return 0;
}

// Command line backup tools (used by scripts/backup.sh and scripts/restore.sh):
//   backup | verify <zip> | restore <zip> | seed-demo
if (args.Length > 0 && args[0] is "backup" or "verify" or "restore" or "seed-demo")
    return await Cli.RunAsync(app, args);

await app.InitializeDatabaseAsync();

app.UseRequestLanguage();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseSecurityHeaders();
app.UseRestoreGate();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseXsrfValidation();

app.MapHealthChecks("/health").AllowAnonymous();

var api = app.MapGroup("/api/v1").RequireAuthorization();
app.MapApi(api);

app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

if (nativeMode && openBrowser && !isCommand) NativeHost.OpenBrowserWhenReady(app);

await app.RunAsync();
return 0;

public partial class Program;
