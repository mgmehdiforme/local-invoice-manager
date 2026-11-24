# Local Invoice Manager

The source of truth for requirements is [docs/PRD.md](docs/PRD.md). Read the relevant sections before any design or implementation work, and follow its §16 Implementation Policy: one phase at a time, docs before code, build and tests passing before moving on, and ask for approval at each milestone.

## Stack (from the PRD)
- Backend: .NET 10, ASP.NET Core Web API, EF Core, SQLite (WAL), modular monolith (Domain / Application / Infrastructure / Web).
- Frontend: React + TypeScript strict, Tailwind CSS, shadcn/ui components (use the `shadcn` MCP server to add components instead of hand-writing them).
- Deployment: Docker Compose, bound to `127.0.0.1:8090` only (app URL `http://localhost:8090`).

## Tooling rules
- Only free, open-source tools that need no account, sign-in or complex setup, and that are reachable from Iran.
- No CDN or online service at runtime: bundle fonts, icons and components locally. Pin dependencies with lock files.
- Machine setup and known limits: [docs/ENVIRONMENT.md](docs/ENVIRONMENT.md). Host port 8080 is taken by n8n, so the app uses `APP_PORT=8090` from `.env` (bound to `127.0.0.1:8090`; container still listens on 8080). This overrides the PRD's port.

## UI rules
- English (LTR) and Persian (RTL) UI. Wrap every user-visible string in `t("<Persian text>")` and add the English to `frontend/src/i18n/en/*.ts`; `npm run i18n:check` must pass. Server text: `Loc.T("<Persian>")` + `backend/src/LocalInvoiceManager.Domain/Localization/En.cs`.
- Use logical CSS (`ms-`, `pe-`, `text-start`), flip directional icons with `i18n.dir`. Vazirmatn font, light theme, neutral colors, configurable brand accent.
- Calendar (Gregorian or Jalali) is an app setting: use `lib/calendar.ts` / `formatDate` in the UI and `Loc.Calendar` on the server; store Gregorian `DateOnly` / UTC timestamps.
- Several currencies: each document freezes its own currency; never add amounts in different currencies.
- Money is always `decimal`; the backend is the source of truth for totals.
- Use the `frontend-design` skill for visual direction.

## Layout and commands
- Releases: push a `v*` tag; `.github/workflows/release.yml` builds the native downloads (`scripts/publish-native.sh`) and the GHCR image. Installers: `install/install.sh`, `install/install.ps1`.
- `backend/` .NET solution (Domain, Application, Infrastructure, Web + tests). `frontend/` Vite React app. `deployment/` Dockerfile + compose. `scripts/` install/backup/restore. `docs/` PRD, architecture, DB, API, security, backup, phase reports, Persian user guide.
- Tests: `cd backend && dotnet test`; `cd frontend && npm test && npm run build`.
- This disk is NTFS (no exec bit): run the API with `dotnet bin/Debug/net10.0/LocalInvoiceManager.Web.dll`, and call npm tools through `node` (already in package.json).
- Shared invoice/quotation editor and A4 preview: `frontend/src/features/documents`. Totals always come from `POST /api/v1/invoices/calculate`.
- New migration: `dotnet ef migrations add <Name> -p src/LocalInvoiceManager.Infrastructure -s src/LocalInvoiceManager.Infrastructure -o Persistence/Migrations`.
