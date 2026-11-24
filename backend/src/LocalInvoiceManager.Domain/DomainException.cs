namespace LocalInvoiceManager.Domain;

/// <summary>A business rule was broken. <see cref="Code"/> is a stable id; <see cref="Message"/> is shown to the user (Persian).</summary>
public class DomainException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class NotFoundException(string entity, object id)
    : DomainException($"{entity.ToLowerInvariant()}.not_found", "مورد درخواستی پیدا نشد.")
{
    public string Entity { get; } = entity;
    public object Id { get; } = id;
}
