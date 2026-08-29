namespace Diten.CrmService.Application.Features.Account;

/// <summary>
/// Seam for emitting MOD-0021 audit events for Account actions. CRM does NOT own an audit store;
/// the Infrastructure implementation forwards to the MOD-0021 audit append contract (currently a
/// structured-logging seam pending Gateway wiring — see follow-ups).
/// </summary>
public interface IAccountAuditPublisher
{
    Task PublishAsync(string eventName, Guid tenantId, Guid accountId, string? detail, CancellationToken cancellationToken);
}

public static class AccountAuditEvents
{
    public const string Create = "account.create";
    public const string Update = "account.update";
    public const string Delete = "account.delete";
    public const string Import = "account.import";
    public const string Export = "account.export";
    public const string HierarchyLink = "account.hierarchy.link";
    public const string HierarchyUnlink = "account.hierarchy.unlink";
    public const string AttributeUpdate = "account.attribute.update";
    public const string DuplicateRejected = "account.duplicate.rejected";
}
