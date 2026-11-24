# Invoice Manager

Invoices, quotations, payments and reports for freelancers and small businesses, running entirely on your own computer. No account, no cloud, no subscription, and it works offline.

- **English or Persian** interface and documents (left-to-right or right-to-left)
- **Gregorian or Jalali** calendar
- **Several currencies**: each invoice keeps its own currency; reports never mix them
- Customers and products with archive and special prices per customer
- Invoices with live A4 preview, unique numbering, PDF in three templates, amount in words
- Partial payments, reversals, overdue tracking, dashboard and CSV reports
- Quotations that convert to invoices, recurring invoices, due-date reminders
- Share by WhatsApp or e-mail (optional SMTP)
- Automatic daily backups with integrity checks and one-click restore

Your data stays in a local SQLite database. The app listens only on `127.0.0.1`, so other computers on your network cannot reach it.

## Install

### Windows
Open **PowerShell** and run:
```powershell
irm https://raw.githubusercontent.com/mgmehdiforme/local-invoice-manager/main/install/install.ps1 | iex
```
This installs to `%LOCALAPPDATA%\LocalInvoiceManager`, adds Start menu and desktop shortcuts, and opens the app.

### macOS and Linux
```bash
curl -fsSL https://raw.githubusercontent.com/mgmehdiforme/local-invoice-manager/main/install/install.sh | bash
```
Start it later with `invoice-manager` (or from the applications menu on Linux).

### Manual download
Download the archive for your system from [Releases](https://github.com/mgmehdiforme/local-invoice-manager/releases/latest), unpack it and run `InvoiceManager` (`InvoiceManager.exe` on Windows). Your browser opens **http://localhost:8090**.

| System | File |
|---|---|
| Windows (64-bit) | `InvoiceManager-win-x64.zip` |
| macOS (Apple Silicon) | `InvoiceManager-osx-arm64.tar.gz` |
| macOS (Intel) | `InvoiceManager-osx-x64.tar.gz` |
| Linux (x64 / ARM64) | `InvoiceManager-linux-x64.tar.gz` / `InvoiceManager-linux-arm64.tar.gz` |

The builds are not code-signed. If you download with a browser, Windows SmartScreen may show "More info → Run anyway"; on macOS run `xattr -dr com.apple.quarantine InvoiceManager` in the unpacked folder. The install scripts above are not affected.

Options: `--port 9000` (another port), `--no-browser`. Set `LIM_HOME` to keep data somewhere else.

### Docker
```bash
docker run -d --name invoice-manager --restart unless-stopped \
  -p 127.0.0.1:8090:8080 -e TZ=Europe/London \
  -v invoice-data:/data -v "$PWD/backups":/backups \
  ghcr.io/mgmehdiforme/local-invoice-manager:latest
```
Or build from source with Docker Compose: `bash scripts/install.sh` (uses `.env`, see `.env.example`).

## First run
Open http://localhost:8090. Choose the language, calendar and default currency, set a password, then fill in **Settings** (business details, bank details, tax, numbering). To try the app with fictional data on an empty install:

```bash
invoice-manager seed-demo
```
(Docker: `docker exec invoice-manager dotnet LocalInvoiceManager.Web.dll seed-demo`.)

## Where your data lives
| Install | Data and backups |
|---|---|
| Windows | `%LOCALAPPDATA%\LocalInvoiceManager` |
| macOS | `~/Library/Application Support/LocalInvoiceManager` |
| Linux | `~/.local/share/LocalInvoiceManager` |
| Docker | volume `invoice-data` (`/data`) and the folder mounted on `/backups` |

Updating (running the installer again, or pulling a new image) keeps your data; a safety backup is made before any database upgrade. Copy a backup to another disk from time to time.

Forgot the password? Data is kept: run `invoice-manager reset-password` (Docker: `docker exec invoice-manager dotnet LocalInvoiceManager.Web.dll reset-password`), then open the app and set a new one.

## Guides
- English user guide: [docs/USER_GUIDE.md](docs/USER_GUIDE.md) · راهنمای فارسی: [docs/USER_GUIDE.fa.md](docs/USER_GUIDE.fa.md)
- Architecture: [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) · Database: [docs/DATABASE.md](docs/DATABASE.md) · API: [docs/API.md](docs/API.md)
- Security: [docs/SECURITY.md](docs/SECURITY.md) · Backup: [docs/BACKUP.md](docs/BACKUP.md) · Requirements: [docs/PRD.md](docs/PRD.md) · Phase reports: [docs/phases/](docs/phases/)

## Development
Requirements: .NET SDK 10, Node 24.

```bash
cd backend && ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/LocalInvoiceManager.Web
```

```bash
cd frontend && npm install && npm run dev
```

The API runs on `http://localhost:5080`, the UI on `http://localhost:5173` (it proxies `/api`).

Tests:

```bash
cd backend && dotnet test
```

```bash
cd frontend && npm test && npm run i18n:check && npm run build
```

Every user-visible string goes through `t("…")` (frontend) or `Loc.T("…")` (backend), with the English text in `frontend/src/i18n/en/` and `backend/src/LocalInvoiceManager.Domain/Localization/En.cs`. `npm run i18n:check` fails on untranslated text.

Release: push a tag such as `v1.0.0`. GitHub Actions builds the downloads (`scripts/publish-native.sh`) and the Docker image, and publishes the release.

## Stack
.NET 10 · ASP.NET Core minimal APIs · EF Core + SQLite (WAL) · FluentValidation · QuestPDF · MailKit · React 19 + TypeScript · Tailwind CSS 4 · shadcn/ui · TanStack Query · Recharts · Vazirmatn. No CDN or online service at runtime.

## Licence
MIT, see [LICENSE](LICENSE). Bundled parts: Vazirmatn font (SIL Open Font License). QuestPDF is used under its Community licence, which is free for individuals and businesses with less than USD 1M yearly revenue; larger businesses need a QuestPDF commercial licence.
