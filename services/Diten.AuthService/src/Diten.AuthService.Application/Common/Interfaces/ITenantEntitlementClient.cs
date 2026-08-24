namespace Diten.AuthService.Application.Common.Interfaces;

/// <summary>
/// S2S read client to Platform for a tenant's EFFECTIVE entitled module codes (plan-derived + materialized).
/// Plan-derived entitlement is virtual on the Platform side and emits no per-module events, so AuthService pulls
/// the authoritative set here to reconcile its Module-sourced role grants. Best-effort at the call sites: a
/// Platform outage must never block provisioning/login.
/// </summary>
public interface ITenantEntitlementClient
{
    Task<IReadOnlyList<string>> GetEntitledModuleCodesAsync(Guid tenantId, CancellationToken ct);

    /// <summary>
    /// FIX-3 — richer read used by the catalog-key-driven sync: each effectively-Active entitled module plus the
    /// permission keys it DECLARES in Platform's descriptor catalog (page RequiredPermission ∪ action PermissionKey).
    /// Lets AuthService grant a module's exact declared permissions regardless of namespace (e.g. organization's
    /// <c>platform.organization-units.*</c> keys). Best-effort: any failure returns an EMPTY list so the caller can
    /// skip the reconcile (or fall back) without breaking provisioning/login. A module may carry an EMPTY key list
    /// (no descriptors yet) — the caller then falls back to the convention resolver for that module.
    /// </summary>
    Task<IReadOnlyList<EntitledModulePermissionKeys>> GetEntitledModulesWithPermissionKeysAsync(Guid tenantId, CancellationToken ct);

    /// <summary>
    /// Authoritative variant for grant mutation. Confirmed includes an intentionally empty Platform result;
    /// unavailable permits neither grant nor revoke.
    /// </summary>
    Task<TenantEntitlementReadResult> ReadEntitledModulesWithPermissionKeysAsync(Guid tenantId, CancellationToken ct);
}

/// <summary>An entitled module and the permission keys it declares in Platform's descriptor catalog.</summary>
public sealed record EntitledModulePermissionKeys(string ModuleCode, IReadOnlyList<string> PermissionKeys);

public sealed record TenantEntitlementReadResult(
    bool IsAuthoritative,
    IReadOnlyList<EntitledModulePermissionKeys> Modules)
{
    public static TenantEntitlementReadResult Confirmed(IReadOnlyList<EntitledModulePermissionKeys> modules)
        => new(true, modules);

    public static TenantEntitlementReadResult Unavailable()
        => new(false, Array.Empty<EntitledModulePermissionKeys>());
}
