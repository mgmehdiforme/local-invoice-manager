using LocalInvoiceManager.Application.Settings;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LocalInvoiceManager.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>Applies migrations, enables WAL and makes sure the settings row exists.</summary>
    public static async Task InitializeAsync(AppDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var pending = (await db.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pending.Count > 0)
        {
            logger.LogInformation("Applying {Count} database migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
            await db.Database.MigrateAsync(ct);
        }
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode = WAL;", ct);
        SettingsService.ApplyDefaults(await db.GetAsync(ct));
    }

    public static string? GetDatabasePath(AppDbContext db) =>
        new SqliteConnectionStringBuilder(db.Database.GetConnectionString()).DataSource;
}
