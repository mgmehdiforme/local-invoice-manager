# API Reference

Base path: `/api/v1`. JSON, camelCase. Dates `yyyy-MM-dd` (Gregorian), timestamps ISO-8601 UTC.

## Conventions

- **Auth:** every endpoint except `GET /auth/status`, `GET /auth/csrf`, `POST /auth/setup`, `POST /auth/login` needs the session cookie (`lim.session`, HttpOnly, SameSite=Strict).
- **CSRF:** every non-GET request needs header `X-XSRF-TOKEN` with the value of cookie `XSRF-TOKEN` (issued by `GET /auth/csrf` and on login).
- **Errors:** Problem Details: `{ "status": 400, "title": "<code>", "detail": "<Persian message>", "code": "invoice.not_draft", "errors": { "items[0].quantity": ["…"] } }`.
  Status codes: 400 business rule or validation, 401 not signed in, 404 not found, 409 duplicate value (unique index), 429 locked out or rate-limited, 503 restore in progress, 500 unexpected (generic message).
- **Language:** send `Accept-Language: en` or `fa` (exactly) to get `detail` and validation messages in that language; otherwise the app language setting is used. PDFs, CSV headers, notifications and e-mails use the app setting.
- **Currency:** documents carry their own `currency`; aggregate endpoints accept `?currency=CODE` (default: the default currency) and never mix currencies.
- **Lists:** `?search=&page=1&pageSize=20&sort=` → `{ items, page, pageSize, total }`.

## Auth
| Method | Path | Body / notes |
|---|---|---|
| GET | `/auth/status` | `{ isSetUp, isAuthenticated, sessionTimeoutMinutes, language, calendar }` |
| GET | `/auth/csrf` | sets `XSRF-TOKEN` cookie |
| POST | `/auth/setup` | `{ password, language?, calendar?, currencyCode? }` — only while no password exists; Persian also sets Persian separators, VAT label and 10% |
| POST | `/auth/login` | `{ password }` — 5 failures → 5-min lockout (doubles each time, max 1 h); also rate-limited |
| POST | `/auth/logout` | |
| POST | `/auth/change-password` | `{ currentPassword, newPassword }` — ends other sessions |

Password rules: at least 8 characters.

## Settings
| GET | `/settings` | all settings except secrets (`smtpPasswordSet: bool`) |
|---|---|---|
| PUT | `/settings` | full settings object incl. `language`, `calendar`, `currencies[]` (`{code, symbol, decimalPlaces, symbolPosition}`, must include `currencyCode`); `smtpPassword` only when changing |
| PUT | `/settings/preferences` | `{ language, calendar }` — header switch |
| POST | `/settings/logo` | multipart `file` (PNG/JPEG/WebP, ≤ 1 MB, checked by magic bytes) |
| DELETE | `/settings/logo` | |
| GET | `/settings/logo` | image |
| POST | `/settings/test-email` | `{ to }` sends a test email |

## Customers
| GET | `/customers?search=&includeInactive=` | list with `balance` |
|---|---|---|
| GET | `/customers/{id}` | |
| POST | `/customers` | create |
| PUT | `/customers/{id}` | update |
| DELETE | `/customers/{id}` | archive (`isActive=false`) |
| POST | `/customers/{id}/restore` | un-archive |
| GET | `/customers/{id}/invoices` | invoice history |
| GET | `/customers/{id}/balance` | `{ totalInvoiced, totalPaid, balance, overdueAmount, …, byCurrency[] }` (top level = default currency) |
| GET/PUT | `/customers/{id}/prices` | customer-specific prices (phase 5) |

## Products
| GET | `/products?search=&includeInactive=&customerId=` | `customerId` adds `customerPrice` |
|---|---|---|
| GET | `/products/{id}` | |
| POST | `/products` | |
| PUT | `/products/{id}` | |
| DELETE | `/products/{id}` | archive |
| POST | `/products/{id}/restore` | |

## Invoices
| GET | `/invoices?search=&status=&paymentStatus=&overdue=&customerId=&from=&to=` | |
|---|---|---|
| GET | `/invoices/defaults` | issue date, due date, tax rate, terms, notes, template, `currencyCode` for a new form |
| GET | `/invoices/{id}` | with items, payments, computed `balance`, `isOverdue` |
| POST | `/invoices` | create draft (`currencyCode` optional, one of the enabled currencies) |
| PUT | `/invoices/{id}` | update draft only |
| DELETE | `/invoices/{id}` | delete draft only |
| POST | `/invoices/calculate` | totals for a draft payload (live preview) |
| POST | `/invoices/{id}/issue` | assigns number, takes snapshots |
| POST | `/invoices/{id}/cancel` | `{ reason }`; not allowed with active payments |
| POST | `/invoices/{id}/duplicate` | new draft copy |
| GET | `/invoices/{id}/pdf?template=&download=` | A4 PDF |
| POST | `/invoices/{id}/email` | `{ to, subject?, message? }` (SMTP must be set) |
| GET | `/invoices/{id}/share` | `{ whatsappUrl, text }` |

## Payments
| POST | `/invoices/{id}/payments` | `{ amount, paymentDate, method, reference?, notes? }` |
|---|---|---|
| POST | `/payments/{id}/reverse` | `{ reason }` |
| GET | `/payments?from=&to=&customerId=&method=` | |

## Quotations
| GET | `/quotations?search=&status=` | |
|---|---|---|
| GET | `/quotations/{id}` | |
| POST | `/quotations` | |
| PUT | `/quotations/{id}` | while Draft/Sent |
| DELETE | `/quotations/{id}` | Draft only |
| POST | `/quotations/{id}/send` | mark as sent |
| POST | `/quotations/{id}/accept` | |
| POST | `/quotations/{id}/reject` | |
| POST | `/quotations/{id}/convert` | creates draft invoice; once only |
| POST | `/quotations/{id}/duplicate` | |
| GET | `/quotations/{id}/pdf` | |

## Recurring invoices
| GET/POST | `/recurring-invoices` | |
|---|---|---|
| GET/PUT/DELETE | `/recurring-invoices/{id}` | |
| POST | `/recurring-invoices/{id}/run-now` | generate the next invoice immediately |
| POST | `/recurring-invoices/run-due` | generate everything that is due (normally done by the background job) |

## Dashboard and reports
| GET | `/dashboard/summary?currency=` | counts and amounts |
|---|---|---|
| GET | `/dashboard/revenue?months=12&currency=` | per month (app calendar): invoiced, received |
| GET | `/dashboard/overdue` | overdue invoices |
| GET | `/reports/revenue?from=&to=&groupBy=month|day&currency=` | |
| GET | `/reports/customers?from=&to=&currency=` | per customer: invoiced, paid, balance |
| GET | `/reports/revenue.csv`, `/reports/customers.csv` | UTF-8 CSV with BOM (Excel-friendly) |

## Notifications
| GET | `/notifications?unreadOnly=` | |
|---|---|---|
| GET | `/notifications/count` | `{ unread }` (does not extend the session) |
| POST | `/notifications/{id}/read`, `/notifications/read-all` | |
| POST | `/notifications/run-reminders` | run today's reminder check now |

## Backup
| GET | `/backup` | `{ items, lastScheduled }` history read from the backup folder |
|---|---|---|
| POST | `/backup` | create manual backup |
| GET | `/backup/{fileName}` | download |
| POST | `/backup/{fileName}/verify` | integrity report for a stored backup |
| POST | `/backup/verify` | multipart file → integrity report |
| POST | `/backup/restore` | multipart file; auto pre-restore backup |
| POST | `/backup/restore-stored` | `{ fileName }` of a stored backup; auto pre-restore backup |
| DELETE | `/backup/{fileName}` | |

## Health
`GET /health` (no auth) → `200 Healthy` when the database answers.
