# Phase 8: Global release (English, Gregorian calendar, multi-currency, cross-platform install)

Requested on 2026-11-24: make the app usable outside Iran, installable on any OS quickly, and published on GitHub.

## Scope
| Area | Decision |
|---|---|
| Language | UI and documents in **English** or **Persian**. One app-wide setting (`language`: `en` / `fa`), also offered on the first-run screen. English is LTR, Persian is RTL. |
| Calendar | App-wide setting `calendar`: `Gregorian` or `Jalali`. Drives date display and the date picker, numbering year (`INV-2026-0001` / `INV-1405-0001`), recurring schedules, report months and PDFs. Storage stays Gregorian `DateOnly`. |
| Currency | Settings hold a list of enabled currencies (code, symbol, decimals, symbol position) and one default. Each invoice, quotation and recurring schedule picks a currency; it is frozen on the document. Dashboard and reports show one currency at a time (selector when more than one is used). Customer balance shows the default currency, and the customer page lists balances per currency. No exchange rates: amounts in different currencies are never added together. |
| Number format | Separators are app-wide settings. Persian uses Persian digits; English uses Latin digits. |
| Install | 1) Native, self-contained downloads for Windows x64, macOS (arm64, x64) and Linux x64. No .NET or Docker needed: unzip and run, the browser opens at `http://localhost:8090`. Data goes to the user's app-data folder. 2) Docker image on GHCR plus a one-file compose. One-line installers: `install.sh` (Linux/macOS), `install.ps1` (Windows). |
| Release | GitHub Actions: CI on push (tests + build); on a `v*` tag, build the native archives and the Docker image and attach them to a GitHub Release. |

Defaults: a **new** database starts in English, Gregorian, USD. An **existing** database keeps Persian, Jalali, IRR (the migration sets those values on the existing row).

## Design
- **Backend localization:** messages stay in Persian in code; `Localization/En.cs` maps each Persian message to English. The error handler translates `detail` and validation errors using the request's `Accept-Language` (sent by the UI). PDFs, CSV headers, notifications, e-mails and share text use the settings language. `NumberToWords` has an English version.
- **Calendar:** `Domain/Calendar/CalendarSystem` wraps .NET `PersianCalendar` or `GregorianCalendar` behind one API (parts, add months, month range, month names, format). `Jalali` remains for Persian-only helpers.
- **Frontend i18n:** a tiny in-house `t()` (no library). Keys are the existing Persian strings, `src/i18n/en/*.ts` hold English. Language and calendar are read from `localStorage` at start (server value wins and is cached); switching reloads the page, so module-level labels work. `npm run i18n:check` fails when a `t()` key has no English entry or a Persian literal is not wrapped.
- **Native mode:** when not in a container, the app listens on `127.0.0.1:8090` (`--port` / `APP_PORT` to change), stores data under the OS app-data folder, and opens the browser (`--no-browser` to skip).

## Done when
- Every screen works in English (LTR) and Persian (RTL); PDFs render in both.
- Gregorian and Jalali both work end to end; existing Jalali data is unchanged.
- Invoices in two currencies; reports filter by currency.
- Backend and frontend tests pass; `i18n:check` passes.
- A native Linux build runs from a plain folder; the Docker image builds and runs.
- The repo is on GitHub with CI and a release workflow.

## Result (2026-11-24)
**Built**
- **Backend**
  - `CalendarSystem`, used for numbering, recurring schedules, reports and PDFs.
  - `Loc` / `En`: about 190 server strings, with errors translated per request.
  - Currency list in settings and on each document, reports filtered by currency, customer balances per currency.
  - English amount in words, PDFs in both directions.
  - Global IBAN and card rules, international WhatsApp numbers, time zone defaults to the computer's own zone.
  - Native host mode, and sample data that follows the install's language.
- **Frontend**
  - `t()` with about 600 English entries and a guard test (`npm run i18n:check`).
  - LTR/RTL layout, Gregorian/Jalali date picker.
  - Currency pickers in the editors and reports.
  - Language, calendar and currency on the first-run screen; header language switch.
  - Currency list editor in settings.
- **Delivery**
  - `scripts/publish-native.sh`, `install/install.sh`, `install/install.ps1`.
  - Multi-arch Dockerfile, CI and release workflows, MIT licence, English README and user guide.

**Tests**
- 35 domain, 40 integration (3 new global tests) and 29 frontend tests pass.
- The Linux native build ran from a plain folder: first run in English with euros, invoice issued, PDF rendered, listening on 127.0.0.1 only.
- English invoice and quotation PDFs checked by eye.

**Upgrade of an existing install**
- Rehearsed on a copy of a real pre-phase-8 database, then applied to the running install. Language, calendar and currency stayed Persian / Jalali / IRR, and a pre-migration backup was made first.
- This found and fixed a bug: the pre-migration backup read the settings with the new model before the new columns existed. It now skips the informational `settings.json` in that case.

**Not verified here**
- The Windows and macOS builds and the release workflow. They run on GitHub Actions on the first `v*` tag.
- The GHCR image may start out private. If so, make it public under the repository's Packages settings.
