# Security

Threat model: one owner on one Ubuntu computer. The app must not be reachable from the network, must resist casual local access (someone else using the computer), and must never lose or silently change financial data.

## Controls

| Area | Control | Where |
|---|---|---|
| Network exposure | Docker publishes only `127.0.0.1:${APP_PORT}`; Kestrel accepts only the host names `localhost`, `127.0.0.1` and `[::1]` (other `Host` headers get 400) | `deployment/docker-compose.yml`, `appsettings.json` |
| Password storage | PBKDF2 hash from ASP.NET Core Identity `PasswordHasher` (salted, iterated, rehashed automatically when parameters change); minimum 8 characters | `AuthService` |
| First-run takeover | `POST /auth/setup` works only while no password exists | `AuthService.SetupAsync` |
| Brute force | 5 wrong passwords → locked for 5 minutes, doubling each time up to 1 hour; plus a rate limit of 10 login attempts per minute | `AuthService`, `Program.cs` |
| Session | HttpOnly, SameSite=Strict cookie; idle timeout (default 30 min, set in Settings); 12 h absolute cap; status polling does not keep a session alive | `Security.cs` |
| Password change | New security stamp → every other session is signed out | `AuthService.ChangePasswordAsync` |
| CSRF | Antiforgery token: the readable `XSRF-TOKEN` cookie must be echoed in the `X-XSRF-TOKEN` header on every non-GET `/api` request; plus SameSite=Strict | `Security.UseXsrfValidation` |
| Cookie `Secure` flag | Set only over HTTPS (`Security:SecureCookies=false` by default). Some browsers refuse `Secure` cookies on `http://localhost`, which broke sign-in, and traffic never leaves the computer. Set it to `true` if you put HTTPS in front. | `appsettings.json` |
| Headers | CSP `default-src 'self'` (no third-party scripts, fonts or CDNs), `X-Content-Type-Options: nosniff`, `X-Frame-Options: SAMEORIGIN`, `Referrer-Policy: no-referrer`, `Permissions-Policy`; API responses use `Cache-Control: no-store` | `Security.UseSecurityHeaders` |
| Input validation | FluentValidation on every write DTO; decimal limits; error messages in the request language (English or Persian); EF Core parameterised queries only (raw SQL is limited to constant PRAGMAs) | `Application/*` |
| Files | Logo type detected by its actual bytes (PNG/JPEG/WebP), at most 1 MB, random stored name, every path resolved inside the documents folder | `SettingsService`, `LocalFileStore` |
| Backups | File names must match `^invoice-backup-[0-9A-Za-z-]+\.zip$`; SHA-256 checked for every file; only files listed in the manifest are extracted; zip-slip and size guards; `PRAGMA integrity_check`; automatic backup before a restore | `BackupService` |
| Secrets | The SMTP password is encrypted with ASP.NET Data Protection (keys in `/data/keys`), is never returned by the API, and is never logged. `.env` and data folders are git-ignored. | `SecretProtector`, `.gitignore` |
| Errors | 500 responses show a generic message in the request language; details go only to the server log | `ErrorHandler` |
| CSV export | Cells starting with `= + - @` are prefixed with `'` (formula-injection guard) | `ReportService` |
| Financial integrity | Issuing, payments, reversals and conversions run in `BEGIN IMMEDIATE` transactions; unique indexes on invoice number, quotation number and source quotation; no hard deletes of issued invoices or payments; audit log for every financial action | `InvoiceService`, `PaymentService`, `QuotationService` |
| Container | Non-root user, `no-new-privileges`, read-only app files (data only on volumes), log rotation | `Dockerfile`, compose |

## Known limits (accepted)
- Anyone with root on the computer, or in the `docker` group, can read the database and backups. Use full-disk encryption if the computer can be stolen.
- Backups include the Data Protection keys (so the SMTP password still works after restoring on another computer). Keep backup copies somewhere private.
- No HTTPS: the app is served over plain HTTP on the loopback interface only.

## Review log
- 2026-11-24 (release review): all `/api/v1` routes require authentication except `auth/status`, `auth/csrf`, `auth/setup`, `auth/login` and `auth/logout`; `/health` is public and returns only "Healthy". Found and fixed during review: a restore could extract zip entries not listed in the manifest. It now extracts only hash-checked files.

## Forgotten password
Data is not lost. Run:
```bash
docker exec local-invoice-manager dotnet LocalInvoiceManager.Web.dll reset-password
```
Then open the app and set a new password.

## Native (non-Docker) builds
The self-contained downloads listen on `127.0.0.1:8090` only (never on the network), keep data in the user's app-data folder (`%LOCALAPPDATA%`, `~/Library/Application Support`, `~/.local/share`, or `LIM_HOME`), and use the same cookie, XSRF and lockout protections. The binaries are not code-signed: Windows SmartScreen or macOS Gatekeeper may ask for confirmation when the archive is downloaded with a browser (the install scripts download with `curl` / PowerShell and are not affected).
