namespace Diten.Platform.Common.Authorization;

public sealed record EntitlementAuditDenyContext(
    Guid TenantId,
    string? ActorType,
    EntitlementKind EntitlementKind,
    string Code,
    EntitlementDenyReason? DenyReason,
    bool IsTransientFailure,
    string Source,
    string RequirementName);
