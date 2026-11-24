# Phase 7 — Release

**Status:** done (2026-11-24)

## Done in this phase
- Security review ([SECURITY.md](../SECURITY.md)). One fix: a restore now extracts only files listed in the manifest (and hash-checked).
- NuGet lock files (`packages.lock.json`) with `--locked-mode` restore in Docker; `package-lock.json` with `npm ci`.
- Business time zone clock (`LocalNow`), so backup file names and job hours use Tehran time.
- A preview fix: empty bank fields no longer show bare labels.
- Docs: README, Persian user guide, backup guide, API update.

## Verification run
| Check | Result |
|---|---|
| `dotnet build` (warnings = errors) | ✅ |
| Backend tests | ✅ 28 domain + 37 integration |
| Frontend tests / production build | ✅ 17 tests, build OK |
| Bundle makes no external requests (CDN, fonts) | ✅ only text inside library error messages |
| Docker image builds (locked restore), non-root, health check | ✅ 544 MB, `healthy` |
| End-to-end in a throwaway container (port 8095, own volume): setup → settings → customer → product → draft → issue `INV-1405-0001` → PDF → partial payment → dashboard → backup | ✅ |
| Data survives a container restart | ✅ |
| Command-line `verify` / `backup` / `restore` (with automatic pre-restore backup) | ✅ |
| **Offline:** container with `--network none` starts, is healthy, and makes backups | ✅ |
| Real container redeployed on `127.0.0.1:8090` (no password set; first visit shows setup) | ✅ |

## PRD §15 acceptance criteria
| Criterion | Evidence |
|---|---|
| Runs on Ubuntu with Docker Compose | `scripts/install.sh`, container healthy |
| Access limited to localhost | compose `127.0.0.1:8090`, host filtering test |
| Password protection works | AuthTests (setup, login, lockout, XSRF, session end) |
| Data survives restarts | E2E restart check |
| Customers / products managed | MasterDataTests + UI |
| Invoices saved as drafts | InvoiceTests |
| Issued numbers unique | 12 concurrent issues test |
| Discounts and taxes correct | Domain calculator tests + hand-checked integration totals |
| Historical snapshots unchanged | Snapshot test (customer, product, seller, currency changed afterwards) |
| PDF Persian/RTL correct | PDF checked by eye in all templates; multi-page, mixed text, LTR isolates for numbers |
| Invoices can be printed | Inline PDF opened from the "Print" button |
| Multiple and partial payments | PaymentAndReportTests (including concurrency) |
| Outstanding balances accurate | Dashboard/report hand-calculation test |
| Overdue identified correctly | Domain overdue test, filter and dashboard tests |
| Dashboard figures accurate | Dashboard test |
| Basic revenue reports | Revenue/customer report tests + CSV |
| Backup and restore tested | Round-trip, tamper and CLI tests |
| Core works offline | `--network none` container run |

## Known limits
- `Secure` cookie flag only behind HTTPS (see SECURITY.md).
- Sending email needs internet access and an SMTP account. Everything else works offline.
- QuestPDF Community licence: free under USD 1M yearly revenue.
