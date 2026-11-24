namespace LocalInvoiceManager.Application.Backup;

public enum BackupKind { Manual, Scheduled, PreRestore, PreMigration }

public sealed record BackupInfo(string FileName, long SizeBytes, DateTime CreatedAtUtc, string? Kind, string? AppVersion, string? SchemaVersion, bool ManifestOk);

public sealed record BackupVerifyResult(bool IsValid, IReadOnlyList<string> Problems, DateTime? CreatedAtUtc, string? SchemaVersion, int InvoiceCount, int CustomerCount);

public interface IBackupService
{
    Task<BackupInfo> CreateAsync(BackupKind kind, CancellationToken ct);
    Task<IReadOnlyList<BackupInfo>> ListAsync(CancellationToken ct);
    Stream? OpenRead(string fileName);
    Task<BackupVerifyResult> VerifyAsync(string zipPath, CancellationToken ct);
    Task<BackupVerifyResult> VerifyStoredAsync(string fileName, CancellationToken ct);
    /// <summary>Verifies, takes a pre-restore backup, then replaces database and documents.</summary>
    Task<BackupVerifyResult> RestoreAsync(string zipPath, CancellationToken ct);
    Task<BackupVerifyResult> RestoreStoredAsync(string fileName, CancellationToken ct);
    void Delete(string fileName);
    Task ApplyRetentionAsync(int keep, CancellationToken ct);
    string? LastScheduledDate();
}
