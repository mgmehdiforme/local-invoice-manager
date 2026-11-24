using LocalInvoiceManager.Application.Backup;
using LocalInvoiceManager.Infrastructure.Persistence;

namespace LocalInvoiceManager.Web.Infrastructure;

/// <summary>Maintenance commands run inside the container: docker exec local-invoice-manager dotnet LocalInvoiceManager.Web.dll backup</summary>
public static class Cli
{
    public static async Task<int> RunAsync(WebApplication app, string[] args)
    {
        var backups = app.Services.GetRequiredService<IBackupService>();
        using (var scope = app.Services.CreateScope())
            await DatabaseInitializer.InitializeAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), app.Logger);

        switch (args[0])
        {
            case "seed-demo":
                using (var scope = app.Services.CreateScope())
                    return await DemoSeeder.RunAsync(scope.ServiceProvider, CancellationToken.None);
            case "backup":
                var info = await backups.CreateAsync(BackupKind.Manual, CancellationToken.None);
                Console.WriteLine(info.FileName);
                return 0;
            case "verify" or "restore" when args.Length < 2:
                Console.Error.WriteLine($"usage: {args[0]} <path-to-backup.zip>");
                return 2;
            case "verify":
                return Report(await backups.VerifyAsync(args[1], CancellationToken.None));
            default:
                var result = await backups.RestoreAsync(args[1], CancellationToken.None);
                if (result.IsValid) Console.WriteLine("Restore completed. A safety backup of the previous data was saved first.");
                return Report(result);
        }
    }

    private static int Report(BackupVerifyResult r)
    {
        Console.WriteLine(r.IsValid
            ? $"OK — {r.InvoiceCount} invoices, {r.CustomerCount} customers, schema {r.SchemaVersion}"
            : "INVALID:\n  " + string.Join("\n  ", r.Problems));
        return r.IsValid ? 0 : 1;
    }
}
