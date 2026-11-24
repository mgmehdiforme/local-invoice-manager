# Database Design

SQLite 3, accessed through EF Core 10 with migrations. Pragmas set on every connection: `journal_mode=WAL`, `foreign_keys=ON`, `busy_timeout=5000`, `synchronous=NORMAL`.

## Conventions

- Primary keys: `INTEGER` auto-increment (`Id`).
- Money and quantities: .NET `decimal`, stored as `TEXT` (exact). Aggregations are done in .NET, not SQL.
- Business dates (issue date, due date, payment date): `DateOnly`, stored as `TEXT` `yyyy-MM-dd` (Gregorian).
- Timestamps: `DateTime` in UTC, column names end in `Utc`.
- Enums: stored as `TEXT` names (readable, safe to reorder).
- Financial records (issued invoices, payments) are never hard-deleted. Customers and products are archived (`IsActive = 0`). Only draft invoices and draft quotations can be deleted.

## Entity relationship overview

```
AppSettings (1 row)
Customers 1──N Invoices 1──N InvoiceItems N──0..1 Products
              │         1──N Payments
              │         N──0..1 Quotations (SourceQuotationId, unique)
              │         N──0..1 RecurringInvoices
Customers 1──N Quotations 1──N QuotationItems N──0..1 Products
Customers 1──N CustomerPrices N──1 Products
Customers 1──N RecurringInvoices
NotificationLogs N──0..1 Invoices
AuditLogs, DocumentFiles, NumberSequences (standalone)
```

## Tables

### AppSettings (single row, Id = 1)
| Column | Type | Notes |
|---|---|---|
| CompanyName, LegalName, EconomicCode, NationalId, RegistrationNumber | TEXT | Seller identity |
| Phone, Email, Website, Address, PostalCode | TEXT | |
| LogoFileId | INTEGER? | → DocumentFiles |
| BankName, BankAccountNumber, BankCardNumber, BankIban, BankAccountHolder | TEXT | Shown on invoices |
| Language | TEXT | `en` / `fa` (new databases `en`; databases from before phase 8 `fa`) |
| Calendar | TEXT | `Gregorian` / `Jalali` |
| CurrencyCode, CurrencySymbol | TEXT | default currency (new databases `USD`, `$`) |
| CurrenciesJson | TEXT | JSON list of enabled currencies `{code, symbol, decimalPlaces, symbolPosition}` |
| CurrencyDecimalPlaces | INTEGER | 0–4, default 0 |
| ThousandsSeparator, DecimalSeparator | TEXT | default `٬` and `٫` |
| CurrencySymbolPosition | TEXT | `Before` / `After` |
| DefaultTaxRate | TEXT (decimal) | percent, default 10 |
| TaxLabel | TEXT | default `مالیات بر ارزش افزوده` |
| InvoicePrefix, QuotationPrefix | TEXT | `INV`, `QUO` |
| NumberPadding | INTEGER | 4 → `0001` |
| ResetNumberingYearly | INTEGER (bool) | include the year (app calendar) and restart each year |
| DefaultDueDays | INTEGER | 30 |
| DefaultPaymentTerms, DefaultNotes | TEXT | |
| DefaultTemplate | TEXT | `modern` / `classic` / `minimal` |
| AccentColor | TEXT | hex, default `#0f766e` |
| PasswordHash | TEXT? | null until first setup |
| SecurityStamp | TEXT | changes on password change → old sessions end |
| SessionTimeoutMinutes | INTEGER | 30 |
| FailedLoginCount, LockoutUntilUtc | INTEGER, TEXT? | login protection |
| BackupEnabled, BackupHour, BackupRetentionCount | INTEGER | daily backup at hour, keep N |
| RemindersEnabled, ReminderDaysBefore, EmailRemindersToCustomers | INTEGER | |
| SmtpHost, SmtpPort, SmtpUser, SmtpPasswordProtected, SmtpUseSsl, SmtpFromEmail, SmtpFromName | | password encrypted with ASP.NET Data Protection |
| UpdatedAtUtc | TEXT | |

### Customers
`Id, Name*, CompanyName, Phone, Mobile, Email, Address, City, PostalCode, TaxId (economic code), NationalId, Notes, IsActive, CreatedAtUtc, UpdatedAtUtc`
Indexes: `Name`, `Phone`, `Mobile`.

### Products
`Id, Name*, Sku (unique when not null), Kind (Product/Service), Unit*, Description, DefaultPrice, IsTaxable, IsActive, CreatedAtUtc, UpdatedAtUtc`
Indexes: `Name`, unique `Sku`.

### CustomerPrices (phase 5)
`Id, CustomerId → Customers, ProductId → Products, Price, UpdatedAtUtc`; unique `(CustomerId, ProductId)`.

### Invoices
| Column | Notes |
|---|---|
| Id | |
| InvoiceNumber | TEXT?, **unique**; null while Draft |
| Status | `Draft` / `Issued` / `Cancelled` |
| CustomerId | → Customers (restrict delete) |
| IssueDate, DueDate | DateOnly; DueDate nullable |
| CustomerName, CustomerSnapshotJson | snapshot (name kept as a column for search) |
| SellerSnapshotJson | seller info at issue time |
| CurrencyCode, CurrencySymbol, CurrencyDecimalPlaces, ThousandsSeparator, DecimalSeparator, CurrencySymbolPosition | currency snapshot |
| DiscountType, DiscountValue | `None` / `Fixed` / `Percent` |
| TaxRate | percent, applied to taxable lines |
| Subtotal, DiscountAmount, TaxableAmount, TaxAmount, Total | calculated by `InvoiceCalculator` |
| PaidAmount | sum of active payments, updated in the payment transaction |
| PaymentStatus | `Unpaid` / `PartiallyPaid` / `Paid`, updated with PaidAmount |
| Notes, PaymentTerms, TemplateKey | |
| SourceQuotationId | → Quotations, **unique** (one invoice per quotation) |
| RecurringInvoiceId | → RecurringInvoices |
| DuplicatedFromId | → Invoices |
| IssuedAtUtc, CancelledAtUtc, CancellationReason, CreatedAtUtc, UpdatedAtUtc | |

Indexes: `InvoiceNumber` (unique), `CustomerId`, `IssueDate`, `DueDate`, `Status`.
Overdue is **not stored**: `Status = Issued AND Total - PaidAmount > 0 AND DueDate < today`.

### InvoiceItems
`Id, InvoiceId → Invoices (cascade), LineNo, ProductId? → Products (set null), Description*, Sku, Unit, Quantity, UnitPrice, DiscountType, DiscountValue, DiscountAmount, IsTaxable, LineTotal`

### Payments
`Id, InvoiceId → Invoices (restrict), Amount, PaymentDate, Method (Cash/Card/BankTransfer/Cheque/Other), Reference, Notes, Status (Active/Reversed), ReversedAtUtc, ReversalReason, CreatedAtUtc`
Indexes: `InvoiceId`, `PaymentDate`.

### Quotations / QuotationItems
Same money columns as invoices, plus `QuotationNumber` (unique, assigned at creation), `Status` (`Draft/Sent/Accepted/Rejected/Converted`), `ValidUntil`, `ConvertedInvoiceId`, `AcceptedAtUtc`. Items mirror `InvoiceItems`.
Indexes: `QuotationNumber` (unique), `CustomerId`, `Status`.

### RecurringInvoices
`Id, Name, CustomerId, Frequency (Weekly/Monthly/Quarterly/Yearly), StartDate, EndDate?, NextRunDate, AutoIssue, IsActive, DueDays, TemplateJson (lines, discount, tax, notes, terms), LastRunAtUtc, LastInvoiceId, CreatedAtUtc, UpdatedAtUtc`
Monthly/quarterly/yearly steps follow the **app calendar** (e.g. the 1st of every Jalali or Gregorian month). The template may name a currency.

### NotificationLogs
`Id, Type (DueSoon/Overdue/InvoiceEmail/RecurringCreated/BackupFailed), Channel (InApp/Email), InvoiceId?, Title, Message, Recipient, Status (Sent/Failed), Error, IsRead, DedupKey (unique, nullable), CreatedAtUtc`
`DedupKey` stops the daily job from sending the same reminder twice (e.g. `overdue:42:2026-11-24`).

### AuditLogs
`Id, EntityType, EntityId, Action, Details (JSON), CreatedAtUtc`
Index `(EntityType, EntityId)`, `CreatedAtUtc`. Written for: issue, cancel, payment add/reverse, quotation convert, settings change, password change, restore.

### DocumentFiles
`Id, Kind (Logo/Attachment), FileName, ContentType, SizeBytes, StoragePath (relative to /data/documents), Sha256, CreatedAtUtc`

### NumberSequences
`Key (PK, e.g. "INV-2026", "INV-1405" or "INV"), NextValue`
Allocation (inside the issue transaction): read row → format number → increment → save. SQLite allows one writer at a time, and the unique index on `InvoiceNumber` is the final guard.

## Calculation rules (InvoiceCalculator)

For currency precision `p` and `R(x) = round(x, p, AwayFromZero)`:

1. Line gross = `Quantity × UnitPrice`.
2. Line discount = Fixed: `min(value, gross)`; Percent: `R(gross × value / 100)`.
3. Line total = `R(gross − line discount)`.
4. Subtotal = Σ line totals.
5. Invoice discount = Fixed: `min(value, subtotal)`; Percent: `R(subtotal × value / 100)`.
6. Taxable base = Σ taxable line totals − `R(invoice discount × taxable share)`, where taxable share = taxable lines ÷ subtotal (the discount is spread pro rata).
7. Tax = `R(taxable base × TaxRate / 100)`.
8. Total = Subtotal − invoice discount + Tax.
9. Balance = Total − PaidAmount. Payment status: 0 paid → Unpaid; 0 < paid < Total → PartiallyPaid; paid ≥ Total → Paid.

Quantities allow up to 3 decimals. Percentages are 0–100. No negative amounts.

## Migrations

`backend/src/LocalInvoiceManager.Infrastructure/Persistence/Migrations`. The app applies pending migrations at startup, after an automatic pre-migration backup when the DB already exists.
