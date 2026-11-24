using LocalInvoiceManager.Domain.Localization;
using FluentValidation;
using LocalInvoiceManager.Application.Abstractions;
using LocalInvoiceManager.Application.Common;
using LocalInvoiceManager.Domain;
using LocalInvoiceManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LocalInvoiceManager.Application.Settings;

public sealed record SettingsDto
{
    /// <summary>"en" or "fa".</summary>
    public string Language { get; init; } = "en";
    public CalendarKind Calendar { get; init; } = CalendarKind.Gregorian;
    /// <summary>Enabled currencies. <see cref="CurrencyCode"/> (the default) must be one of them; its symbol fields are taken from here.</summary>
    public List<CurrencyDef> Currencies { get; init; } = [];

    public string CompanyName { get; init; } = "";
    public string? LegalName { get; init; }
    public string? EconomicCode { get; init; }
    public string? NationalId { get; init; }
    public string? RegistrationNumber { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? Website { get; init; }
    public string? Address { get; init; }
    public string? PostalCode { get; init; }
    public bool HasLogo { get; init; }

    public string? BankName { get; init; }
    public string? BankAccountNumber { get; init; }
    public string? BankCardNumber { get; init; }
    public string? BankIban { get; init; }
    public string? BankAccountHolder { get; init; }

    public string CurrencyCode { get; init; } = "USD";
    public string CurrencySymbol { get; init; } = "$";
    public int CurrencyDecimalPlaces { get; init; } = 2;
    public string ThousandsSeparator { get; init; } = ",";
    public string DecimalSeparator { get; init; } = ".";
    public SymbolPosition CurrencySymbolPosition { get; init; }

    public decimal DefaultTaxRate { get; init; }
    public string TaxLabel { get; init; } = "";

    public string InvoicePrefix { get; init; } = "INV";
    public string QuotationPrefix { get; init; } = "QUO";
    public int NumberPadding { get; init; } = 4;
    public bool ResetNumberingYearly { get; init; }
    public int DefaultDueDays { get; init; }
    public string? DefaultPaymentTerms { get; init; }
    public string? DefaultNotes { get; init; }
    public string DefaultTemplate { get; init; } = "modern";
    public string AccentColor { get; init; } = "#0f766e";

    public int SessionTimeoutMinutes { get; init; }

    public bool BackupEnabled { get; init; }
    public int BackupHour { get; init; }
    public int BackupRetentionCount { get; init; }

    public bool RemindersEnabled { get; init; }
    public int ReminderDaysBefore { get; init; }
    public bool EmailRemindersToCustomers { get; init; }

    public string? SmtpHost { get; init; }
    public int SmtpPort { get; init; } = 587;
    public string? SmtpUser { get; init; }
    /// <summary>Write-only. Null = keep the stored password; empty string = remove it.</summary>
    public string? SmtpPassword { get; init; }
    public bool SmtpPasswordSet { get; init; }
    public bool SmtpUseSsl { get; init; }
    public string? SmtpFromEmail { get; init; }
    public string? SmtpFromName { get; init; }

    /// <summary>Example of the next invoice number with these settings.</summary>
    public string? NextInvoiceNumberPreview { get; init; }
}

public static class Templates
{
    public static readonly string[] Keys = ["modern", "classic", "minimal"];
}

/// <summary>Quick switch for the header toggle and first run.</summary>
public sealed record PreferencesRequest(string Language, CalendarKind Calendar);

public sealed class SettingsValidator : AbstractValidator<SettingsDto>
{
    public SettingsValidator()
    {
        RuleFor(x => x.Language).Must(l => l is "en" or "fa").WithMessage("زبان باید en یا fa باشد.");
        RuleFor(x => x.Calendar).IsInEnum();
        RuleForEach(x => x.Currencies).ChildRules(c =>
        {
            c.RuleFor(x => x.Code).NotEmpty().MaximumLength(10).Matches("^[A-Za-z]{2,10}$").WithMessage("کد ارز باید ۲ تا ۱۰ حرف انگلیسی باشد.");
            c.RuleFor(x => x.Symbol).NotEmpty().MaximumLength(10).WithMessage("نماد ارز را وارد کنید.");
            c.RuleFor(x => x.DecimalPlaces).InclusiveBetween(0, 4).WithMessage("تعداد اعشار باید بین ۰ تا ۴ باشد.");
        });
        RuleFor(x => x.Currencies).Must(l => l.Select(c => c.Code.ToUpperInvariant()).Distinct().Count() == l.Count)
            .WithMessage("هر ارز فقط یک بار در فهرست بیاید.");
        RuleFor(x => x).Must(x => x.Currencies.Count == 0 || x.Currencies.Any(c => c.Code.Equals(x.CurrencyCode, StringComparison.OrdinalIgnoreCase)))
            .WithMessage("ارز پیش‌فرض باید در فهرست ارزها باشد.").WithName("currencyCode");
        RuleFor(x => x.CompanyName).MaximumLength(200);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email)).WithMessage("ایمیل معتبر نیست.");
        RuleFor(x => x.CurrencyCode).NotEmpty().MaximumLength(10).WithMessage("کد ارز را وارد کنید.");
        RuleFor(x => x.CurrencySymbol).NotEmpty().MaximumLength(10).WithMessage("نماد ارز را وارد کنید.");
        RuleFor(x => x.CurrencyDecimalPlaces).InclusiveBetween(0, 4).WithMessage("تعداد اعشار باید بین ۰ تا ۴ باشد.");
        RuleFor(x => x.ThousandsSeparator).MaximumLength(1);
        RuleFor(x => x.DecimalSeparator).NotEmpty().MaximumLength(1);
        RuleFor(x => x).Must(x => x.ThousandsSeparator != x.DecimalSeparator)
            .WithMessage("جداکننده هزارگان و اعشار نباید یکسان باشند.").WithName("decimalSeparator");
        RuleFor(x => x.DefaultTaxRate).InclusiveBetween(0, 100).WithMessage("نرخ مالیات باید بین ۰ تا ۱۰۰ باشد.");
        RuleFor(x => x.TaxLabel).NotEmpty().MaximumLength(60);
        RuleFor(x => x.InvoicePrefix).NotEmpty().MaximumLength(10).Matches("^[A-Za-z0-9]+$")
            .WithMessage("پیشوند فقط حروف انگلیسی و عدد باشد.");
        RuleFor(x => x.QuotationPrefix).NotEmpty().MaximumLength(10).Matches("^[A-Za-z0-9]+$")
            .WithMessage("پیشوند فقط حروف انگلیسی و عدد باشد.");
        RuleFor(x => x.NumberPadding).InclusiveBetween(1, 8);
        RuleFor(x => x.DefaultDueDays).InclusiveBetween(0, 365);
        RuleFor(x => x.DefaultTemplate).Must(t => Templates.Keys.Contains(t)).WithMessage("قالب نامعتبر است.");
        RuleFor(x => x.AccentColor).Matches("^#[0-9a-fA-F]{6}$").WithMessage("رنگ باید به شکل ‎#RRGGBB‎ باشد.");
        RuleFor(x => x.SessionTimeoutMinutes).InclusiveBetween(5, 720).WithMessage("زمان نشست باید بین ۵ تا ۷۲۰ دقیقه باشد.");
        RuleFor(x => x.BackupHour).InclusiveBetween(0, 23);
        RuleFor(x => x.BackupRetentionCount).InclusiveBetween(1, 365);
        RuleFor(x => x.ReminderDaysBefore).InclusiveBetween(0, 60);
        RuleFor(x => x.SmtpPort).InclusiveBetween(1, 65535);
        RuleFor(x => x.SmtpFromEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.SmtpFromEmail)).WithMessage("ایمیل فرستنده معتبر نیست.");
        // IBAN of any country (2 letters + 2 check digits + up to 30 characters), or an Iranian Sheba without "IR".
        RuleFor(x => x.BankIban).Matches(@"^([A-Z]{2}\d{2}[A-Z0-9]{10,30}|\d{24})$").When(x => !string.IsNullOrWhiteSpace(x.BankIban))
            .WithMessage("شماره IBAN / شبا معتبر نیست.");
        RuleFor(x => x.BankCardNumber).Matches(@"^\d{12,19}$").When(x => !string.IsNullOrWhiteSpace(x.BankCardNumber))
            .WithMessage("شماره کارت باید ۱۲ تا ۱۹ رقم باشد.");
    }
}

public sealed class SettingsService(
    IAppDbContext db,
    IBusinessClock clock,
    AuditLogger audit,
    ISecretProtector protector,
    IFileStore files,
    NumberingService numbering,
    IValidator<SettingsDto> validator)
{
    public const long MaxLogoBytes = 1024 * 1024;

    public async Task<SettingsDto> GetAsync(CancellationToken ct)
    {
        var s = await db.GetAsync(ct);
        return ToDto(s) with { NextInvoiceNumberPreview = await numbering.PreviewInvoiceNumberAsync(ct) };
    }

    public async Task<SettingsDto> UpdateAsync(SettingsDto dto, CancellationToken ct)
    {
        dto = Normalize(dto);
        await validator.ValidateAndThrowAsync(dto, ct);
        var s = await db.GetAsync(ct);

        s.Language = dto.Language;
        s.Calendar = dto.Calendar;
        s.CompanyName = dto.CompanyName.Trim();
        s.LegalName = Text.Clean(dto.LegalName);
        s.EconomicCode = Text.Clean(dto.EconomicCode);
        s.NationalId = Text.Clean(dto.NationalId);
        s.RegistrationNumber = Text.Clean(dto.RegistrationNumber);
        s.Phone = Text.Clean(dto.Phone);
        s.Email = Text.Clean(dto.Email);
        s.Website = Text.Clean(dto.Website);
        s.Address = Text.Clean(dto.Address);
        s.PostalCode = Text.Clean(dto.PostalCode);
        s.BankName = Text.Clean(dto.BankName);
        s.BankAccountNumber = Text.Clean(dto.BankAccountNumber);
        s.BankCardNumber = Text.Clean(dto.BankCardNumber);
        s.BankIban = Text.Clean(dto.BankIban);
        s.BankAccountHolder = Text.Clean(dto.BankAccountHolder);
        s.CurrencyCode = dto.CurrencyCode.Trim().ToUpperInvariant();
        var defaultDef = dto.Currencies.FirstOrDefault(c => c.Code.Equals(s.CurrencyCode, StringComparison.OrdinalIgnoreCase));
        s.CurrencySymbol = defaultDef?.Symbol ?? dto.CurrencySymbol.Trim();
        s.CurrencyDecimalPlaces = defaultDef?.DecimalPlaces ?? dto.CurrencyDecimalPlaces;
        s.ThousandsSeparator = dto.ThousandsSeparator;
        s.DecimalSeparator = dto.DecimalSeparator;
        s.CurrencySymbolPosition = defaultDef?.SymbolPosition ?? dto.CurrencySymbolPosition;
        s.CurrenciesJson = CurrencyDef.ToJson(dto.Currencies);
        s.DefaultTaxRate = dto.DefaultTaxRate;
        s.TaxLabel = dto.TaxLabel.Trim();
        s.InvoicePrefix = dto.InvoicePrefix.Trim().ToUpperInvariant();
        s.QuotationPrefix = dto.QuotationPrefix.Trim().ToUpperInvariant();
        s.NumberPadding = dto.NumberPadding;
        s.ResetNumberingYearly = dto.ResetNumberingYearly;
        s.DefaultDueDays = dto.DefaultDueDays;
        s.DefaultPaymentTerms = Text.Clean(dto.DefaultPaymentTerms);
        s.DefaultNotes = Text.Clean(dto.DefaultNotes);
        s.DefaultTemplate = dto.DefaultTemplate;
        s.AccentColor = dto.AccentColor.ToLowerInvariant();
        s.SessionTimeoutMinutes = dto.SessionTimeoutMinutes;
        s.BackupEnabled = dto.BackupEnabled;
        s.BackupHour = dto.BackupHour;
        s.BackupRetentionCount = dto.BackupRetentionCount;
        s.RemindersEnabled = dto.RemindersEnabled;
        s.ReminderDaysBefore = dto.ReminderDaysBefore;
        s.EmailRemindersToCustomers = dto.EmailRemindersToCustomers;
        s.SmtpHost = Text.Clean(dto.SmtpHost);
        s.SmtpPort = dto.SmtpPort;
        s.SmtpUser = Text.Clean(dto.SmtpUser);
        s.SmtpUseSsl = dto.SmtpUseSsl;
        s.SmtpFromEmail = Text.Clean(dto.SmtpFromEmail);
        s.SmtpFromName = Text.Clean(dto.SmtpFromName);
        if (dto.SmtpPassword is not null)
            s.SmtpPasswordProtected = dto.SmtpPassword.Length == 0 ? null : protector.Protect(dto.SmtpPassword);
        s.UpdatedAtUtc = clock.UtcNow;

        audit.Log("Settings", 1, "Updated");
        await db.SaveChangesAsync(ct);
        ApplyDefaults(s);
        return await GetAsync(ct);
    }

    /// <summary>Changes only language and calendar (header switch).</summary>
    public async Task SetPreferencesAsync(PreferencesRequest req, CancellationToken ct)
    {
        if (req.Language is not ("en" or "fa")) throw new DomainException("settings.language", "زبان باید en یا fa باشد.");
        var s = await db.GetAsync(ct);
        s.Language = req.Language;
        s.Calendar = req.Calendar;
        s.UpdatedAtUtc = clock.UtcNow;
        audit.Log("Settings", 1, "Preferences", new { req.Language, req.Calendar });
        await db.SaveChangesAsync(ct);
        ApplyDefaults(s);
    }

    /// <summary>Background jobs and documents use the stored language and calendar.</summary>
    public static void ApplyDefaults(AppSettings s)
    {
        Loc.DefaultLanguage = s.Language;
        Loc.DefaultCalendar = s.Calendar;
    }

    public async Task SetLogoAsync(string fileName, Stream content, long length, CancellationToken ct)
    {
        if (length <= 0 || length > MaxLogoBytes)
            throw new DomainException("logo.size", "حجم لوگو باید کمتر از ۱ مگابایت باشد.");
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        var contentType = ImageSniffer.Detect(bytes)
            ?? throw new DomainException("logo.type", "فقط تصویر PNG، JPEG یا WebP قابل قبول است.");

        var s = await db.GetAsync(ct);
        await RemoveLogoInternalAsync(s, ct);
        var ext = contentType switch { "image/png" => ".png", "image/jpeg" => ".jpg", _ => ".webp" };
        ms.Position = 0;
        var path = await files.SaveAsync("logo", $"logo-{Guid.NewGuid():N}{ext}", ms, ct);
        var doc = new DocumentFile
        {
            Kind = DocumentKind.Logo,
            FileName = Path.GetFileName(fileName),
            ContentType = contentType,
            SizeBytes = bytes.Length,
            StoragePath = path,
            Sha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant(),
            CreatedAtUtc = clock.UtcNow,
        };
        db.DocumentFiles.Add(doc);
        await db.SaveChangesAsync(ct);
        s.LogoFileId = doc.Id;
        s.UpdatedAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task RemoveLogoAsync(CancellationToken ct)
    {
        var s = await db.GetAsync(ct);
        await RemoveLogoInternalAsync(s, ct);
        await db.SaveChangesAsync(ct);
    }

    public async Task<(byte[] Bytes, string ContentType)?> GetLogoAsync(CancellationToken ct)
    {
        var s = await db.GetAsync(ct);
        if (s.LogoFileId is null) return null;
        var doc = await db.DocumentFiles.FindAsync([s.LogoFileId.Value], ct);
        if (doc is null) return null;
        await using var stream = files.OpenRead(doc.StoragePath);
        if (stream is null) return null;
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct);
        return (ms.ToArray(), doc.ContentType);
    }

    private async Task RemoveLogoInternalAsync(AppSettings s, CancellationToken ct)
    {
        if (s.LogoFileId is null) return;
        // The file itself is kept: issued invoices reference the logo that was current when they were issued.
        var stillUsed = await db.Invoices.AnyAsync(i => i.SellerSnapshotJson != null && i.SellerSnapshotJson.Contains($"\"logoFileId\":{s.LogoFileId}"), ct);
        var doc = await db.DocumentFiles.FindAsync([s.LogoFileId.Value], ct);
        s.LogoFileId = null;
        if (doc is not null && !stillUsed)
        {
            files.Delete(doc.StoragePath);
            db.DocumentFiles.Remove(doc);
        }
    }

    private static SettingsDto Normalize(SettingsDto d) => d with
    {
        Language = Loc.Normalize(d.Language) ?? "en",
        Currencies = (d.Currencies ?? []).Select(c => c with { Code = (c.Code ?? "").Trim().ToUpperInvariant(), Symbol = (c.Symbol ?? "").Trim() }).ToList(),
        BankIban = Text.Clean(d.BankIban)?.Replace(" ", "").ToUpperInvariant(),
        BankCardNumber = Text.Clean(d.BankCardNumber)?.Replace(" ", "").Replace("-", ""),
        ThousandsSeparator = d.ThousandsSeparator ?? "",
    };

    public static SettingsDto ToDto(AppSettings s) => new()
    {
        Language = s.Language, Calendar = s.Calendar, Currencies = [.. s.Currencies()],
        CompanyName = s.CompanyName, LegalName = s.LegalName, EconomicCode = s.EconomicCode, NationalId = s.NationalId,
        RegistrationNumber = s.RegistrationNumber, Phone = s.Phone, Email = s.Email, Website = s.Website,
        Address = s.Address, PostalCode = s.PostalCode, HasLogo = s.LogoFileId is not null,
        BankName = s.BankName, BankAccountNumber = s.BankAccountNumber, BankCardNumber = s.BankCardNumber,
        BankIban = s.BankIban, BankAccountHolder = s.BankAccountHolder,
        CurrencyCode = s.CurrencyCode, CurrencySymbol = s.CurrencySymbol, CurrencyDecimalPlaces = s.CurrencyDecimalPlaces,
        ThousandsSeparator = s.ThousandsSeparator, DecimalSeparator = s.DecimalSeparator,
        CurrencySymbolPosition = s.CurrencySymbolPosition,
        DefaultTaxRate = s.DefaultTaxRate, TaxLabel = s.TaxLabel,
        InvoicePrefix = s.InvoicePrefix, QuotationPrefix = s.QuotationPrefix, NumberPadding = s.NumberPadding,
        ResetNumberingYearly = s.ResetNumberingYearly, DefaultDueDays = s.DefaultDueDays,
        DefaultPaymentTerms = s.DefaultPaymentTerms, DefaultNotes = s.DefaultNotes,
        DefaultTemplate = s.DefaultTemplate, AccentColor = s.AccentColor,
        SessionTimeoutMinutes = s.SessionTimeoutMinutes,
        BackupEnabled = s.BackupEnabled, BackupHour = s.BackupHour, BackupRetentionCount = s.BackupRetentionCount,
        RemindersEnabled = s.RemindersEnabled, ReminderDaysBefore = s.ReminderDaysBefore,
        EmailRemindersToCustomers = s.EmailRemindersToCustomers,
        SmtpHost = s.SmtpHost, SmtpPort = s.SmtpPort, SmtpUser = s.SmtpUser, SmtpUseSsl = s.SmtpUseSsl,
        SmtpFromEmail = s.SmtpFromEmail, SmtpFromName = s.SmtpFromName,
        SmtpPasswordSet = s.SmtpPasswordProtected is not null,
    };
}

public static class ImageSniffer
{
    /// <summary>Detects PNG/JPEG/WebP by magic bytes (never trusts the file name or browser content type).</summary>
    public static string? Detect(ReadOnlySpan<byte> b)
    {
        if (b.Length >= 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47) return "image/png";
        if (b.Length >= 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        if (b.Length >= 12 && b[0] == 'R' && b[1] == 'I' && b[2] == 'F' && b[3] == 'F' && b[8] == 'W' && b[9] == 'E' && b[10] == 'B' && b[11] == 'P')
            return "image/webp";
        return null;
    }
}
