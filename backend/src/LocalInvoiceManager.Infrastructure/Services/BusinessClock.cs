using LocalInvoiceManager.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace LocalInvoiceManager.Infrastructure.Services;

public sealed class BusinessClock(TimeProvider time, IOptions<AppOptions> options) : IBusinessClock
{
    private readonly TimeZoneInfo _zone = ResolveZone(options.Value.TimeZone);

    public DateTime UtcNow => time.GetUtcNow().UtcDateTime;

    public DateTime LocalNow => TimeZoneInfo.ConvertTimeFromUtc(UtcNow, _zone);

    public DateOnly Today => DateOnly.FromDateTime(LocalNow);

    /// <summary>Configured IANA/Windows zone id; empty = the computer's own time zone.</summary>
    private static TimeZoneInfo ResolveZone(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return TimeZoneInfo.Local;
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (Exception e) when (e is TimeZoneNotFoundException or InvalidTimeZoneException) { return TimeZoneInfo.Local; }
    }
}
