using LocalInvoiceManager.Domain.Localization;
using LocalInvoiceManager.Application.Abstractions;
using LocalInvoiceManager.Application.Backup;
using LocalInvoiceManager.Application.Common;
using LocalInvoiceManager.Application.Notifications;
using LocalInvoiceManager.Application.Recurring;
using LocalInvoiceManager.Domain;

namespace LocalInvoiceManager.Web.Endpoints;

public static class AutomationEndpoints
{
    public sealed record ToRequest(string To);
    public sealed record FileNameRequest(string FileName);

    public static void MapAutomation(this RouteGroupBuilder api)
    {
        // Notifications
        var n = api.MapGroup("/notifications").WithTags("Notifications");
        n.MapGet("/", (bool? unreadOnly, NotificationService s, CancellationToken ct) => s.ListAsync(unreadOnly == true, ct));
        n.MapGet("/count", async (NotificationService s, CancellationToken ct) => new { unread = await s.UnreadCountAsync(ct) });
        n.MapPost("/{id:int}/read", async (int id, NotificationService s, CancellationToken ct) => { await s.MarkReadAsync(id, ct); return Results.NoContent(); });
        n.MapPost("/read-all", async (NotificationService s, CancellationToken ct) => { await s.MarkReadAsync(null, ct); return Results.NoContent(); });
        n.MapPost("/run-reminders", async (NotificationService s, CancellationToken ct) => new { created = await s.RunRemindersAsync(ct) });

        // Email & sharing
        api.MapPost("/invoices/{id:int}/email", (int id, EmailInvoiceRequest req, NotificationService s, CancellationToken ct) => s.EmailInvoiceAsync(id, req, ct)).WithTags("Invoices");
        api.MapGet("/invoices/{id:int}/share", (int id, NotificationService s, CancellationToken ct) => s.ShareAsync(id, ct)).WithTags("Invoices");
        api.MapPost("/settings/test-email", async (ToRequest req, NotificationService s, CancellationToken ct) => { await s.SendTestEmailAsync(req.To, ct); return Results.NoContent(); }).WithTags("Settings");

        // Recurring invoices
        var r = api.MapGroup("/recurring-invoices").WithTags("Recurring");
        r.MapGet("/", (RecurringInvoiceService s, CancellationToken ct) => s.ListAsync(ct));
        r.MapGet("/{id:int}", (int id, RecurringInvoiceService s, CancellationToken ct) => s.GetAsync(id, ct));
        r.MapPost("/", (RecurringInput input, RecurringInvoiceService s, CancellationToken ct) => s.CreateAsync(input, ct));
        r.MapPut("/{id:int}", (int id, RecurringInput input, RecurringInvoiceService s, CancellationToken ct) => s.UpdateAsync(id, input, ct));
        r.MapDelete("/{id:int}", async (int id, RecurringInvoiceService s, CancellationToken ct) => { await s.DeleteAsync(id, ct); return Results.NoContent(); });
        r.MapPost("/run-due", async (RecurringInvoiceService s, CancellationToken ct) => new { created = await s.RunDueAsync(ct) });
        r.MapPost("/{id:int}/run-now", async (int id, RecurringInvoiceService s, CancellationToken ct) => new { invoiceId = await s.RunNowAsync(id, ct) });

        // Backup
        var b = api.MapGroup("/backup").WithTags("Backup");
        b.MapGet("/", async (IBackupService s, CancellationToken ct) => new { items = await s.ListAsync(ct), lastScheduled = s.LastScheduledDate() });
        b.MapPost("/", async (IBackupService s, IAppDbContext db, AuditLogger audit, CancellationToken ct) =>
        {
            var info = await s.CreateAsync(BackupKind.Manual, ct);
            var settings = await LocalInvoiceManager.Application.Settings.SettingsStore.GetAsync(db, ct);
            await s.ApplyRetentionAsync(settings.BackupRetentionCount, ct);
            return info;
        });
        b.MapGet("/{fileName}", (string fileName, IBackupService s) =>
            s.OpenRead(fileName) is { } stream ? Results.File(stream, "application/zip", fileName) : Results.NotFound());
        b.MapDelete("/{fileName}", (string fileName, IBackupService s) => { s.Delete(fileName); return Results.NoContent(); });
        b.MapPost("/{fileName}/verify", (string fileName, IBackupService s, CancellationToken ct) => s.VerifyStoredAsync(fileName, ct));
        b.MapPost("/verify", async (IFormFile file, IBackupService s, CancellationToken ct) =>
            await WithUploadAsync(file, path => s.VerifyAsync(path, ct))).DisableAntiforgery();
        b.MapPost("/restore", async (IFormFile file, IBackupService s, IAppDbContext db, AuditLogger audit, CancellationToken ct) =>
            await AfterRestoreAsync(await WithUploadAsync(file, path => s.RestoreAsync(path, ct)), file.FileName, db, audit, ct)).DisableAntiforgery();
        b.MapPost("/restore-stored", async (FileNameRequest req, IBackupService s, IAppDbContext db, AuditLogger audit, CancellationToken ct) =>
            await AfterRestoreAsync(await s.RestoreStoredAsync(req.FileName, ct), req.FileName, db, audit, ct));
    }

    private static async Task<BackupVerifyResult> AfterRestoreAsync(BackupVerifyResult result, string name, IAppDbContext db, AuditLogger audit, CancellationToken ct)
    {
        if (!result.IsValid) throw new DomainException("backup.invalid", Loc.T("فایل پشتیبان معتبر نیست: {0}", string.Join(" ", result.Problems)));
        audit.Log("Backup", Path.GetFileName(name), "Restored");
        await db.SaveChangesAsync(ct);
        return result;
    }

    private static async Task<T> WithUploadAsync<T>(IFormFile file, Func<string, Task<T>> action)
    {
        if (file.Length == 0) throw new DomainException("backup.empty", "فایل خالی است.");
        var tmp = Path.Combine(Path.GetTempPath(), $"lim-upload-{Guid.NewGuid():N}.zip");
        try
        {
            await using (var fs = File.Create(tmp)) await file.CopyToAsync(fs);
            return await action(tmp);
        }
        finally { try { File.Delete(tmp); } catch { /* temp */ } }
    }
}
