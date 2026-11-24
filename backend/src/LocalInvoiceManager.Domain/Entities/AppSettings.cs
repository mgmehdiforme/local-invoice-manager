namespace LocalInvoiceManager.Domain.Entities;

/// <summary>The single settings row (Id = 1). Holds the one seller profile and all app configuration.</summary>
public class AppSettings
{
    public const int SingletonId = 1;
    public int Id { get; set; } = SingletonId;

    // Seller
    public string CompanyName { get; set; } = "";
    public string? LegalName { get; set; }
    public string? EconomicCode { get; set; }
    public string? NationalId { get; set; }
    public string? RegistrationNumber { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Website { get; set; }
    public string? Address { get; set; }
    public string? PostalCode { get; set; }
    public int? LogoFileId { get; set; }

    // Bank
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankCardNumber { get; set; }
    public string? BankIban { get; set; }
    public string? BankAccountHolder { get; set; }

    // Language and calendar. New databases start in English + Gregorian; the phase 8 migration keeps
    // existing databases in Persian + Jalali.
    /// <summary>"en" or "fa". UI, PDFs, CSV headers, notifications and e-mails use it.</summary>
    public string Language { get; set; } = "en";
    public CalendarKind Calendar { get; set; } = CalendarKind.Gregorian;

    // Currency: the default currency (below) plus any other enabled currencies in CurrenciesJson.
    public string CurrencyCode { get; set; } = "USD";
    public string CurrencySymbol { get; set; } = "$";
    public int CurrencyDecimalPlaces { get; set; } = 2;
    public string ThousandsSeparator { get; set; } = ",";
    public string DecimalSeparator { get; set; } = ".";
    public SymbolPosition CurrencySymbolPosition { get; set; } = SymbolPosition.Before;
    /// <summary>JSON array of <see cref="CurrencyDef"/>; always contains the default currency.</summary>
    public string CurrenciesJson { get; set; } = "[]";

    // Tax
    public decimal DefaultTaxRate { get; set; }
    public string TaxLabel { get; set; } = "Tax";

    // Numbering & defaults
    public string InvoicePrefix { get; set; } = "INV";
    public string QuotationPrefix { get; set; } = "QUO";
    public int NumberPadding { get; set; } = 4;
    public bool ResetNumberingYearly { get; set; } = true;
    public int DefaultDueDays { get; set; } = 30;
    public string? DefaultPaymentTerms { get; set; }
    public string? DefaultNotes { get; set; }
    public string DefaultTemplate { get; set; } = "modern";
    public string AccentColor { get; set; } = "#0f766e";

    // Security
    public string? PasswordHash { get; set; }
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public int SessionTimeoutMinutes { get; set; } = 30;
    public int FailedLoginCount { get; set; }
    public int LockoutCount { get; set; }
    public DateTime? LockoutUntilUtc { get; set; }

    // Backup
    public bool BackupEnabled { get; set; } = true;
    public int BackupHour { get; set; } = 2;
    public int BackupRetentionCount { get; set; } = 30;

    // Reminders
    public bool RemindersEnabled { get; set; } = true;
    public int ReminderDaysBefore { get; set; } = 3;
    public bool EmailRemindersToCustomers { get; set; }

    // Email (optional)
    public string? SmtpHost { get; set; }
    public int SmtpPort { get; set; } = 587;
    public string? SmtpUser { get; set; }
    public string? SmtpPasswordProtected { get; set; }
    public bool SmtpUseSsl { get; set; } = true;
    public string? SmtpFromEmail { get; set; }
    public string? SmtpFromName { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public Calendar.CalendarSystem CalendarSystem => Domain.Calendar.CalendarSystem.For(Calendar);

    /// <summary>Enabled currencies, default first. Falls back to the default currency alone.</summary>
    public IReadOnlyList<CurrencyDef> Currencies()
    {
        var list = CurrencyDef.ParseList(CurrenciesJson);
        var def = new CurrencyDef(CurrencyCode, CurrencySymbol, CurrencyDecimalPlaces, CurrencySymbolPosition);
        return [def, .. list.Where(c => !c.Code.Equals(CurrencyCode, StringComparison.OrdinalIgnoreCase))];
    }

    /// <summary>The enabled currency with this code; null or empty means the default currency.</summary>
    public CurrencyDef ResolveCurrency(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return Currencies()[0];
        return Currencies().FirstOrDefault(c => c.Code.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new DomainException("currency.unknown", "این ارز در تنظیمات فعال نیست.");
    }

    public bool IsSmtpConfigured => !string.IsNullOrWhiteSpace(SmtpHost) && !string.IsNullOrWhiteSpace(SmtpFromEmail);
}

/// <summary>An enabled currency. Separators are app-wide (AppSettings), not per currency.</summary>
public sealed record CurrencyDef(string Code, string Symbol, int DecimalPlaces, SymbolPosition SymbolPosition)
{
    private static readonly System.Text.Json.JsonSerializerOptions Json = new(System.Text.Json.JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public static IReadOnlyList<CurrencyDef> ParseList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return System.Text.Json.JsonSerializer.Deserialize<List<CurrencyDef>>(json, Json) ?? []; }
        catch (System.Text.Json.JsonException) { return []; }
    }

    public static string ToJson(IEnumerable<CurrencyDef> list) => System.Text.Json.JsonSerializer.Serialize(list, Json);
}
