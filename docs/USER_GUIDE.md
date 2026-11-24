# User guide

Invoice Manager runs only on your computer. Your data never leaves it, and daily work needs no internet connection. (راهنمای فارسی: [USER_GUIDE.fa.md](USER_GUIDE.fa.md))

## 1. First run
1. Install it (see the [README](../README.md)) and open **http://localhost:8090**.
2. Choose **language** (English or Persian), **calendar** (Gregorian or Jalali) and **default currency**, then set a **password** of at least 8 characters. The password locks the app.
3. Open **Settings** and complete:
   - **Business:** name, IDs, address, phone, logo. Printed at the top of every invoice.
   - **Bank details:** account, IBAN, card. Printed at the bottom so customers know where to pay.
   - **Currency and tax:** the currencies you invoice in (add as many as you need; one is the default), separators, default tax rate and its label (e.g. VAT).
   - **Invoice:** number prefix, yearly restart, default payment term, print template and brand color.
   - **Language and calendar:** can be changed at any time. The **EN / فا** switch in the header changes the language too.

Want to look around first? On an empty install run `invoice-manager seed-demo` for fictional sample data.

## 2. Invoices
- **New invoice** → pick the customer, add lines. The 🔍 button next to a line picks a product or service; you can also type any description. Set quantity, price, line discount and whether the line is taxable.
- **Currency:** when more than one currency is enabled, choose it in the invoice form. The invoice keeps that currency for good.
- **Live preview:** the A4 page next to the form (or the "Preview" tab on small screens). All totals are calculated by the app's server.
- **Save draft:** drafts have no number and can be edited any time.
- **Issue:** the invoice gets its final number, e.g. `INV-2026-0001`, and can no longer be edited. Customer and seller details are frozen on it; later changes to the customer do not alter issued invoices.
- **Print / PDF:** three templates (modern, classic, minimal). The PDF follows the app language.
- **Send:** WhatsApp (a ready message; attach the PDF yourself), e-mail with the PDF attached (needs SMTP settings and internet), or copy the message text.
- **Duplicate** makes a new draft from an existing invoice. **Cancel** an issued invoice with a reason; its number is never reused. An invoice with payments cannot be cancelled until the payments are reversed.

## 3. Payments
- On an issued invoice, **Record payment**: amount, date, method (cash, card, bank transfer, cheque…).
- Several partial payments are allowed; the status changes automatically: unpaid → partly paid → paid. A payment cannot exceed the balance.
- **Reverse** a payment (e.g. a bounced cheque): it is kept in the history with the reason, and the balance goes back up.
- Unpaid invoices past their due date show a red **Overdue** label.

## 4. Quotations
Create a quotation, print or send it, mark it **Sent**. When the customer agrees, mark it **Accepted** and click **Convert to invoice**: a draft invoice with the same lines, prices and currency is created. Conversion works once; the two documents are linked.

## 5. Customers, products and special prices
Customers and products are **archived**, never deleted, so old invoices stay intact. A customer's page shows the balance (per currency when they were invoiced in more than one), past invoices and **special prices**, which fill in automatically on new invoices for that customer.

## 6. Recurring invoices
For monthly, quarterly or yearly contracts: create a schedule with start date, frequency, currency and lines. Months follow the app calendar. Choose whether each run creates a **draft** for you to check or **issues automatically**. If the computer was off, missed runs are created when it starts again.

## 7. Dashboard, reports and notifications
- **Dashboard:** owed by customers, overdue, received this month, 12-month chart and invoices that need follow-up.
- **Reports:** revenue by month or day for any date range, and revenue per customer; export to CSV (opens in Excel).
- With several currencies, a currency selector appears: figures are shown for one currency at a time and never added across currencies.
- **Bell:** daily reminders for invoices due soon or overdue, recurring invoice results, and backup failures.

## 8. Backups (important)
- The app backs up **every day** automatically. Time and number of copies are in **Settings › Backup**; **Back up now** makes a manual copy.
- Copy a backup to a USB disk or another computer from time to time.
- **Restore** from a stored copy or a file. The file is checked first, and a safety backup of the current data is made before anything is replaced.
- **Verify** checks a backup without restoring it.

## 9. Security
- Only this computer can open the app; other devices on the network cannot.
- The app locks after a period without use (30 minutes by default). Change the password in **Settings › Security**; other open sessions are signed out.
- Five wrong passwords lock sign-in for a few minutes.
- Forgot the password? Your data is kept: run `invoice-manager reset-password` (Docker: `docker exec invoice-manager dotnet LocalInvoiceManager.Web.dll reset-password`) and set a new one.

## 10. Questions
- **The app does not open:** start it again (`invoice-manager`, the Start menu shortcut, or `docker start invoice-manager`).
- **Numbers restarted at 1 in the new year?** With "restart every year" on, numbers start again each year in the app calendar, e.g. `INV-2027-0001`.
- **How do I type dates?** Click the calendar or type `2026-11-24` (Gregorian) or `1405/08/24` (Jalali).
