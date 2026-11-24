using LocalInvoiceManager.Domain.Localization;
using LocalInvoiceManager.Application.Backup;
using LocalInvoiceManager.Infrastructure.Backup;
using LocalInvoiceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LocalInvoiceManager.Web.Infrastructure;

public static class Startup
{
    /// <summary>Backs up an existing database before applying new migrations, then migrates.</summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var applied = (await db.Database.GetAppliedMigrationsAsync()).Any();
        var pending = (await db.Database.GetPendingMigrationsAsync()).Any();
        if (applied && pending)
        {
            app.Logger.LogInformation("Database update found; creating a safety backup first.");
            await app.Services.GetRequiredService<IBackupService>().CreateAsync(BackupKind.PreMigration, CancellationToken.None);
        }
        await DatabaseInitializer.InitializeAsync(db, app.Logger);
    }

    /// <summary>
    /// Request language for messages: the UI sends exactly "en" or "fa" in Accept-Language. Anything else (a browser
    /// opening a PDF link sends e.g. "en-US,en;q=0.9") uses the app setting, so documents follow the setting.
    /// Also sets the UI culture so FluentValidation's built-in messages match.
    /// </summary>
    public static IApplicationBuilder UseRequestLanguage(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        var header = ctx.Request.Headers.AcceptLanguage.ToString().Trim();
        Loc.UseLanguage(header is "en" or "fa" ? header : Loc.DefaultLanguage);
        System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo(Loc.Language == "fa" ? "fa" : "en");
        await next();
    });

    /// <summary>Answers 503 to other API calls while a restore is swapping the data.</summary>
    public static IApplicationBuilder UseRestoreGate(this IApplicationBuilder app) => app.Use(async (ctx, next) =>
    {
        var gate = ctx.RequestServices.GetRequiredService<RestoreGate>();
        if (gate.IsRestoring && ctx.Request.Path.StartsWithSegments("/api"))
        {
            ctx.Response.StatusCode = 503;
            ctx.Response.Headers.RetryAfter = "5";
            await ctx.Response.WriteAsJsonAsync(new { status = 503, code = "restoring", detail = Loc.T("در حال بازیابی اطلاعات؛ چند ثانیه دیگر دوباره تلاش کنید.") });
            return;
        }
        await next();
    });
}
