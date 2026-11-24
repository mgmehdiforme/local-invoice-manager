# Phase 5 — Quotations and customer-specific prices

**Status:** done (2026-11-24)

## Built
- **Quotations:**
  - Create and edit. A quotation gets its number (`QUO-1405-0001`) when it is created, inside a write transaction.
  - Statuses: Draft → Sent → Accepted / Rejected → Converted.
  - Can be edited while Draft or Sent; deleted only while Draft.
  - "Expired" is calculated (valid-until date in the past while the quotation is still open).
  - Duplicate (new number), and a PDF titled "پیش‌فاکتور" with "valid until" and no bank block.
- **Conversion:**
  - Only an accepted quotation can be converted.
  - It creates a draft invoice with the same lines, prices, discounts, tax, notes and template; issue date is today, and the due date comes from settings.
  - Both documents are linked (`Invoices.SourceQuotationId` ↔ `Quotations.ConvertedInvoiceId`), and each links to the other in the UI.
  - Conversion happens only once: the status check runs inside a transaction, and a unique index on `SourceQuotationId` backs it up.
- **Customer-specific prices:**
  - `GET/PUT /customers/{id}/prices` (replace all; no negative prices, no duplicate products).
  - Edited on the customer page.
  - The product picker in the invoice and quotation editor uses the customer's price and labels it.
- **UI:**
  - Quotations list with status tabs and search.
  - Editor: the shared document editor, with "valid until" defaulting to issue date + 14 days.
  - Detail page: convert, open the linked invoice, edit, mark sent, accepted or rejected, print, PDF, duplicate, delete.

## Tests
- Integration:
  - Full lifecycle: numbering; cannot convert before accepting; editable while Sent and locked after Accepted.
  - **Two simultaneous conversions → exactly one succeeds.** Totals and lines are kept; both documents are linked; a third conversion is rejected; PDF works; duplicate gets the next number.
  - A rejected quotation cannot be accepted; only drafts can be deleted.
  - Customer prices: saved, shown through `products?customerId=`, negative prices rejected, cleared.
