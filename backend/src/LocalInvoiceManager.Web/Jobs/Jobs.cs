using LocalInvoiceManager.Domain.Localization;
using LocalInvoiceManager.Application.Abstractions;
using LocalInvoiceManager.Application.Backup;
using LocalInvoiceManager.Application.Notifications;
using LocalInvoiceManager.Application.Recurring;
using LocalInvoiceManager.Application.Settings;
using LocalInvoiceManager.Domain;
using LocalInvoiceManager.Domain.Calendar;

namespace LocalInvoiceManager.Web.Jobs;

public static class Jobs
{
    /// <summary>Background jobs (backup, reminders, recurring invoices). Disabled with Jobs:Enabled=false (tests).</summary>
    public static WebApplicationBuilder AddJobs(this WebApplicationBuilder builder)
    {
        if (builder.Configuration.GetValue("Jobs:Enabled", true))
            builder.Services.AddHostedService<DailyJobs>();
        return builder;
    }
}

/// <summary>
/// Checks every 5 minutes (and right after start-up, so a computer that was off catches up):
/// recurring invoices due today, the daily reminder run (after 08:00), and the daily backup (after the configured hour).
/// </summary>
public sealed class DailyJobs(IServiceScopeFactory scopes, IBusinessClock clock, IBackupService backups, ILogger<DailyJobs> logger) : BackgroundService
{
    private DateOnly? _remindersDoneFor;

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        await Task.Delay(TimeSpan.FromSeconds(20), stop);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do
        {
            await RunOnceAsync(stop);
        } while (await timer.WaitForNextTickAsync(stop));
    }

    public async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<IAppDbContext>();
        var today = clock.Today;
        try
        {
            var created = await sp.GetRequiredService<RecurringInvoiceService>().RunDueAsync(ct);
            if (created.Count > 0) logger.LogInformation("Recurring invoices created: {Count}", created.Count);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogError(ex, "Recurring job failed"); }

        try
        {
            if (_remindersDoneFor != today && LocalHour() >= 8)
            {
                await sp.GetRequiredService<NotificationService>().RunRemindersAsync(ct);
                _remindersDoneFor = today;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogError(ex, "Reminder job failed"); }

        try
        {
            var s = await db.GetAsync(ct);
            var todayKey = today.ToString("yyyy-MM-dd"); // matches the Gregorian date in backup file names
            if (s.BackupEnabled && LocalHour() >= s.BackupHour && backups.LastScheduledDate() != todayKey)
            {
                await backups.CreateAsync(BackupKind.Scheduled, ct);
                await backups.ApplyRetentionAsync(s.BackupRetentionCount, ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Scheduled backup failed");
            sp.GetRequiredService<NotificationService>().AddInApp(NotificationType.BackupFailed, Loc.T("پشتیبان‌گیری خودکار ناموفق بود"), Loc.T("جزئیات در لاگ برنامه است. یک پشتیبان دستی بگیرید."));
            await db.SaveChangesAsync(ct);
        }

        int LocalHour() => clock.LocalNow.Hour;
    }
}
