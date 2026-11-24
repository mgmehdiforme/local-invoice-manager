namespace LocalInvoiceManager.Infrastructure;

public sealed class StorageOptions
{
    public const string Section = "Storage";
    /// <summary>Folder for app.db, documents/ and keys/.</summary>
    public string DataPath { get; set; } = "data";
    /// <summary>Folder for backup zips (can be another disk).</summary>
    public string BackupPath { get; set; } = "backups";

    public string DocumentsPath => Path.Combine(DataPath, "documents");
    public string KeysPath => Path.Combine(DataPath, "keys");
}

public sealed class AppOptions
{
    public const string Section = "App";
    /// <summary>IANA or Windows time zone id for "today"; empty = the computer's time zone.</summary>
    public string TimeZone { get; set; } = "";
}
