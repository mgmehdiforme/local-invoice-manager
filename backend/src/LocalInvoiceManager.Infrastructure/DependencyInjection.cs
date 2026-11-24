using LocalInvoiceManager.Application.Abstractions;
using LocalInvoiceManager.Application.Backup;
using LocalInvoiceManager.Application.Documents;
using LocalInvoiceManager.Infrastructure.Backup;
using LocalInvoiceManager.Infrastructure.Email;
using LocalInvoiceManager.Infrastructure.Pdf;
using LocalInvoiceManager.Infrastructure.Persistence;
using LocalInvoiceManager.Infrastructure.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace LocalInvoiceManager.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<StorageOptions>(config.GetSection(StorageOptions.Section));
        services.Configure<AppOptions>(config.GetSection(AppOptions.Section));
        var storage = config.GetSection(StorageOptions.Section).Get<StorageOptions>() ?? new StorageOptions();
        Directory.CreateDirectory(storage.DataPath);
        Directory.CreateDirectory(storage.DocumentsPath);
        Directory.CreateDirectory(storage.BackupPath);

        var connectionString = config.GetConnectionString("Default")
            ?? $"Data Source={Path.Combine(storage.DataPath, "app.db")}";

        services.AddSingleton<SqlitePragmaInterceptor>();
        services.AddDbContext<AppDbContext>((sp, o) => o
            .UseSqlite(connectionString)
            .AddInterceptors(sp.GetRequiredService<SqlitePragmaInterceptor>()));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddDataProtection()
            .SetApplicationName("LocalInvoiceManager")
            .PersistKeysToFileSystem(new DirectoryInfo(storage.KeysPath));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IBusinessClock, BusinessClock>();
        services.AddSingleton<IFileStore, LocalFileStore>();
        services.AddSingleton<ISecretProtector, SecretProtector>();
        services.AddSingleton<IPdfRenderer, QuestPdfRenderer>();
        services.AddSingleton<RestoreGate>();
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IBackupService, BackupService>();
        return services;
    }
}
