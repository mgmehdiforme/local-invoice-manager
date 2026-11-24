using LocalInvoiceManager.Domain.Localization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using LocalInvoiceManager.Application.Abstractions;
using LocalInvoiceManager.Application.Backup;
using LocalInvoiceManager.Application.Common;
using LocalInvoiceManager.Application.Settings;
using LocalInvoiceManager.Domain;
using LocalInvoiceManager.Domain.Calendar;
using LocalInvoiceManager.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LocalInvoiceManager.Infrastructure.Backup;

/// <summary>
/// Self-describing backup zips:
///   database.db      consistent copy via the SQLite online-backup API (safe while the app runs, WAL included)
///   documents/       logo and other stored files
///   keys/            Data Protection keys (so the saved SMTP password still decrypts after a restore elsewhere)
///   settings.json    readable copy of the settings (no secrets)
///   manifest.json    app/schema version, time, and SHA-256 of every file
/// History is read from the backup folder itself, so a restore can never erase it.
/// </summary>
public sealed partial class BackupService(
    IServiceScopeFactory scopes,
    IOptions<StorageOptions> storage,
    IBusinessClock clock,
    RestoreGate gate,
    ILogger<BackupService> logger) : IBackupService
{
    private const string Prefix = "invoice-backup-";
    private const long MaxEntryBytes = 2L * 1024 * 1024 * 1024; // zip-bomb guard
    private static readonly SemaphoreSlim Lock = new(1, 1);
    private string BackupDir => Path.GetFullPath(storage.Value.BackupPath);
    private string DataDir => Path.GetFullPath(storage.Value.DataPath);

    [GeneratedRegex(@"^invoice-backup-[0-9A-Za-z\-]+\.zip$")]
    private static partial Regex SafeName();

    public sealed record Manifest(string App, string AppVersion, string SchemaVersion, DateTime CreatedAtUtc, string JalaliDate, string Kind, List<ManifestFile> Files);
    public sealed record ManifestFile(string Path, long Size, string Sha256);

    public async Task<BackupInfo> CreateAsync(BackupKind kind, CancellationToken ct)
    {
        await Lock.WaitAsync(ct);
        try { return await CreateCoreAsync(kind, ct); }
        finally { Lock.Release(); }
    }

    private async Task<BackupInfo> CreateCoreAsync(BackupKind kind, CancellationToken ct)
    {
        Directory.CreateDirectory(BackupDir);
        var now = clock.UtcNow;
        var today = clock.Today;
        // Gregorian date in the name (sortable, calendar-neutral). Older backups may carry a Jalali date (1405-…).
        var name = $"{Prefix}{today:yyyy-MM-dd}-{clock.LocalNow:HHmmss}-{kind.ToString().ToLowerInvariant()}.zip";
        var work = Directory.CreateTempSubdirectory("lim-backup-");
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var schema = (await db.Database.GetAppliedMigrationsAsync(ct)).LastOrDefault() ?? "none";

            // 1) consistent database copy
            var dbCopy = Path.Combine(work.FullName, "database.db");
            var conn = (SqliteConnection)db.Database.GetDbConnection();
            await db.Database.OpenConnectionAsync(ct);
            try
            {
                await using var dest = new SqliteConnection($"Data Source={dbCopy};Pooling=False");
                await dest.OpenAsync(ct);
                conn.BackupDatabase(dest);
            }
            finally { await db.Database.CloseConnectionAsync(); }

            // 2) readable settings (no secrets). Informational only, so it is skipped when the database schema is older
            //    than the code — the case for the automatic backup taken right before a migration.
            try
            {
                var settings = SettingsService.ToDto(await db.GetAsync(ct)) with { SmtpPassword = null };
                await File.WriteAllTextAsync(Path.Combine(work.FullName, "settings.json"), JsonSerializer.Serialize(settings, JsonDefaults.Options), ct);
            }
            catch (SqliteException e) when (kind == BackupKind.PreMigration)
            {
                logger.LogInformation("Settings summary skipped in the pre-migration backup ({Error}).", e.Message);
            }

            // 3) zip everything with hashes
            var tmpZip = Path.Combine(BackupDir, name + ".partial");
            var files = new List<ManifestFile>();
            await using (var fs = File.Create(tmpZip))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                files.Add(await AddFileAsync(zip, dbCopy, "database.db", ct));
                var settingsFile = Path.Combine(work.FullName, "settings.json");
                if (File.Exists(settingsFile)) files.Add(await AddFileAsync(zip, settingsFile, "settings.json", ct));
                foreach (var (dir, prefix) in new[] { (storage.Value.DocumentsPath, "documents"), (storage.Value.KeysPath, "keys") })
                {
                    if (!Directory.Exists(dir)) continue;
                    foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                        files.Add(await AddFileAsync(zip, f, $"{prefix}/{Path.GetRelativePath(dir, f).Replace('\\', '/')}", ct));
                }
                var manifest = new Manifest("LocalInvoiceManager", AppVersion, schema, now, Jalali.Format(today), kind.ToString(), files);
                var entry = zip.CreateEntry("manifest.json", CompressionLevel.Optimal);
                await using var es = entry.Open();
                await JsonSerializer.SerializeAsync(es, manifest, JsonDefaults.Options, ct);
            }
            var final = Path.Combine(BackupDir, name);
            File.Move(tmpZip, final, overwrite: true);
            logger.LogInformation("Backup created: {File} ({Kind})", name, kind);
            return Info(new FileInfo(final));
        }
        finally
        {
            SqliteConnection.ClearPool(new SqliteConnection($"Data Source={Path.Combine(work.FullName, "database.db")}"));
            try { work.Delete(true); } catch { /* temp */ }
        }
    }

    private static async Task<ManifestFile> AddFileAsync(ZipArchive zip, string path, string entryName, CancellationToken ct)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        await using var src = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        await using var dst = entry.Open();
        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[81920];
        long size = 0;
        int read;
        while ((read = await src.ReadAsync(buffer, ct)) > 0)
        {
            sha.AppendData(buffer, 0, read);
            await dst.WriteAsync(buffer.AsMemory(0, read), ct);
            size += read;
        }
        return new ManifestFile(entryName, size, Convert.ToHexString(sha.GetHashAndReset()).ToLowerInvariant());
    }

    public Task<IReadOnlyList<BackupInfo>> ListAsync(CancellationToken ct)
    {
        if (!Directory.Exists(BackupDir)) return Task.FromResult<IReadOnlyList<BackupInfo>>([]);
        IReadOnlyList<BackupInfo> list = new DirectoryInfo(BackupDir).EnumerateFiles($"{Prefix}*.zip")
            .Select(Info).OrderByDescending(b => b.CreatedAtUtc).ToList();
        return Task.FromResult(list);
    }

    private BackupInfo Info(FileInfo f)
    {
        try
        {
            using var zip = ZipFile.OpenRead(f.FullName);
            var m = ReadManifest(zip);
            return new BackupInfo(f.Name, f.Length, m?.CreatedAtUtc ?? f.LastWriteTimeUtc, m?.Kind, m?.AppVersion, m?.SchemaVersion, m is not null);
        }
        catch (InvalidDataException)
        {
            return new BackupInfo(f.Name, f.Length, f.LastWriteTimeUtc, null, null, null, false);
        }
    }

    private static Manifest? ReadManifest(ZipArchive zip)
    {
        var e = zip.GetEntry("manifest.json");
        if (e is null || e.Length > 10_000_000) return null;
        using var s = e.Open();
        try { return JsonSerializer.Deserialize<Manifest>(s, JsonDefaults.Options); }
        catch (JsonException) { return null; }
    }

    public Stream? OpenRead(string fileName)
    {
        var path = StoredPath(fileName);
        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    public void Delete(string fileName)
    {
        var path = StoredPath(fileName);
        if (File.Exists(path)) File.Delete(path);
    }

    public Task<BackupVerifyResult> VerifyStoredAsync(string fileName, CancellationToken ct) => VerifyAsync(StoredPath(fileName), ct);

    public Task<BackupVerifyResult> RestoreStoredAsync(string fileName, CancellationToken ct) => RestoreAsync(StoredPath(fileName), ct);

    public async Task<BackupVerifyResult> VerifyAsync(string zipPath, CancellationToken ct)
    {
        var problems = new List<string>();
        Manifest? manifest = null;
        int invoices = 0, customers = 0;
        if (!File.Exists(zipPath)) return new BackupVerifyResult(false, [Loc.T("فایل پشتیبان پیدا نشد.")], null, null, 0, 0);
        var work = Directory.CreateTempSubdirectory("lim-verify-");
        try
        {
            using var zip = ZipFile.OpenRead(zipPath);
            manifest = ReadManifest(zip);
            if (manifest is null || manifest.App != "LocalInvoiceManager")
                return new BackupVerifyResult(false, [Loc.T("این فایل پشتیبان این برنامه نیست یا manifest.json ندارد.")], null, null, 0, 0);

            foreach (var f in manifest.Files)
            {
                var entry = zip.GetEntry(f.Path);
                if (entry is null) { problems.Add(Loc.T("فایل {0} در پشتیبان نیست.", f.Path)); continue; }
                if (entry.Length > MaxEntryBytes) { problems.Add(Loc.T("فایل {0} بیش از حد بزرگ است.", f.Path)); continue; }
                await using var s = entry.Open();
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(s, ct)).ToLowerInvariant();
                if (hash != f.Sha256) problems.Add(Loc.T("فایل {0} خراب است (هش مطابقت ندارد).", f.Path));
            }
            if (manifest.Files.All(f => f.Path != "database.db")) problems.Add(Loc.T("پایگاه داده در پشتیبان نیست."));

            if (problems.Count == 0)
            {
                var dbPath = Path.Combine(work.FullName, "check.db");
                zip.GetEntry("database.db")!.ExtractToFile(dbPath);
                await using (var c = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly;Pooling=False"))
                {
                    await c.OpenAsync(ct);
                    await using var cmd = c.CreateCommand();
                    cmd.CommandText = "PRAGMA integrity_check;";
                    var result = (string?)await cmd.ExecuteScalarAsync(ct);
                    if (result != "ok") problems.Add(Loc.T("بررسی سلامت پایگاه داده ناموفق بود."));
                    cmd.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId DESC LIMIT 1;";
                    var schema = (string?)await cmd.ExecuteScalarAsync(ct);
                    using var scope = scopes.CreateScope();
                    var known = scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.GetMigrations().ToList();
                    if (schema is null || !known.Contains(schema))
                        problems.Add(Loc.T("این پشتیبان با نسخه جدیدتری از برنامه ساخته شده است. ابتدا برنامه را به‌روز کنید."));
                    cmd.CommandText = "SELECT (SELECT COUNT(*) FROM Invoices), (SELECT COUNT(*) FROM Customers);";
                    await using var r = await cmd.ExecuteReaderAsync(ct);
                    if (await r.ReadAsync(ct)) { invoices = r.GetInt32(0); customers = r.GetInt32(1); }
                }
            }
        }
        catch (InvalidDataException)
        {
            problems.Add(Loc.T("فایل zip معتبر نیست یا ناقص است."));
        }
        catch (SqliteException ex)
        {
            problems.Add(Loc.T("پایگاه داده داخل پشتیبان قابل خواندن نیست."));
            logger.LogWarning(ex, "Backup verify failed for {File}", Path.GetFileName(zipPath));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            try { work.Delete(true); } catch { /* temp */ }
        }
        return new BackupVerifyResult(problems.Count == 0, problems, manifest?.CreatedAtUtc, manifest?.SchemaVersion, invoices, customers);
    }

    public async Task<BackupVerifyResult> RestoreAsync(string zipPath, CancellationToken ct)
    {
        var check = await VerifyAsync(zipPath, ct);
        if (!check.IsValid) return check;

        await Lock.WaitAsync(ct);
        try
        {
            await CreateCoreAsync(BackupKind.PreRestore, ct); // safety net before anything changes
            using var _ = await gate.EnterAsync(ct);          // block other requests while data is swapped
            var work = Directory.CreateTempSubdirectory("lim-restore-");
            try
            {
                using (var zip = ZipFile.OpenRead(zipPath))
                {
                    // Only files listed in the manifest (and therefore hash-checked by VerifyAsync) are extracted.
                    var allowed = ReadManifest(zip)!.Files.Select(f => f.Path).ToHashSet(StringComparer.Ordinal);
                    foreach (var entry in zip.Entries)
                    {
                        if (entry.FullName.EndsWith('/') || !allowed.Contains(entry.FullName)) continue;
                        var target = Path.GetFullPath(Path.Combine(work.FullName, entry.FullName));
                        if (!target.StartsWith(work.FullName + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                            throw new DomainException("backup.unsafe", "مسیر نامعتبر در فایل پشتیبان."); // zip-slip guard
                        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                        entry.ExtractToFile(target, overwrite: true);
                    }
                }

                // Database: copy page-by-page into the live database (safe with WAL and open pools).
                using (var scope = scopes.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var live = (SqliteConnection)db.Database.GetDbConnection();
                    await db.Database.OpenConnectionAsync(ct);
                    try
                    {
                        await using var src = new SqliteConnection($"Data Source={Path.Combine(work.FullName, "database.db")};Mode=ReadOnly;Pooling=False");
                        await src.OpenAsync(ct);
                        src.BackupDatabase(live);
                    }
                    finally { await db.Database.CloseConnectionAsync(); }
                    SqliteConnection.ClearAllPools();
                    await DatabaseInitializer.InitializeAsync(db, logger, ct); // apply newer migrations if the backup is older
                }

                // Documents: replace. Keys: add missing ones (never delete current keys).
                var docs = storage.Value.DocumentsPath;
                if (Directory.Exists(docs)) Directory.Delete(docs, true);
                Directory.CreateDirectory(docs);
                CopyDir(Path.Combine(work.FullName, "documents"), docs, overwrite: true);
                CopyDir(Path.Combine(work.FullName, "keys"), storage.Value.KeysPath, overwrite: false);
                logger.LogWarning("Data restored from backup {File}", Path.GetFileName(zipPath));
            }
            finally
            {
                SqliteConnection.ClearAllPools();
                try { work.Delete(true); } catch { /* temp */ }
            }
        }
        finally { Lock.Release(); }
        return check;
    }

    public async Task ApplyRetentionAsync(int keep, CancellationToken ct)
    {
        var all = await ListAsync(ct);
        foreach (var old in all.Skip(Math.Max(1, keep)))
        {
            Delete(old.FileName);
            logger.LogInformation("Old backup removed by retention: {File}", old.FileName);
        }
    }

    public string? LastScheduledDate()
    {
        if (!Directory.Exists(BackupDir)) return null;
        return new DirectoryInfo(BackupDir).EnumerateFiles($"{Prefix}*-scheduled.zip")
            .OrderByDescending(f => f.Name).Select(f => f.Name[Prefix.Length..(Prefix.Length + 10)]).FirstOrDefault();
    }

    private string StoredPath(string fileName)
    {
        if (!SafeName().IsMatch(fileName))
            throw new DomainException("backup.name", "نام فایل پشتیبان نامعتبر است.");
        return Path.Combine(BackupDir, fileName);
    }

    private static void CopyDir(string from, string to, bool overwrite)
    {
        if (!Directory.Exists(from)) return;
        foreach (var f in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, f));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            if (overwrite || !File.Exists(target)) File.Copy(f, target, overwrite);
        }
    }

    public static string AppVersion => typeof(BackupService).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";
}

/// <summary>While a restore runs, other API requests wait (or get 503) so nobody writes to half-restored data.</summary>
public sealed class RestoreGate
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    public bool IsRestoring { get; private set; }

    public async Task<IDisposable> EnterAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        IsRestoring = true;
        return new Release(this);
    }

    private sealed class Release(RestoreGate g) : IDisposable
    {
        public void Dispose() { g.IsRestoring = false; g._gate.Release(); }
    }
}
