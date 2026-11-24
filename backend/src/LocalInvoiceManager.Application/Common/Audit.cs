using System.Text.Json;
using LocalInvoiceManager.Application.Abstractions;
using LocalInvoiceManager.Domain.Entities;

namespace LocalInvoiceManager.Application.Common;

/// <summary>Adds an audit row to the current unit of work (saved with the caller's SaveChanges).</summary>
public sealed class AuditLogger(IAppDbContext db, IBusinessClock clock)
{
    public void Log(string entityType, object entityId, string action, object? details = null) =>
        db.AuditLogs.Add(new AuditLog
        {
            EntityType = entityType,
            EntityId = entityId.ToString() ?? "",
            Action = action,
            Details = details is null ? null : JsonSerializer.Serialize(details, JsonDefaults.Options),
            CreatedAtUtc = clock.UtcNow,
        });
}

public static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };
}
