# Architecture

Local Invoice Manager is a single-user, local-first web app. One Docker container serves both the API and the React UI, stores data in SQLite on a Docker volume, and listens only on `127.0.0.1:8090` on the host.

```
Browser (http://localhost:8090)
   │  same origin: cookies + XSRF header
   ▼
┌──────────────── Docker container (port 8080 inside) ────────────────┐
│ ASP.NET Core (LocalInvoiceManager.Web)                              │
│   ├── /api/v1/*         Minimal API endpoints                       │
│   ├── /health           health check                                │
│   └── /*                React SPA (static files from wwwroot)       │
│ Application services ── Domain rules ── Infrastructure (EF, PDF…)   │
│ Background jobs: daily backup, reminders, recurring invoices        │
└──────────────┬───────────────────────────────┬──────────────────────┘
               │ /data volume                  │ /backups bind mount
         app.db (SQLite WAL)            invoice-backup-*.zip
         documents/ (logo…)
```

## Projects (modular monolith)

| Project | Holds | Depends on |
|---|---|---|
| `LocalInvoiceManager.Domain` | Entities, enums, money/tax calculation, calendar system (Gregorian/Jalali), server text localization (`Localization/Loc`, `En`), domain errors. No framework code. | – |
| `LocalInvoiceManager.Application` | Use-case services (one per feature), DTOs, FluentValidation validators, interfaces for infrastructure (`IAppDbContext`, `IPdfRenderer`, `IEmailSender`, `IFileStore`). | Domain, EF Core (abstractions only) |
| `LocalInvoiceManager.Infrastructure` | `AppDbContext`, entity configurations, migrations, QuestPDF templates, MailKit sender, file store, backup engine. | Application |
| `LocalInvoiceManager.Web` | `Program.cs`, endpoints, authentication, CSRF, rate limiting, error handling, background services, SPA hosting. | Application, Infrastructure |

Application services use `IAppDbContext` (EF `DbSet`s) directly. There is no repository layer and no CQRS/mediator: one service class per feature keeps the code short and easy to follow.

Tests:
- `Domain.Tests`: calculation, rounding, Gregorian/Jalali conversion, numbering format, English/Persian amount in words.
- `Application.Tests`: services against an in-memory SQLite database (invoice lifecycle, payments, snapshots, quotations).
- `Integration.Tests`: the real HTTP pipeline (`WebApplicationFactory`): auth, CSRF, endpoints, PDF, backup/restore.

## Frontend

Vite + React + TypeScript (strict), Tailwind CSS v4, shadcn/ui components copied into `src/components/ui`.

| Concern | Library (all MIT, free, bundled locally) |
|---|---|
| Routing | react-router |
| Server state | @tanstack/react-query |
| Forms | react-hook-form + zod |
| Icons | lucide-react |
| Charts | recharts |
| Toasts | sonner |
| Date picker | own component over `lib/calendar.ts` (Gregorian or Jalali) |
| Jalali conversion | jalaali-js |
| Translations | own `t()` in `src/i18n` (Persian keys, English dictionaries in `src/i18n/en`), checked by `npm run i18n:check` |
| Font | Vazirmatn (bundled woff2) |

Folder layout follows the PRD (`app/`, `components/`, `layouts/`, `features/<feature>/`, `services/`, `hooks/`, `styles/`). Each feature folder owns its pages, API hooks and components.

The UI runs in English (LTR) or Persian (RTL, Persian digits); dates follow the app calendar (Gregorian or Jalali). Both are app-wide settings; switching reloads the page. See [phases/phase-8-global.md](phases/phase-8-global.md).

## Key decisions

| # | Decision | Why |
|---|---|---|
| D1 | One container serves API + SPA | One port, same-origin cookies, simplest deploy. |
| D2 | Minimal APIs grouped by feature | Less ceremony than controllers. |
| D3 | `decimal` everywhere for money; stored as TEXT in SQLite; sums done in .NET | SQLite has no exact decimal type. TEXT keeps exact values, and data size for a single business is small. |
| D4 | Rounding: `MidpointRounding.AwayFromZero` to the currency's decimal places at line, discount, tax and total level | Matches how people round by hand; deterministic. |
| D5 | Totals are always calculated by the backend; the UI asks `POST /invoices/calculate` for live preview totals | PRD: the frontend is never the source of truth. |
| D6 | Invoice numbers come from a `NumberSequences` row updated in the same transaction as the issue, plus a unique index | Transactional, never reused, gap-free in normal use. |
| D7 | Snapshots: customer, seller and currency are copied onto the invoice at issue time; line items always store description/unit/price | Later edits never change issued invoices. |
| D8 | PDFs with QuestPDF (Community license, free under USD 1M yearly revenue) and embedded Vazirmatn | Native RTL text shaping, no headless browser (keeps the image small and offline). |
| D9 | Business "today" uses the configured time zone (the computer's own zone by default) via `TimeProvider` | Overdue and the numbering year must match the user's calendar. |
| D10 | Cookie auth + PBKDF2 password hash (ASP.NET Identity `PasswordHasher`), XSRF token header, login lockout | Standard, no external identity service. |
| D11 | Backups are self-describing zips (`manifest.json` with SHA-256 hashes); history comes from the backup folder, not the DB | A restore would otherwise overwrite its own history. |
| D12 | Optional integrations: SMTP email (MailKit) and WhatsApp share links (`wa.me`) | No paid API and nothing required for offline work. |
| D13 | No runtime CDN; all fonts/icons/components are in the bundle | Works fully offline, in any country. |
| D14 | English + Persian, Gregorian + Jalali, several currencies (phase 8) | Global use; amounts in different currencies are never summed. |
| D15 | Self-contained single-file downloads besides Docker | Install on Windows/macOS/Linux without Docker or .NET. |

## Error handling

- Domain/business rule violations throw `DomainException(code, message)` → HTTP 409 or 400 with a Problem Details body: `{ title, detail, code, errors? }`.
- Validation failures → HTTP 400 with `errors` per field (FluentValidation).
- Not found → 404. Unexpected → 500 with a generic message (details only in logs).
- Messages shown to the user are in the request language (English or Persian); codes are stable English identifiers.

## Logging

Built-in `Microsoft.Extensions.Logging` with JSON console output in production (`docker compose logs`). Passwords, cookies and SMTP secrets are never logged.

## Configuration

| Setting | Source | Default |
|---|---|---|
| `APP_PORT` | `.env` → compose | 8090 |
| `ConnectionStrings__Default` | env | `Data Source=/data/app.db` |
| `Storage__DataPath` | env | `/data` |
| `Storage__BackupPath` | env | `/backups` |
| `App__TimeZone` | env | empty = the computer's time zone (Docker: `TZ`, default UTC) |

Everything else (company, currency, tax, numbering, session timeout, backup schedule, SMTP) is in the Settings screen and stored in the `AppSettings` row.
