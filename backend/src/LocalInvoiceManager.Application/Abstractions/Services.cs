namespace LocalInvoiceManager.Application.Abstractions;

/// <summary>Current time and the business "today" in the configured time zone (the computer's zone unless App:TimeZone is set).</summary>
public interface IBusinessClock
{
    DateTime UtcNow { get; }
    DateOnly Today { get; }
    /// <summary>Wall-clock time in the business time zone.</summary>
    DateTime LocalNow { get; }
}

/// <summary>Stores user files (logo, attachments) under the data folder.</summary>
public interface IFileStore
{
    Task<string> SaveAsync(string relativeFolder, string fileName, Stream content, CancellationToken ct);
    Stream? OpenRead(string relativePath);
    void Delete(string relativePath);
}

/// <summary>Encrypts secrets (SMTP password) at rest.</summary>
public interface ISecretProtector
{
    string Protect(string plain);
    string? Unprotect(string? protectedValue);
}

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}

public sealed record EmailAttachment(string FileName, string ContentType, byte[] Content);

public sealed record EmailMessage(string To, string Subject, string HtmlBody, IReadOnlyList<EmailAttachment>? Attachments = null);
