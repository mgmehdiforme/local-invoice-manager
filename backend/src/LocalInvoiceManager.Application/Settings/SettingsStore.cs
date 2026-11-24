using LocalInvoiceManager.Application.Abstractions;
using LocalInvoiceManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LocalInvoiceManager.Application.Settings;

public static class SettingsStore
{
    /// <summary>Loads the settings row, creating it with defaults on first run.</summary>
    public static async Task<AppSettings> GetAsync(this IAppDbContext db, CancellationToken ct = default)
    {
        var s = await db.AppSettings.FirstOrDefaultAsync(x => x.Id == AppSettings.SingletonId, ct);
        if (s is not null) return s;
        s = new AppSettings { UpdatedAtUtc = DateTime.UtcNow };
        db.AppSettings.Add(s);
        await db.SaveChangesAsync(ct);
        return s;
    }
}
