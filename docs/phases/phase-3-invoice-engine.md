# Phase 3 — Invoice engine

**Status:** done (2026-11-24)

## Built
- **Invoice API:**
  - Drafts: create, update and delete.
  - `POST /invoices/calculate` for live totals.
  - Issue, cancel (a reason is required, and invoices with active payments cannot be cancelled), duplicate.
  - List filters: status, payment status, unpaid, overdue, customer, Jalali date range (sent as Gregorian), and search by number or customer.
  - Customer invoice history and a defaults endpoint for new invoices.
- **Numbering:** `NumberingService` allocates the next number inside the issue transaction. SQLite `BEGIN IMMEDIATE` serializes writers, and a unique index on `InvoiceNumber` is the final guard. Drafts use no numbers, and numbers of cancelled invoices are never reused.
- **Snapshots at issue:** the customer, the seller (including bank details, tax label, accent colour and logo id) and the currency format are copied onto the invoice. Every line also stores its own description, unit and price. Changing or archiving a customer or product afterwards leaves issued invoices untouched. The files of replaced logos are kept while any issued invoice refers to them.
- **PDF (QuestPDF, Community licence):**
  - A4, right-to-left, with Vazirmatn embedded and system fonts ignored, so the output is identical on any machine.
  - Three templates: modern, classic, minimal.
  - Persian digits, Jalali dates, amount in words, and a `*` mark on tax-exempt lines.
  - Bank details, payment terms and notes.
  - "Page X of Y" in Persian digits.
  - Watermark for drafts and cancelled invoices.
  - The large header appears only on page 1, the table header repeats, and a row never splits across pages.
  - Opens in the browser for printing (`inline`) or downloads with `download=true`.
- **UI:**
  - Invoices list: status tabs, search, Jalali date range, and the sum of the visible rows.
  - Single-page editor:
    - Searchable customer picker, with "add new customer" inside it.
    - Line rows with a catalog picker, which uses the customer's own price when one is set.
    - Quantity (up to 3 decimals), unit, price, line discount (percent or fixed) and a taxable checkbox.
    - Invoice discount and tax rate; notes, payment terms and template.
    - A live A4 preview beside the form on wide screens, or behind an edit/preview tab on narrow ones.
    - Totals come from the server, re-requested after 300 ms of no typing.
    - A confirmation dialog before issuing shows the next number and the total.
    - A warning before leaving with unsaved changes.
  - Detail page: the A4 view as issued, a template switcher, print, PDF, duplicate and cancel, plus a summary panel.

## Decisions
- **The editor and A4 paper are shared** (`features/documents`), so quotations in Phase 5 reuse them.
- **Numbers such as phones, card numbers and IBANs are wrapped in Unicode left-to-right isolates** (LRI…PDI) in the PDF, and in `<bdi dir="ltr">` on screen. In the bidi algorithm, Persian digits count as "Arabic numbers" and a dash does not hold them together, so they printed reversed.
- An invoice must have at least one line and a total above zero before it can be issued.

## Tests
- Integration (9 for invoices):
  - Lifecycle and exact totals: 2,250,000 subtotal, 10% discount, tax charged only on the taxable share → 2,205,000.
  - Drafts use no numbers; issued invoices are read-only.
  - Cancelling needs a reason; cancelled numbers are not reused.
  - Draft edit and delete; per-field validation errors (`items[0].quantity` …); no items → cannot issue.
  - **12 concurrent issues → INV-1405-0001 … 0012 with no duplicates or gaps.**
  - Snapshots survive changes to the customer, product, seller and currency.
  - Duplicating creates a new draft; list filters, overdue and customer balance.
  - A PDF in all three templates with 45 lines of long mixed Persian/English text runs to several pages.
- Domain: Persian amount in words (8 cases).
- Checked by eye: PDF pages in each template, bank and phone direction, and the editor flow in the browser (pick product → live total → issue → detail page).
