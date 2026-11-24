namespace LocalInvoiceManager.Domain.Entities;

public class RecurringInvoice
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public RecurrenceFrequency Frequency { get; set; } = RecurrenceFrequency.Monthly;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public DateOnly NextRunDate { get; set; }
    public bool AutoIssue { get; set; }
    public bool IsActive { get; set; } = true;
    public int DueDays { get; set; } = 30;
    /// <summary>JSON of the invoice template: lines, discount, tax rate, notes, payment terms.</summary>
    public string TemplateJson { get; set; } = "{}";
    public DateTime? LastRunAtUtc { get; set; }
    public int? LastInvoiceId { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>Next run date. Months follow the app calendar (e.g. "1st of every Jalali month").</summary>
    public DateOnly Advance(DateOnly from, Calendar.CalendarSystem calendar) => Frequency switch
    {
        RecurrenceFrequency.Weekly => from.AddDays(7),
        RecurrenceFrequency.Monthly => calendar.AddMonths(from, 1),
        RecurrenceFrequency.Quarterly => calendar.AddMonths(from, 3),
        RecurrenceFrequency.Yearly => calendar.AddMonths(from, 12),
        _ => throw new ArgumentOutOfRangeException(),
    };
}

public class NotificationLog
{
    public int Id { get; set; }
    public NotificationType Type { get; set; }
    public NotificationChannel Channel { get; set; }
    public int? InvoiceId { get; set; }
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public string? Recipient { get; set; }
    public NotificationStatus Status { get; set; } = NotificationStatus.Sent;
    public string? Error { get; set; }
    public bool IsRead { get; set; }
    public string? DedupKey { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class AuditLog
{
    public long Id { get; set; }
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string Action { get; set; } = "";
    public string? Details { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public class DocumentFile
{
    public int Id { get; set; }
    public DocumentKind Kind { get; set; }
    public string FileName { get; set; } = "";
    public string ContentType { get; set; } = "";
    public long SizeBytes { get; set; }
    public string StoragePath { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }
}

public class NumberSequence
{
    public string Key { get; set; } = "";
    public int NextValue { get; set; } = 1;
}
