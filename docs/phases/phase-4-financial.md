# Phase 4 — Financial management

**Status:** done (2026-11-24)

## Built
- **Payments:**
  - Record a payment (amount, Jalali date, method, reference, note). An invoice can take many partial payments.
  - A payment is rejected when the invoice is not issued, when it exceeds the remaining balance, when it is dated before the issue date, or when it has more decimals than the currency allows.
  - Each payment runs in one write transaction, so simultaneous payments cannot overpay.
  - **Reversal** needs a reason. The payment stays in the history marked "reversed" with its reason, and the invoice's paid amount and status are recalculated. Payments are never deleted.
  - Paid amount and status (Unpaid, PartiallyPaid, Paid) are stored on the invoice and refreshed inside the same transaction.
  - An invoice cannot be cancelled while it has active payments.
- **Dashboard:**
  - A row of figures: outstanding receivables, overdue (links to the filtered list), received this Jalali month (with the amount invoiced this month), and total invoiced (with draft and customer counts).
  - A bar showing the share collected.
  - Chart of the last 12 Jalali months, invoiced vs received.
  - "Needs follow-up" list of overdue invoices, and the latest invoices.
- **Reports:**
  - Revenue by Jalali month or by day for any range, with presets: this month, last month, this quarter, this year.
  - Revenue by customer: invoiced, paid, balance, with overdue highlighted.
  - Chart, a table view and CSV export. CSV files are UTF-8 with a BOM so Excel shows Persian correctly, and cells that look like formulas are neutralised.
- **Payments page:** search, date range, method filter, and an option to show reversed payments.

## Decisions
- Reports count **issued invoices only**. "Received" means active payments, grouped by payment date.
- The chart uses a fixed two-colour palette that passed the dataviz validator: invoiced `#0f8a74`, received `#5b6fd6`, all checks passing on white. It always has a legend and a hover tooltip, a single Y axis, and an optional table view.
- Overdue is never stored. It is calculated from the due date, the balance and the business "today" (Asia/Tehran).

## Tests
- Integration:
  - Partial and multiple payments; overpay, before-issue, zero-amount and precision checks.
  - Fully paid → no more payments; cannot cancel while payments are active.
  - Reversal needs a reason, stays in history, and cannot be repeated; list with and without reversed payments.
  - **8 simultaneous payments of 300,000 on a 1,000,000 invoice → exactly 3 accepted, paid amount 900,000.**
  - Payments only allowed on issued invoices.
  - Dashboard and reports match a hand calculation (drafts and cancelled invoices excluded, Aban 1405 bucket, tax, overdue) and the CSV starts with a BOM.
- Checked by eye: dashboard with sample history (strip, chart, follow-up list, latest invoices).
