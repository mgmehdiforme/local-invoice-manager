using LocalInvoiceManager.Domain.Entities;
using LocalInvoiceManager.Domain.Localization;
using LocalInvoiceManager.Application.Abstractions;
using LocalInvoiceManager.Application.Common;
using LocalInvoiceManager.Application.Settings;
using LocalInvoiceManager.Domain;
using Microsoft.AspNetCore.Identity;

namespace LocalInvoiceManager.Application.Auth;

public sealed record AuthStatus(bool IsSetUp, int SessionTimeoutMinutes, string Language, CalendarKind Calendar);

/// <summary>First run: the password plus optional language, calendar and default currency.</summary>
public sealed record SetupRequest(string Password, string? Language = null, CalendarKind? Calendar = null, string? CurrencyCode = null);

public sealed record LoginResult(string SecurityStamp, int SessionTimeoutMinutes);

public sealed class AuthService(IAppDbContext db, IBusinessClock clock, AuditLogger audit)
{
    public const int MinPasswordLength = 8;
    public const int MaxFailedAttempts = 5;
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object User = new();

    public async Task<AuthStatus> GetStatusAsync(CancellationToken ct)
    {
        var s = await db.GetAsync(ct);
        return new AuthStatus(s.PasswordHash is not null, s.SessionTimeoutMinutes, s.Language, s.Calendar);
    }

    public async Task<LoginResult> SetupAsync(SetupRequest req, CancellationToken ct)
    {
        var password = req.Password;
        var s = await db.GetAsync(ct);
        if (s.PasswordHash is not null)
            throw new DomainException("auth.already_set_up", "رمز عبور قبلاً تنظیم شده است.");
        ValidateNewPassword(password);
        ApplyFirstRunPreferences(s, req);
        s.PasswordHash = Hasher.HashPassword(User, password);
        s.SecurityStamp = Guid.NewGuid().ToString("N");
        s.UpdatedAtUtc = clock.UtcNow;
        audit.Log("Auth", 1, "Setup");
        await db.SaveChangesAsync(ct);
        return new LoginResult(s.SecurityStamp, s.SessionTimeoutMinutes);
    }

    /// <summary>Language, calendar and currency chosen on the first-run screen, with matching number format and tax defaults.</summary>
    private static void ApplyFirstRunPreferences(AppSettings s, SetupRequest req)
    {
        if (Loc.Normalize(req.Language) is { } lang)
        {
            s.Language = lang;
            s.Calendar = lang == "fa" ? CalendarKind.Jalali : CalendarKind.Gregorian;
            if (lang == "fa")
            {
                s.ThousandsSeparator = "٬";
                s.DecimalSeparator = "٫";
                s.TaxLabel = "مالیات بر ارزش افزوده";
                s.DefaultTaxRate = 10m;
            }
        }
        if (req.Calendar is { } cal) s.Calendar = cal;
        if (!string.IsNullOrWhiteSpace(req.CurrencyCode))
        {
            var c = CurrencyPresets.Find(req.CurrencyCode);
            s.CurrencyCode = c.Code;
            s.CurrencySymbol = c.Symbol;
            s.CurrencyDecimalPlaces = c.DecimalPlaces;
            s.CurrencySymbolPosition = c.SymbolPosition;
            s.CurrenciesJson = CurrencyDef.ToJson([c]);
        }
        Settings.SettingsService.ApplyDefaults(s);
    }

    public async Task<LoginResult> LoginAsync(string password, CancellationToken ct)
    {
        var s = await db.GetAsync(ct);
        if (s.PasswordHash is null)
            throw new DomainException("auth.not_set_up", "ابتدا رمز عبور را تنظیم کنید.");

        var now = clock.UtcNow;
        if (s.LockoutUntilUtc is { } until && until > now)
        {
            var minutes = (int)Math.Ceiling((until - now).TotalMinutes);
            throw new DomainException("auth.locked_out", Loc.T("به دلیل تلاش‌های ناموفق، ورود تا {0} دقیقه دیگر ممکن نیست.", minutes));
        }

        var result = Hasher.VerifyHashedPassword(User, s.PasswordHash, password ?? "");
        if (result == PasswordVerificationResult.Failed)
        {
            s.FailedLoginCount++;
            if (s.FailedLoginCount >= MaxFailedAttempts)
            {
                var minutes = Math.Min(60, 5 * (1 << Math.Min(s.LockoutCount, 4)));
                s.LockoutUntilUtc = now.AddMinutes(minutes);
                s.LockoutCount++;
                s.FailedLoginCount = 0;
                audit.Log("Auth", 1, "LockedOut", new { minutes });
            }
            await db.SaveChangesAsync(ct);
            throw new DomainException("auth.invalid_password", "رمز عبور اشتباه است.");
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
            s.PasswordHash = Hasher.HashPassword(User, password!);
        s.FailedLoginCount = 0;
        s.LockoutCount = 0;
        s.LockoutUntilUtc = null;
        await db.SaveChangesAsync(ct);
        return new LoginResult(s.SecurityStamp, s.SessionTimeoutMinutes);
    }

    public async Task<LoginResult> ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct)
    {
        var s = await db.GetAsync(ct);
        if (s.PasswordHash is null || Hasher.VerifyHashedPassword(User, s.PasswordHash, currentPassword ?? "") == PasswordVerificationResult.Failed)
            throw new DomainException("auth.invalid_password", "رمز عبور فعلی اشتباه است.");
        ValidateNewPassword(newPassword);
        s.PasswordHash = Hasher.HashPassword(User, newPassword);
        s.SecurityStamp = Guid.NewGuid().ToString("N");
        s.UpdatedAtUtc = clock.UtcNow;
        audit.Log("Auth", 1, "PasswordChanged");
        await db.SaveChangesAsync(ct);
        return new LoginResult(s.SecurityStamp, s.SessionTimeoutMinutes);
    }

    /// <summary>Used by the command line "reset-password": clears the password so the setup screen appears again. Data is kept.</summary>
    public async Task ResetPasswordAsync(CancellationToken ct)
    {
        var s = await db.GetAsync(ct);
        s.PasswordHash = null;
        s.SecurityStamp = Guid.NewGuid().ToString("N");
        s.FailedLoginCount = 0;
        s.LockoutCount = 0;
        s.LockoutUntilUtc = null;
        audit.Log("Auth", 1, "PasswordReset");
        await db.SaveChangesAsync(ct);
    }

    public async Task<(bool Valid, int TimeoutMinutes)> ValidateSessionAsync(string? stamp, CancellationToken ct)
    {
        var s = await db.GetAsync(ct);
        return (s.PasswordHash is not null && stamp == s.SecurityStamp, s.SessionTimeoutMinutes);
    }

    private static void ValidateNewPassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            throw new DomainException("auth.weak_password", Loc.T("رمز عبور باید حداقل {0} کاراکتر باشد.", MinPasswordLength));
        if (password.Length > 128)
            throw new DomainException("auth.weak_password", "رمز عبور بیش از حد طولانی است.");
    }
}
