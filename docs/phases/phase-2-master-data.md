# Phase 2 — Master data

**Status:** done (2026-11-24)

## Built
- **Customers API and screens**
  - Create, edit, archive and restore. Customers are never deleted, so invoice history stays intact.
  - Search by name, company, phone, mobile, email, economic code or national id. Search treats Arabic and Persian ی/ک the same, and Persian and Latin digits the same.
  - Each customer shows a balance, an overdue amount and an invoice count, all calculated in .NET with decimals.
  - Customer page shows contact details, a balance strip (invoiced / received / outstanding / overdue) and an invoice history placeholder, which is filled in Phase 3.
- **Products and services API and screens**
  - Type (product or service), SKU (unique, with a friendly duplicate message), unit (with suggestions), default price, taxable or tax-exempt, and archive/restore.
  - Archived items disappear from pickers. Issued invoices keep their own copy of each line.
- **Settings screen** (one form split into tabs, with one save button):
  - Business details and logo
  - Bank details (card number and IBAN checked)
  - Currency, with a live example of the format
  - Tax
  - Numbering, with a preview of the next invoice number
  - Invoice defaults: due days, payment terms, notes, print template, brand colour
  - Reminders and optional SMTP email
  - Backup schedule
  - Session timeout and password change
- **Shared UI pieces:** `DataTable`, `FormField` (shows server-side validation errors next to each field), `NumberInput` (accepts Persian digits and shows thousands separators), `Money`, `SearchInput`, `EmptyState`, `ConfirmDialog`, `StatusBadge`.

## Decisions
- Validation lives on the server only (FluentValidation). The UI shows the server's Persian messages next to the matching fields, so the rules exist in one place.
- List endpoints filter and sort in .NET after loading active rows. A single business has at most a few thousand customers or products, and this keeps Persian-aware search correct, which SQLite `LIKE` cannot do.
- Query parameters on list endpoints are nullable, so minimal-API `[AsParameters]` treats them as optional.

## Tests
- Integration: customer CRUD, Persian-normalized search (ي/ك and ۰-۹), archive and restore, 404, and balance; product CRUD, duplicate SKU, negative price rejected, archive.
- Checked by hand in the browser: login, customer list, settings tabs (RTL layout, LTR inputs for numbers).
