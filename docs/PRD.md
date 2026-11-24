# Product Requirements Document (PRD)
## Local Invoice Manager

**Version:** 1.1  
**Status:** Final  
**Product Type:** Local-first Web Application  
**Target Platform:** Ubuntu Linux + Docker

---

# 1. Product Overview

## 1.1 Vision

Build a lightweight, professional, Persian-first invoice management application that runs locally on the user's computer.

The application should allow a single business owner to manage customers, products, invoices, quotations, payments, and financial reports without requiring cloud services, complex account management, or external infrastructure.

**Core Principle: Powerful functionality with minimal setup and operational complexity.**

## 1.2 Target User

- One business owner.
- Single seller/company profile.
- Single-user access protected by a password.
- No registration, roles, permissions, or multi-tenancy.

## 1.3 Deployment Environment

- Ubuntu Linux.
- Docker Compose.
- Localhost-only access.
- Application URL: `http://localhost:8080`.
- Persistent local storage.
- Core functionality must work offline.

---

# 2. Final Product Decisions

| Area | Decision |
|---|---|
| Application | Local Invoice Manager |
| Users | Single user |
| Authentication | Single password lock |
| Seller profiles | One |
| UI language | Persian |
| Layout | RTL |
| Calendar | Jalali only in UI |
| Internal dates | Gregorian |
| Currency | Configurable local currency |
| Backend | ASP.NET Core / .NET 10 LTS |
| Frontend | React + TypeScript |
| Database | SQLite |
| ORM | Entity Framework Core |
| Architecture | Modular Monolith |
| Deployment | Docker Compose |
| Host OS | Ubuntu Linux |
| Network | Localhost only |
| PDF design | Modern, minimalist, professional |
| External services | Optional only |

---

# 3. Functional Scope

## 3.1 MVP — Phase 1

### Invoice Management

- Automatic invoice numbering.
- Create, edit, save, and resume draft invoices.
- Issue and cancel invoices.
- Duplicate existing invoices.
- Live invoice preview.
- PDF export.
- Printing.
- Multiple invoice templates.
- Fixed discounts.
- Percentage discounts.
- Configurable tax.
- Payment terms.
- Invoice notes.
- Configurable due dates.

### Customer Management

- Create and edit customers.
- Customer contact information.
- Phone and email.
- Address.
- Tax identification.
- Customer notes.
- Customer invoice history.
- Customer outstanding balance.

### Product & Service Management

- Product/service catalog.
- Default prices.
- SKU.
- Unit of measurement.
- Product descriptions.
- Custom invoice line items.
- Inactive products must remain available in historical invoice snapshots.

### Payment Management

- Record invoice payments.
- Partial payments.
- Multiple payments per invoice.
- Payment history.
- Outstanding balance calculation.
- Payment statuses:
  - Unpaid
  - Partially Paid
  - Paid
- Automatic overdue detection.
- Payment reversal with history.

### Dashboard & Reports

- Total invoices.
- Total invoiced amount.
- Received payments.
- Outstanding receivables.
- Overdue invoices.
- Monthly revenue chart.
- Revenue reports by date range.
- Revenue reports by customer.
- Recent invoices.

### Security & Settings

- Single password authentication.
- Change password.
- Session timeout.
- Company profile settings.
- Logo and bank information.
- Currency configuration.
- Tax configuration.
- Invoice numbering configuration.

### Backup & Restore

- Manual backup.
- Automatic daily backup.
- Download backup.
- Restore from backup.
- Backup history.
- Backup integrity validation.

## 3.2 Phase 2

- Quotations.
- Convert quotations into invoices.
- Recurring invoices.
- Due-date reminders.
- Email invoice delivery.
- WhatsApp sharing or integration.
- Customer-specific pricing.
- Additional reporting features.

External delivery integrations must remain optional and must not affect offline invoice management.

## 3.3 Phase 3

- Advanced financial analytics.
- Accounting export.
- Expense management.
- Payment gateway integration.
- AI invoice assistant.
- OCR invoice extraction.
- Multi-company support, only if required in the future.

## 3.4 Explicitly Out of Scope

- Double-entry accounting.
- Payroll.
- Inventory and supply-chain management.
- Multi-user management.
- Roles and permissions.
- Native mobile applications.
- Microservices.
- Kubernetes.
- Redis.
- RabbitMQ.
- Mandatory cloud services.
- Mandatory online payment processing.

---

# 4. Business Rules

## 4.1 Invoice Lifecycle

Document status:

- Draft
- Issued
- Cancelled

Payment status:

- Unpaid
- Partially Paid
- Paid

Overdue is a calculated condition based on due date and remaining balance. It is not a separate document status.

## 4.2 Invoice Numbering

Example:

`INV-1405-0001`

Rules:

- Numbers are assigned when an invoice is issued.
- Draft invoices do not consume final invoice numbers.
- Issued numbers must never be reused.
- Number allocation must be transactional.
- Numbering can restart by Jalali year if configured.
- Database constraints must guarantee uniqueness.

## 4.3 Financial Calculations

All financial calculations must use decimal arithmetic.

Line total:

`Quantity × Unit Price - Line Discount`

Invoice subtotal:

`Sum of line totals`

Final total:

`Subtotal - Invoice Discount + Tax`

Rules:

- Tax calculation must be deterministic.
- Fixed and percentage discounts must be supported.
- Taxable and non-taxable lines must be supported.
- Rounding rules must be documented and consistent.
- The frontend must never be the source of truth for financial calculations.

## 4.4 Payments

- One invoice can have multiple payments.
- Active payments contribute to the paid amount.
- Reversed payments do not contribute to the paid amount.
- Payment amount cannot exceed the remaining balance.
- Payment operations must be transactional.
- Payment corrections must preserve audit history.

## 4.5 Historical Data

Issued invoices must preserve snapshots of:

- Customer name and contact details.
- Product/service description.
- Quantity and unit.
- Unit price.
- Discount.
- Tax.
- Currency.
- Seller information used at issue time.

Updating a customer or product must not change previously issued invoices.

## 4.6 Quotation Conversion

- An accepted quotation can be converted into an invoice.
- Conversion must preserve line items and pricing.
- Duplicate conversion must be prevented.
- Converted quotation and invoice must remain linked.

---

# 5. Calendar and Currency

## 5.1 Jalali Calendar

The application must use the Persian calendar throughout the user interface and generated PDF documents.

Requirements:

- Jalali date input.
- Jalali date display.
- Jalali date filtering.
- Jalali reports.
- Jalali invoice numbers.
- Correct date conversion and leap-year handling.

Internally, dates must use standard Gregorian date representations.

Date-only business values should use date-only semantics. Timestamps should use UTC.

Never store Jalali date strings as the canonical date representation.

## 5.2 Currency

Currency must be configurable.

Settings include:

- Currency code.
- Currency symbol.
- Decimal precision.
- Thousands separator.
- Decimal separator.
- Symbol placement.

Each invoice must retain its own currency configuration and historical financial values.

---

# 6. Database Design

## 6.1 Main Tables

| Table | Purpose |
|---|---|
| AppSettings | Application and seller configuration |
| Customers | Customer records |
| Products | Product and service catalog |
| Invoices | Invoice headers |
| InvoiceItems | Invoice line items |
| Payments | Payment records |
| Quotations | Quotation headers |
| QuotationItems | Quotation line items |
| RecurringInvoices | Recurring invoice configuration |
| NotificationLogs | Notification history |
| AuditLogs | Important operation history |
| DocumentFiles | Stored document metadata |

Seller information is stored in AppSettings because the application supports only one seller profile.

## 6.2 Main Relationships

- Customer 1:N Invoices.
- Customer 1:N Quotations.
- Invoice 1:N InvoiceItems.
- Invoice 1:N Payments.
- Product 1:N InvoiceItems, optional reference.
- Quotation 1:N QuotationItems.
- Quotation 0:1 Invoice after conversion.

## 6.3 Database Requirements

- SQLite.
- EF Core migrations.
- Foreign key enforcement.
- Appropriate indexes.
- Unique invoice number constraint.
- Transactional financial operations.
- WAL mode.
- Consistent backup strategy.
- Decimal-safe storage and conversion.
- No hard deletion of important financial records.

## 6.4 Important Indexes

- Customers: Name, Phone.
- Products: Name, SKU.
- Invoices: InvoiceNumber, CustomerId, IssueDate, DueDate, Status.
- Payments: InvoiceId, PaymentDate.
- Quotations: QuotationNumber, CustomerId, Status.
- AuditLogs: EntityType, EntityId, CreatedAtUtc.

---

# 7. API Architecture

All API endpoints use:

`/api/v1`

Main resources:

### Authentication

- GET `/auth/status`
- POST `/auth/setup`
- POST `/auth/login`
- POST `/auth/logout`
- POST `/auth/change-password`

### Customers

- GET `/customers`
- GET `/customers/{id}`
- POST `/customers`
- PUT `/customers/{id}`
- DELETE `/customers/{id}`
- GET `/customers/{id}/invoices`
- GET `/customers/{id}/balance`

### Products

- GET `/products`
- POST `/products`
- PUT `/products/{id}`
- DELETE `/products/{id}`

### Invoices

- GET `/invoices`
- GET `/invoices/{id}`
- POST `/invoices`
- PUT `/invoices/{id}`
- POST `/invoices/{id}/issue`
- POST `/invoices/{id}/cancel`
- POST `/invoices/{id}/duplicate`
- GET `/invoices/{id}/pdf`

### Payments

- POST `/invoices/{id}/payments`
- POST `/payments/{id}/reverse`
- GET `/payments`

### Quotations

- GET `/quotations`
- POST `/quotations`
- PUT `/quotations/{id}`
- POST `/quotations/{id}/accept`
- POST `/quotations/{id}/convert`

### Dashboard

- GET `/dashboard/summary`
- GET `/dashboard/revenue`
- GET `/dashboard/overdue`

### Settings and Backup

- GET `/settings`
- PUT `/settings`
- POST `/backup`
- GET `/backup`
- POST `/backup/restore`

The MVP must implement only the endpoints needed by the active development phase.

---

# 8. Technical Architecture

## 8.1 Backend

- .NET 10 LTS.
- ASP.NET Core Web API.
- Entity Framework Core.
- SQLite.
- FluentValidation or equivalent validation.
- Structured logging.
- Dependency injection.

## 8.2 Frontend

- React.
- TypeScript strict mode.
- Tailwind CSS.
- Reusable UI components.
- Feature-based organization.
- Persian RTL layout.
- Responsive design.

## 8.3 Architecture Pattern

Use a pragmatic Modular Monolith.

Projects:

- Domain
- Application
- Infrastructure
- Web

Avoid unnecessary abstractions and excessive architectural complexity.

## 8.4 PDF Generation

PDF requirements:

- A4 format.
- Persian RTL rendering.
- Embedded Persian fonts.
- Seller and customer information.
- Invoice line items.
- Tax and discount.
- Final total.
- Bank details.
- Notes and payment terms.
- Page numbering.
- Configurable brand accent.
- Multiple templates.

PDF generation must be tested early with real Persian text, mixed Persian/English content, numbers, and long descriptions.

---

# 9. Project Structure

```text
LocalInvoiceManager/
├── backend/
│   ├── LocalInvoiceManager.sln
│   ├── src/
│   │   ├── LocalInvoiceManager.Domain/
│   │   ├── LocalInvoiceManager.Application/
│   │   ├── LocalInvoiceManager.Infrastructure/
│   │   └── LocalInvoiceManager.Web/
│   └── tests/
│       ├── Domain.Tests/
│       ├── Application.Tests/
│       └── Integration.Tests/
│
├── frontend/
│   └── src/
│       ├── app/
│       ├── components/
│       ├── layouts/
│       ├── features/
│       │   ├── dashboard/
│       │   ├── invoices/
│       │   ├── customers/
│       │   ├── products/
│       │   ├── payments/
│       │   ├── quotations/
│       │   └── settings/
│       ├── services/
│       ├── hooks/
│       └── styles/
│
├── deployment/
│   ├── Dockerfile
│   ├── docker-compose.yml
│   └── .dockerignore
│
├── docs/
│   ├── PRD.md
│   ├── DATABASE.md
│   ├── API.md
│   ├── ARCHITECTURE.md
│   └── IMPLEMENTATION_PLAN.md
│
├── scripts/
│   ├── install.sh
│   ├── backup.sh
│   └── restore.sh
│
├── .env.example
├── .gitignore
└── README.md
```

---

# 10. UI/UX Requirements

## 10.1 Navigation

- Dashboard
- Invoices
- Create Invoice
- Quotations
- Customers
- Products & Services
- Payments
- Reports
- Settings

## 10.2 Dashboard

Display:

- Total invoices.
- Total revenue.
- Received payments.
- Outstanding receivables.
- Overdue invoices.
- Monthly revenue chart.
- Recent invoices.
- Create Invoice action.

## 10.3 Invoice Creation

The invoice creation workflow should be a single page:

1. Select customer.
2. Add products or custom line items.
3. Set quantity and price.
4. Apply discounts.
5. Configure tax.
6. Set due date.
7. Add notes and payment terms.
8. Preview invoice.
9. Save draft or issue invoice.

## 10.4 Visual Design

- Modern minimalist design.
- Persian RTL.
- Vazirmatn font.
- Light theme by default.
- Neutral colors.
- Configurable brand accent.
- Clear status indicators.
- Minimal animation.
- Responsive layout.

---

# 11. Security Requirements

- Password hashing.
- Single-user authentication.
- Secure session cookies.
- HttpOnly cookies.
- CSRF protection.
- Login attempt protection.
- Session timeout.
- Secure password change.
- Input validation.
- Safe file handling.
- No secrets in source control or logs.
- Localhost-only network binding.

Docker must bind the application to:

`127.0.0.1:8080`

Never expose the application to the LAN or public internet by default.

---

# 12. Backup and Restore

Backup archive example:

```text
invoice-backup-1405-08-24.zip
├── database.db
├── documents/
├── settings.json
└── manifest.json
```

Requirements:

- Consistent SQLite backup.
- Include associated documents.
- Include required settings.
- Archive integrity verification.
- Manual and scheduled backup.
- Backup retention policy.
- Restore validation.
- Automatic backup before restore.

Backups should support a separate external directory or disk.

---

# 13. Development Roadmap

## Phase 1 — Foundation

- Solution structure.
- Docker.
- SQLite.
- EF Core.
- Initial migrations.
- Authentication.
- Settings.
- Frontend setup.
- RTL foundation.

## Phase 2 — Master Data

- Customer management.
- Product management.
- Company settings.
- Currency and tax settings.

## Phase 3 — Invoice Engine

- Invoice creation.
- Draft and issue.
- Automatic numbering.
- Discounts and tax.
- Invoice duplication.
- Live preview.
- PDF export and printing.

## Phase 4 — Financial Management

- Payment registration.
- Partial payments.
- Payment reversal.
- Outstanding balance.
- Overdue detection.
- Dashboard.
- Reports.

## Phase 5 — Quotations

- Quotation management.
- Quotation conversion.
- Customer-specific pricing.

## Phase 6 — Automation and Release

- Recurring invoices.
- Notifications.
- Email and WhatsApp integrations.
- Backup and restore.
- Security review.
- Integration tests.
- Docker release.

Every phase must build successfully and pass its relevant tests before moving forward.

---

# 14. Non-Functional Requirements

| Requirement | Target |
|---|---|
| Startup | Simple Docker Compose command |
| Network | Localhost only |
| Offline | Core features work offline |
| Data persistence | Docker volume |
| Performance | Fast local interactions |
| Security | Password protected |
| Reliability | Transactional financial operations |
| Backup | Verified backup and restore |
| UI | Persian RTL |
| Calendar | Jalali |
| PDF | Correct Persian rendering |
| Maintainability | Modular codebase |
| Testing | Unit and integration tests |

---

# 15. Acceptance Criteria

The MVP is complete when:

- Application runs on Ubuntu using Docker Compose.
- Access is limited to localhost.
- Password protection works.
- Data survives application restarts.
- Customers can be created and managed.
- Products and services can be managed.
- Invoices can be created and saved as drafts.
- Issued invoice numbers are unique.
- Discounts and taxes calculate correctly.
- Historical invoice snapshots remain unchanged.
- PDF output displays Persian text and RTL correctly.
- Invoices can be printed.
- Multiple and partial payments work.
- Outstanding balances are accurate.
- Overdue invoices are identified correctly.
- Dashboard displays accurate financial figures.
- Basic revenue reports work.
- Backup and restore are tested.
- Core functionality works without internet access.

---

# 16. Claude Code Implementation Policy

Claude Code must:

1. Read the complete PRD before starting.
2. Produce the architecture and database design first.
3. Create documentation before implementation.
4. Work incrementally, one phase at a time.
5. Avoid implementing future features prematurely.
6. Write meaningful tests.
7. Run builds and tests after each phase.
8. Fix errors before continuing.
9. Update documentation after changes.
10. Request approval at each major milestone.

**Do not generate the entire application in one attempt.**

The objective is a reliable, maintainable, production-quality local application rather than a large codebase generated without validation.

---

# 17. Final Product Principle

Local Invoice Manager should feel like a small, polished business application.

It should not feel like a complicated accounting ERP.

The user should be able to open the application, create an invoice, send or print it, record payments, and understand their financial position with minimal effort.

**Simple to operate. Reliable with financial data. Independent from cloud services.**
