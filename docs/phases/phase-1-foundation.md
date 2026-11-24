# Phase 1 — Foundation

**Status:** done (2026-11-24)

## Built
- .NET 10 solution: `Domain`, `Application`, `Infrastructure`, `Web` plus `Domain.Tests`, `Application.Tests` and `Integration.Tests`. Package versions are set centrally (`Directory.Packages.props`), and warnings count as errors.
- SQLite via EF Core 10. The full schema from `docs/DATABASE.md` is in the `InitialCreate` migration. Migrations run at startup, then WAL mode is set. Every connection gets `foreign_keys=ON`, `synchronous=NORMAL` and `busy_timeout=5000`.
- Domain: entities, `InvoiceCalculator` (decimal, AwayFromZero rounding, pro-rata invoice discount), Jalali helpers (built on `PersianCalendar`), and an overdue rule that is calculated rather than stored.
- Auth:
  - First-run password setup, login, logout and change password. Passwords are hashed with PBKDF2 through ASP.NET Identity's `PasswordHasher`.
  - Five wrong passwords lock login for 5 minutes. The lock doubles each time, up to 1 hour. A rate limit of 10 login attempts per minute applies on top.
  - Sessions end after an idle timeout set in Settings (default 30 minutes), with a 12-hour absolute cap. Changing the password signs out every other session (security stamp).
  - CSRF protection: the `XSRF-TOKEN` cookie must be echoed in an `X-XSRF-TOKEN` header on every API request that changes data.
  - Command-line password reset: `reset-password`.
- Settings API: one settings row, Persian validation messages, SMTP password encrypted with Data Protection, and logo upload that checks the file's actual bytes (PNG/JPEG/WebP, at most 1 MB) and blocks path tricks in file names.
- Problem Details errors, each with a stable `code` and a Persian `detail`.
- Security headers on every response (CSP, nosniff, frame options, referrer policy). Only `localhost` and `127.0.0.1` are accepted as host names.
- Frontend:
  - Vite + React 19 + TypeScript strict, Tailwind v4 and shadcn/ui set up for RTL.
  - Vazirmatn bundled locally. Design tokens use a cool neutral palette with one brand colour the user can change.
  - Right-hand sidebar navigation, plus setup and login screens.
  - Jalali date picker, Persian-digit number and money formatting, and an API client that handles XSRF and 401 responses.
- Docker:
  - Multi-stage image that runs as a non-root user. Health check: `dotnet … healthcheck`.
  - Compose binds `127.0.0.1:${APP_PORT:-8090}`. Data lives on the named volume `local-invoice-manager-data`; backups go to the `../backups` bind mount.
  - Logs rotate at 10 MB × 3 files.

## Decisions taken in this phase
- **Port 8090** instead of 8080, because n8n already uses 8080.
- **Cookie `Secure` flag only over HTTPS** (`Security:SecureCookies=false` by default). The in-app browser refused `Secure` cookies on `http://localhost`, which blocked sign-in. Cookies are still HttpOnly and SameSite=Strict, and the app is reachable only from this computer.
- **TypeScript 6** (not 7): TypeScript 7 ships a native binary that cannot run from the NTFS project drive. npm scripts call tools through `node …`, for the same reason.
- **Own Jalali date picker** built on jalaali-js, because react-day-picker 10 dropped its Persian calendar.
- Every `docker COPY` from the NTFS drive uses `--chmod=755`, because the copied files are otherwise unreadable to the non-root user.

## Tests
- Domain (20): calculator (tax, line discounts, pro-rata invoice discount, rounding, precision, large Rial amounts), Jalali conversion and leap years, adding months, recurrence, overdue.
- Integration (8): setup/login/logout, setup only once, XSRF required, lockout, password change ends other sessions, security headers, host filtering, settings validation and secret handling, logo file-type checks.
- Frontend (17): number and money formatting, Jalali parsing and formatting, leap years, weekdays.
- Docker: image builds, the container reports healthy, `/` serves the RTL app, port bound to 127.0.0.1 only.
