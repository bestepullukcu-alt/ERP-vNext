namespace Diten.CrmService.Application.Features.Contact;

/// <summary>
/// Seam for emitting MOD-0021 audit events for Contact actions. CRM does NOT own an audit store; the Infrastructure
/// implementation forwards to the MOD-0021 audit append contract (structured-logging seam pending Gateway wiring — FU06).
/// </summary>
public interface IContactAuditPublisher
{
    Task PublishAsync(string eventName, Guid tenantId, Guid contactId, string? detail, CancellationToken cancellationToken);
}

public static class ContactAuditEvents
{
    public const string Create = "contact.create";
    public const string Update = "contact.update";
    public const string Delete = "contact.delete";
    public const string DuplicateRejected = "contact.duplicate.rejected";
}
