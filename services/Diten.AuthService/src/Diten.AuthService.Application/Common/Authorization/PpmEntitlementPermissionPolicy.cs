namespace Diten.AuthService.Application.Common.Authorization;

public enum PpmExplicitGrantState
{
    Absent,
    Dormant,
    Effective
}

public enum PpmAuthorizationDenial
{
    None,
    TenantMismatch,
    InvalidModuleCode,
    UnknownPermission,
    EntitlementInactive,
    ExplicitGrantMissing
}

public sealed record PpmAuthorizationDecision(
    bool IsAllowed,
    PpmExplicitGrantState GrantState,
    PpmAuthorizationDenial Denial,
    bool AuditRequired);

public sealed record PpmEntitlementTransition(
    bool ExplicitGrantPreserved,
    bool CreatesGrant,
    bool AuditRequired,
    string? AuditAction);

/// <summary>
/// Pure PPM two-gate policy. Persistence and token projection must consume
/// this decision without normalizing identities or creating/deleting grants.
/// </summary>
public static class PpmEntitlementPermissionPolicy
{
    public const string EntitlementActivatedAuditAction = "ppm.entitlement.activated";
    public const string EntitlementDeactivatedAuditAction = "ppm.entitlement.deactivated";

    public static bool RequiresExplicitGrant(string? moduleCode) =>
        PpmPermissionCatalog.IsModuleCode(moduleCode);

    public static PpmAuthorizationDecision Evaluate(
        Guid requestTenantId,
        Guid grantTenantId,
        string? moduleCode,
        string? permissionKey,
        bool hasActiveEntitlement,
        bool hasExplicitGrant)
    {
        if (requestTenantId == Guid.Empty || requestTenantId != grantTenantId)
        {
            return Denied(PpmAuthorizationDenial.TenantMismatch, PpmExplicitGrantState.Absent);
        }

        if (!PpmPermissionCatalog.IsModuleCode(moduleCode))
        {
            return Denied(PpmAuthorizationDenial.InvalidModuleCode, PpmExplicitGrantState.Absent);
        }

        if (!PpmPermissionCatalog.Contains(permissionKey))
        {
            return Denied(PpmAuthorizationDenial.UnknownPermission, PpmExplicitGrantState.Absent);
        }

        if (!hasExplicitGrant)
        {
            return Denied(PpmAuthorizationDenial.ExplicitGrantMissing, PpmExplicitGrantState.Absent);
        }

        if (!hasActiveEntitlement)
        {
            return Denied(PpmAuthorizationDenial.EntitlementInactive, PpmExplicitGrantState.Dormant);
        }

        return new(true, PpmExplicitGrantState.Effective, PpmAuthorizationDenial.None, false);
    }

    public static string AuditActionForEntitlement(bool isActive) =>
        isActive ? EntitlementActivatedAuditAction : EntitlementDeactivatedAuditAction;

    public static PpmEntitlementTransition EvaluateEntitlementTransition(
        bool wasActive,
        bool isActive,
        bool hasExplicitGrant)
    {
        var changed = wasActive != isActive;
        return new(
            ExplicitGrantPreserved: hasExplicitGrant,
            CreatesGrant: false,
            AuditRequired: changed,
            AuditAction: changed ? AuditActionForEntitlement(isActive) : null);
    }

    private static PpmAuthorizationDecision Denied(
        PpmAuthorizationDenial denial,
        PpmExplicitGrantState state) => new(false, state, denial, true);
}
