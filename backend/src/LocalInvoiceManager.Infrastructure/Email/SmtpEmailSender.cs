using LocalInvoiceManager.Domain.Localization;
using LocalInvoiceManager.Application.Abstractions;
using LocalInvoiceManager.Application.Settings;
using LocalInvoiceManager.Domain;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;

namespace LocalInvoiceManager.Infrastructure.Email;

/// <summary>Optional SMTP delivery (any provider). Nothing else in the app depends on it.</summary>
public sealed class SmtpEmailSender(IServiceScopeFactory scopes, ISecretProtector protector) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        var s = await db.GetAsync(ct);
        if (!s.IsSmtpConfigured)
            throw new DomainException("email.not_configured", "ابتدا تنظیمات ایمیل را در «تنظیمات › یادآوری و ایمیل» کامل کنید.");

        var mime = new MimeMessage();
        mime.From.Add(new MailboxAddress(s.SmtpFromName ?? s.CompanyName, s.SmtpFromEmail!));
        if (!MailboxAddress.TryParse(message.To, out var to))
            throw new DomainException("email.invalid_to", "نشانی ایمیل گیرنده معتبر نیست.");
        mime.To.Add(to);
        mime.Subject = message.Subject;
        var body = new BodyBuilder { HtmlBody = message.HtmlBody };
        foreach (var a in message.Attachments ?? []) body.Attachments.Add(a.FileName, a.Content, ContentType.Parse(a.ContentType));
        mime.Body = body.ToMessageBody();

        using var client = new SmtpClient { Timeout = 30_000 };
        try
        {
            var security = !s.SmtpUseSsl ? SecureSocketOptions.None : s.SmtpPort == 465 ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            await client.ConnectAsync(s.SmtpHost!, s.SmtpPort, security, ct);
            var password = protector.Unprotect(s.SmtpPasswordProtected);
            if (!string.IsNullOrEmpty(s.SmtpUser)) await client.AuthenticateAsync(s.SmtpUser, password ?? "", ct);
            await client.SendAsync(FormatOptions.Default, new MailKit.SendRequest(mime), ct);
            await client.DisconnectAsync(true, ct);
        }
        catch (Exception ex) when (ex is not DomainException and not OperationCanceledException)
        {
            // Show a useful reason without leaking credentials.
            throw new DomainException("email.send_failed", Loc.T("ارسال ایمیل ناموفق بود: {0}. اتصال اینترنت و تنظیمات SMTP را بررسی کنید.", ex.GetType().Name));
        }
    }
}
