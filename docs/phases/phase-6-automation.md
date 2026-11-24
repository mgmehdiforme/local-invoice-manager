# Phase 6 — Automation, sharing and backup

**Status:** done (2026-11-24)

## Built
- **Backup** (`docs/BACKUP.md` has the full procedure):
  - A zip with `database.db` (a consistent copy made with the SQLite online-backup API while the app runs), `documents/`, `keys/`, `settings.json` (no secrets) and `manifest.json` (app and schema version, time, SHA-256 of every file).
  - Manual backup from Settings or `scripts/backup.sh [copy-to-dir]`.
  - **Daily automatic backup** after the configured hour. It catches up if the computer was off, and old backups beyond the retention count (default 30) are deleted.
  - Automatic backup **before a restore** and **before a database update**.
  - Verify: checks the manifest and every file hash, runs `PRAGMA integrity_check`, and checks the schema version. A backup from a newer app version is refused.
  - Restore: by uploading a file or choosing from the history.
    1. The file is verified.
    2. A safety backup is taken.
    3. Other requests are paused (they get 503).
    4. The database is copied page by page into the live one.
    5. Newer migrations are applied.
    6. Documents are replaced and keys are merged.
    Only files listed in the manifest are extracted, with guards against zip-slip and oversized files.
  - The backup history is read from the backup folder itself, so a restore cannot erase it.
  - Command line: `dotnet LocalInvoiceManager.Web.dll backup | verify <zip> | restore <zip>`, used by the scripts.
- **Reminders and notifications:**
  - A daily job (after 08:00) creates in-app notices for "due soon" (within N days, once per invoice) and "overdue" (at most once a week per invoice). A unique `DedupKey` prevents duplicates.
  - Optional email reminder to the customer, with the PDF attached.
  - Bell in the top bar with an unread count (checked every minute). Clicking a notice opens its invoice; "mark all read" is available.
- **Sharing an issued invoice:**
  - Email with the PDF attached through any SMTP provider (MailKit). This is optional, and a clear message appears when SMTP is not set.
  - A WhatsApp `wa.me` link with a ready-made Persian message: amount, balance, due date, card number and IBAN. The customer's mobile number is normalised to `98…`.
  - Copy the message text.
  - Every email is logged in NotificationLogs.
- **Recurring invoices:**
  - Weekly, monthly, quarterly or yearly on the **Jalali calendar**: "1st of every month" means 1 Farvardin, 1 Ordibehesht, and so on, with day-of-month clamping.
  - Each run either creates a draft or issues automatically.
  - Missed runs catch up (at most 12 per schedule per run); the schedule stops after its end date.
  - "Create now" button; failures appear as notifications.
  - Editor reuses the document editor, with a schedule panel.
- **Background job runner:** checks every 5 minutes and right after start-up; it is turned off in tests.
- **Scripts:**
  - `scripts/install.sh`: checks Docker, creates `.env` with your UID/GID, checks the port, builds, waits for the health check and prints the URL.
  - `scripts/backup.sh`
  - `scripts/restore.sh`: verifies first, asks you to type `yes`, then restarts the app.

## Tests
- Integration (11):
  - **Backup → change data → download → restore by upload → data is exactly as it was**, numbering continues correctly, and a pre-restore backup exists.
  - Tampered zip, a non-zip file and a path-traversal file name are all rejected.
  - Retention keeps only the newest N.
  - A reminder is created once and not twice; unread count and mark-read work.
  - Recurring monthly schedule from 1 Shahrivar 1405 catches up month by month on 1st-of-Jalali-month dates, with auto-issue; a second run creates nothing.
  - Email without SMTP gives a clear error; the share link has the normalised phone; mobile normalisation (5 cases).
