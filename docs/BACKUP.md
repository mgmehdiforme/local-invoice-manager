# Backup and restore

## What a backup contains
`invoice-backup-<yyyy-MM-dd>-<time>-<kind>.zip` (Gregorian date; backups made before phase 8 carry the Jalali date, e.g. `1405-08-24`)
```
database.db     consistent copy of the SQLite database (made while the app runs)
documents/      logo and stored files
keys/           encryption keys (needed to read the saved SMTP password)
settings.json   your settings in readable form (no passwords)
manifest.json   app version, database version, time, SHA-256 of each file
```
Kinds: `manual`, `scheduled` (daily), `prerestore` (taken automatically before a restore), `premigration` (taken automatically before an app update changes the database).

## Where backups are kept
In the `backups/` folder of the project (set `BACKUP_DIR` in `.env` to use another folder or disk). Keep copies elsewhere too, for example on a USB disk:
```bash
bash scripts/backup.sh /media/$USER/USB/invoice-backups
```

## Automatic backup
Settings → Backup: on or off, the hour of the day, and how many to keep (default 30). The app checks every 5 minutes. If the computer was off at that hour, the backup runs as soon as the app starts again.

## Restore
From the app: Settings → Backup → **Restore** on an entry in the list, or **Restore from file** for a zip from elsewhere.
From the terminal:
```bash
bash scripts/restore.sh /path/to/invoice-backup-....zip
```
What happens:
1. The file is verified: manifest present, every SHA-256 matches, the database passes `PRAGMA integrity_check`, and the database version is not newer than the app.
2. A `prerestore` backup of the current data is saved.
3. The database, documents and keys are replaced, and any newer database updates are applied.

If a restore was a mistake, restore the `prerestore` backup.

## Testing a backup without restoring
Settings → Backup → **Check** on an entry, or:
```bash
docker exec local-invoice-manager dotnet LocalInvoiceManager.Web.dll verify /backups/<file>.zip
```
