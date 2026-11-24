# Implementation Plan

Each phase ends with: `dotnet build` (warnings as errors), `dotnet test`, `npm run build`, `npm test`, and a report in `docs/phases/phase-N.md` (what was built, decisions, tests, known limits).

| Phase | Scope | Done when |
|---|---|---|
| 1 Foundation | Solution + 4 projects + 3 test projects; SQLite/EF/WAL; initial migration; auth (setup, login, logout, change password, lockout, timeout, CSRF); settings API; React app with RTL shell, Vazirmatn, sidebar navigation, login/setup pages; Dockerfile + compose on `127.0.0.1:8090` | Container starts, setup + login work, data survives restart |
| 2 Master data | Customers and products (CRUD, archive, search); settings screens: company, logo, bank, currency, tax, numbering, security | Screens work end to end; validation in Persian |
| 3 Invoice engine | Draft create/edit, calculator, live preview, issue with numbering + snapshots, cancel, duplicate, delete draft, PDF with 3 templates, print | Numbers unique under concurrency test; Persian PDF renders correctly |
| 4 Financial | Payments (partial, multiple, reverse), payment status, overdue, customer balance, dashboard, revenue and customer reports, CSV | Figures match hand-calculated test data |
| 5 Quotations | Quotation CRUD, send/accept/reject, PDF, convert once, linking; customer-specific prices | Conversion keeps lines and prices; second convert rejected |
| 6 Automation | Recurring invoices (Jalali schedule), due/overdue reminders (in-app + optional email), invoice email (SMTP) and WhatsApp share, backup (manual, daily, retention, download, verify, restore with pre-restore backup), install/backup/restore scripts | Backup → wipe → restore gives identical data |
| 7 Release | Security review, full integration test run, Docker release build, offline check, README + Persian user guide | All PRD §15 acceptance criteria checked |
| 8 Global | English UI and documents, Gregorian calendar, several currencies, native downloads for Windows/macOS/Linux, GHCR image, CI and release workflows | See [phases/phase-8-global.md](phases/phase-8-global.md) |

Out of scope (PRD §3.3, §3.4): analytics beyond basic reports, accounting export, expenses, payment gateways, AI/OCR, multi-company, multi-user.

## Status (2026-11-24)
Phases 1–7 are done (Persian edition). Phase 8 (global release) is done. Reports: [phases/](phases/). The PRD §15 acceptance check is in [phases/phase-7-release.md](phases/phase-7-release.md).
