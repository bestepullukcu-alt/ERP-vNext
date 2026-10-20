namespace Diten.BuildingBlocks.Auditing;

/// <summary>
/// AUD-001 K4 (WP-VW-W6 F8-a) — the SHARED marker of path <c>b</c>: a command whose every execution is forwarded to
/// Platform's central audit store (<c>POST /api/internal/audit/append</c>) by <see cref="AuditForwardingBehavior{TRequest,TResponse}"/>.
/// <para>
/// The marker is not empty on purpose: a marked command must SAY what it is (category, operation, object type, source
/// module) — there is no default a forgotten value could fall back to. A command that also wants to say what changed
/// implements <see cref="IAuditDetailsProvider"/>.
/// </para>
/// <para>
/// The marker does nothing on its own: the service registers <see cref="AuditForwardingBehavior{TRequest,TResponse}"/>
/// in its MediatR pipeline (the architecture test looks for that registration in the service itself).
/// </para>
/// </summary>
public interface IAuditableCommand
{
    AuditDescriptor DescribeAudit();
}

/// <summary>What one audited command is. <paramref name="EntityId"/> is the object's id when the command knows it; a
/// create learns it from its response (<see cref="AuditDetails.EntityId"/>).</summary>
public sealed record AuditDescriptor(
    AuditCategory Category,
    AuditOperation Operation,
    string EntityType,
    string SourceModule,
    Guid? EntityId = null);

// The wire values of Platform's own enums (Diten.Platform.Domain.Enums.AuditEnums). Platform maps by the SAME integer
// values; AuditEnumAlignmentTests compares this file with Platform's source, name by name and value by value.

public enum AuditCategory
{
    Unknown = 0,
    Security = 1,
    IdentityAccess = 2,
    TenantAdministration = 3,
    PlatformConfiguration = 4,
    ReferenceData = 5,
    ModuleCatalog = 6,
    SubscriptionBilling = 7,
    Quota = 8,
    DataExport = 9,
    DataPrivacy = 10,
    Integration = 11,
    System = 12,
    DocumentManagement = 13,
    MasterData = 14,
    PortfolioDelivery = 15,
    Tasks = 16,
    ESignature = 17,
    Crm = 18
}

public enum AuditOperation
{
    Unknown = 0,
    Create = 1,
    Update = 2,
    Delete = 3,
    Activate = 4,
    Deactivate = 5,
    Suspend = 6,
    Reactivate = 7,
    Assign = 8,
    Revoke = 9,
    Export = 10,
    Redact = 11,
    Login = 12,
    Logout = 13,
    PermissionDenied = 14,
    Execute = 15,
    LifecycleTransition = 16
}

public enum AuditOutcome
{
    Unknown = 0,
    Succeeded = 1,
    Failed = 2,
    Denied = 3
}

public enum AuditActorType
{
    Unknown = 0,
    PlatformAdministrator = 1,
    PartnerAdministrator = 2,
    TenantUser = 3,
    System = 4,
    Service = 5
}
