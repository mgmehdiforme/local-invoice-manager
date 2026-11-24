using LocalInvoiceManager.Domain.Localization;
using System.Net;
using LocalInvoiceManager.Application.Abstractions;
using LocalInvoiceManager.Application.Common;
using LocalInvoiceManager.Application.Documents;
using LocalInvoiceManager.Application.Invoices;
using LocalInvoiceManager.Application.Settings;
using LocalInvoiceManager.Domain;
using LocalInvoiceManager.Domain.Calendar;
using LocalInvoiceManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LocalInvoiceManager.Application.Notifications;

public sealed record NotificationDto(int Id, NotificationType Type, NotificationChannel Channel, int? InvoiceId, string Title, string Message,
    string? Recipient, NotificationStatus Status, string? Error, bool IsRead, DateTime CreatedAtUtc);

public sealed record EmailInvoiceRequest(string? To, string? Subject, string? Message);

public sealed record ShareInfo(string Text, string? WhatsappUrl, string? Phone);

public sealed class NotificationService(
    IAppDbContext db, IBusinessClock clock, IEmailSender email, DocumentPdfService pdf, InvoiceService invoices, ILogger<NotificationService> logger)
{
    public async Task<IReadOnlyList<NotificationDto>> ListAsync(bool unreadOnly, CancellationToken ct) =>
        (await db.NotificationLogs.AsNoTracking()
            .Where(n => !unreadOnly || !n.IsRead)
            .OrderByDescending(n => n.Id).Take(100).ToListAsync(ct))
        .Select(ToDto).ToList();

    public Task<int> UnreadCountAsync(CancellationToken ct) => db.NotificationLogs.CountAsync(n => !n.IsRead, ct);

    public async Task MarkReadAsync(int? id, CancellationToken ct)
    {
        var rows = await db.NotificationLogs.Where(n => !n.IsRead && (id == null || n.Id == id)).ToListAsync(ct);
        foreach (var r in rows) r.IsRead = true;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Daily reminders: "due soon" once per invoice and due date, "overdue" at most once a week per invoice.
    /// DedupKey (unique) guarantees no duplicates even if the job runs twice.
    /// </summary>
    public async Task<int> RunRemindersAsync(CancellationToken ct)
    {
        var s = await db.GetAsync(ct);
        if (!s.RemindersEnabled) return 0;
        var today = clock.Today;
        var open = await db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Issued && i.PaymentStatus != PaymentStatus.Paid && i.DueDate != null)
            .ToListAsync(ct);
        var created = 0;
        foreach (var inv in open)
        {
            var due = inv.DueDate!.Value;
            var balance = Fmt.Money(inv.Total - inv.PaidAmount, CurrencyDto.From(inv));
            NotificationLog? n = null;
            if (due < today)
            {
                var week = (today.DayNumber - due.DayNumber) / 7;
                n = New(NotificationType.Overdue, inv, $"overdue:{inv.Id}:{due:yyyyMMdd}:{week}",
                    Loc.T("سررسید فاکتور {0} گذشته است", inv.InvoiceNumber),
                    Loc.T("{0} — مانده {1} — سررسید {2} ({3} روز پیش)", inv.CustomerName, balance, Fmt.Date(due), Fmt.Fa((today.DayNumber - due.DayNumber).ToString())));
            }
            else if (due.DayNumber - today.DayNumber <= s.ReminderDaysBefore)
            {
                n = New(NotificationType.DueSoon, inv, $"duesoon:{inv.Id}:{due:yyyyMMdd}",
                    Loc.T("سررسید فاکتور {0} نزدیک است", inv.InvoiceNumber),
                    Loc.T("{0} — مانده {1} — سررسید {2}", inv.CustomerName, balance, Fmt.Date(due)));
            }
            if (n is null || await db.NotificationLogs.AnyAsync(x => x.DedupKey == n.DedupKey, ct)) continue;
            db.NotificationLogs.Add(n);
            created++;

            if (s.EmailRemindersToCustomers && s.IsSmtpConfigured)
            {
                var customerEmail = Snapshots.FromJson<CustomerSnapshot>(inv.CustomerSnapshotJson)?.Email;
                if (!string.IsNullOrWhiteSpace(customerEmail))
                    db.NotificationLogs.Add(await SendReminderEmailAsync(inv, customerEmail, n.DedupKey + ":email", s, ct));
            }
        }
        await db.SaveChangesAsync(ct);
        return created;
    }

    private async Task<NotificationLog> SendReminderEmailAsync(Invoice inv, string to, string dedup, AppSettings s, CancellationToken ct)
    {
        var log = New(NotificationType.DueSoon, inv, dedup, Loc.T("یادآوری ایمیلی فاکتور {0}", inv.InvoiceNumber), "", NotificationChannel.Email);
        log.Recipient = to;
        log.IsRead = true;
        try
        {
            var (bytes, name) = await pdf.InvoicePdfAsync(inv.Id, null, ct);
            var balance = Fmt.Money(inv.Total - inv.PaidAmount, CurrencyDto.From(inv));
            await email.SendAsync(new EmailMessage(to, Loc.T("یادآوری پرداخت فاکتور {0}", inv.InvoiceNumber),
                Html(Loc.T("مشتری گرامی {0}،", inv.CustomerName), Loc.T("یادآوری می‌کنیم مانده فاکتور {0} به مبلغ {1} با سررسید {2} هنوز پرداخت نشده است.", inv.InvoiceNumber, balance, Fmt.Date(inv.DueDate!.Value)), s),
                [new EmailAttachment(name, "application/pdf", bytes)]), ct);
            log.Message = Loc.T("به {0} ارسال شد.", to);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            log.Status = NotificationStatus.Failed;
            log.Error = ex.Message;
            log.Message = Loc.T("ارسال به {0} ناموفق بود.", to);
            logger.LogWarning("Reminder email for invoice {Id} failed: {Error}", inv.Id, ex.Message);
        }
        return log;
    }

    public async Task<NotificationDto> EmailInvoiceAsync(int invoiceId, EmailInvoiceRequest req, CancellationToken ct)
    {
        var inv = await invoices.GetAsync(invoiceId, ct);
        if (inv.Status != InvoiceStatus.Issued)
            throw new DomainException("email.not_issued", "فقط فاکتور صادرشده قابل ارسال است.");
        var to = Text.Clean(req.To) ?? inv.Customer.Email
            ?? throw new DomainException("email.no_recipient", "ایمیل مشتری ثبت نشده است؛ نشانی گیرنده را وارد کنید.");
        var s = await db.GetAsync(ct);
        var (bytes, name) = await pdf.InvoicePdfAsync(invoiceId, null, ct);
        var subject = Text.Clean(req.Subject) ?? Loc.T("فاکتور {0} از {1}", inv.InvoiceNumber, s.CompanyName);
        var text = Text.Clean(req.Message) ?? Loc.T("فاکتور {0} به مبلغ {1} پیوست این ایمیل است.", inv.InvoiceNumber, Fmt.Money(inv.Total, inv.Currency));
        var log = new NotificationLog
        {
            Type = NotificationType.InvoiceEmail, Channel = NotificationChannel.Email, InvoiceId = invoiceId,
            Title = Loc.T("ارسال فاکتور {0} با ایمیل", inv.InvoiceNumber), Message = Loc.T("به {0}", to), Recipient = to, IsRead = true, CreatedAtUtc = clock.UtcNow,
        };
        try
        {
            await email.SendAsync(new EmailMessage(to, subject, Html(Loc.T("{0} گرامی،", inv.Customer.Name), text, s), [new EmailAttachment(name, "application/pdf", bytes)]), ct);
        }
        catch (DomainException ex)
        {
            log.Status = NotificationStatus.Failed;
            log.Error = ex.Message;
            db.NotificationLogs.Add(log);
            await db.SaveChangesAsync(ct);
            throw;
        }
        db.NotificationLogs.Add(log);
        await db.SaveChangesAsync(ct);
        return ToDto(log);
    }

    public async Task SendTestEmailAsync(string to, CancellationToken ct)
    {
        var s = await db.GetAsync(ct);
        await email.SendAsync(new EmailMessage(to, Loc.T("ایمیل آزمایشی مدیریت فاکتور"), Html(Loc.T("سلام،"), Loc.T("تنظیمات ایمیل درست است."), s)), ct);
    }

    /// <summary>Text + wa.me link for sharing an invoice on WhatsApp (no API, nothing sent by the app).</summary>
    public async Task<ShareInfo> ShareAsync(int invoiceId, CancellationToken ct)
    {
        var inv = await invoices.GetAsync(invoiceId, ct);
        var s = await db.GetAsync(ct);
        var lines = new List<string>
        {
            Loc.T("{0} گرامی،", inv.Customer.Name),
            Loc.T("فاکتور {0} از {1}", inv.InvoiceNumber, s.CompanyName),
            Loc.T("مبلغ: {0}", Fmt.Money(inv.Total, inv.Currency)),
        };
        if (inv.PaidAmount > 0) lines.Add(Loc.T("مانده: {0}", Fmt.Money(inv.Balance, inv.Currency)));
        if (inv.DueDate is { } due) lines.Add(Loc.T("سررسید: {0}", Fmt.Date(due)));
        if (!string.IsNullOrWhiteSpace(s.BankCardNumber)) lines.Add(Loc.T("شماره کارت: {0}", s.BankCardNumber));
        if (!string.IsNullOrWhiteSpace(s.BankIban)) lines.Add(Loc.T("شبا: {0}", char.IsDigit(s.BankIban[0]) ? "IR" + s.BankIban : s.BankIban));
        lines.Add(Loc.T("فایل PDF فاکتور را جداگانه می‌فرستم. با تشکر."));
        var text = string.Join("\n", lines);
        var phone = NormalizeMobile(inv.Customer.Mobile ?? inv.Customer.Phone);
        var url = $"https://wa.me/{phone ?? ""}?text={Uri.EscapeDataString(text)}";
        return new ShareInfo(text, url, phone);
    }

    /// <summary>09121234567 / +98 912 … / ۰۹۱۲… → 989121234567. Returns null when it does not look like a mobile number.</summary>
    public static string? NormalizeMobile(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var normalized = Text.NormalizeForSearch(raw).Trim();
        var digits = new string(normalized.Where(char.IsAsciiDigit).ToArray());
        // International format (+44…, 0044…) works for any country.
        if (normalized.StartsWith('+') && digits.Length is >= 8 and <= 15) return digits;
        if (digits.StartsWith("00") && digits.Length is >= 10 and <= 17) return digits[2..];
        // Iranian local mobile numbers (09…) are recognized without a country code.
        if (digits.StartsWith("09") && digits.Length == 11) return "98" + digits[1..];
        if (digits.StartsWith('9') && digits.Length == 10) return "98" + digits;
        return digits.StartsWith("989") && digits.Length == 12 ? digits : null;
    }

    private NotificationLog New(NotificationType type, Invoice inv, string dedup, string title, string message, NotificationChannel channel = NotificationChannel.InApp) => new()
    {
        Type = type, Channel = channel, InvoiceId = inv.Id, Title = title, Message = message, DedupKey = dedup, CreatedAtUtc = clock.UtcNow,
    };

    public void AddInApp(NotificationType type, string title, string message, int? invoiceId = null) =>
        db.NotificationLogs.Add(new NotificationLog
        {
            Type = type, Channel = NotificationChannel.InApp, InvoiceId = invoiceId, Title = title, Message = message, CreatedAtUtc = clock.UtcNow,
        });

    private static string Html(string greeting, string body, AppSettings s)
    {
        string E(string? v) => WebUtility.HtmlEncode(v ?? "");
        return $"""
            <div dir="{(Loc.IsPersian ? "rtl" : "ltr")}" style="font-family:Tahoma,Vazirmatn,sans-serif;font-size:14px;line-height:1.9;color:#18211f">
              <p>{E(greeting)}</p><p>{E(body)}</p>
              <p style="color:#66706c">{E(s.CompanyName)}<br>{E(s.Phone)}</p>
            </div>
            """;
    }

    private static NotificationDto ToDto(NotificationLog n) =>
        new(n.Id, n.Type, n.Channel, n.InvoiceId, n.Title, n.Message, n.Recipient, n.Status, n.Error, n.IsRead, n.CreatedAtUtc);
}
