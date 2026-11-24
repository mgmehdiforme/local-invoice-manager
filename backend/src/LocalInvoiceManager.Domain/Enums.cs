namespace LocalInvoiceManager.Domain;

public enum InvoiceStatus { Draft, Issued, Cancelled }

public enum PaymentStatus { Unpaid, PartiallyPaid, Paid }

public enum DiscountType { None, Fixed, Percent }

public enum ProductKind { Product, Service }

public enum PaymentMethod { Cash, Card, BankTransfer, Cheque, Other }

public enum PaymentRecordStatus { Active, Reversed }

public enum QuotationStatus { Draft, Sent, Accepted, Rejected, Converted }

public enum RecurrenceFrequency { Weekly, Monthly, Quarterly, Yearly }

public enum SymbolPosition { Before, After }

public enum NotificationType { DueSoon, Overdue, InvoiceEmail, RecurringCreated, BackupFailed }

public enum NotificationChannel { InApp, Email }

public enum NotificationStatus { Sent, Failed }

public enum DocumentKind { Logo, Attachment }

/// <summary>Calendar used for display, numbering years, recurring schedules and report months. Storage is always Gregorian.</summary>
public enum CalendarKind { Gregorian, Jalali }
