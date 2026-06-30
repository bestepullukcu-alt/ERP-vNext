namespace Diten.AuthService.Application.Common.Interfaces;

/// <summary>
/// Synchronises a tenant's role permissions with its module entitlements (the AuthService side of the
/// entitlement → role-permission bridge). Transport-agnostic: the eventing consumer calls this; the
/// rules are fixed in <c>docs/entitlement-permission-bridge.md</c> (S2).
/// </summary>
public interface IEntitlementPermissionSyncService
{
    /// <summary>Module entitlement added/enabled: grant the module's permissions to the tenant roles.</summary>
    Task GrantModuleAsync(Guid tenantId, string moduleCode, string actor, CancellationToken ct = default);

    /// <summary>Module entitlement removed/disabled: drop only this module's source-tagged grants.</summary>
    Task RevokeModuleAsync(Guid tenantId, string moduleCode, string actor, CancellationToken ct = default);

    /// <summary>
    /// Reconciles a tenant's Module-sourced grants against its FULL effective entitled-module set (plan-derived +
    /// materialized). Grants every entitled module's permissions and revokes Module-grants whose source module is
    /// no longer entitled. Idempotent; never touches System (baseline) or Manual (operator) grants. Used at tenant
    /// provisioning and on plan/subscription change, where per-module add/disable events are not emitted.
    /// </summary>
    Task SyncTenantModulesAsync(Guid tenantId, IReadOnlyCollection<string> entitledModuleCodes, string actor, CancellationToken ct = default);

    /// <summary>
    /// FIX-3 — grants a single module using the permission keys it DECLARES in Platform's descriptor catalog
    /// (namespace-agnostic, Key → Permission → grant). The catalog key set is the authoritative boundary, so a
    /// tenant receives only the permissions its entitled module declares. When <paramref name="permissionKeys"/>
    /// is null/empty (module declares no descriptors, or the catalog pull failed), falls back to
    /// <see cref="GrantModuleAsync"/> (the convention + allow-list resolver) — never a no-op upgrade-by-silence.
    /// </summary>
    Task GrantModuleWithKeysAsync(Guid tenantId, string moduleCode, IReadOnlyCollection<string> permissionKeys, string actor, CancellationToken ct = default);

    /// <summary>
    /// FIX-3 — catalog-key-driven counterpart of <see cref="SyncTenantModulesAsync"/>. Grants each entitled
    /// module via its declared catalog key set (with per-module convention fallback) and revokes Module-grants
    /// whose source module is no longer entitled. Idempotent; never touches System/Manual grants. Used at tenant
    /// provisioning and on plan/subscription change.
    /// </summary>
    Task SyncTenantModulesWithKeysAsync(Guid tenantId, IReadOnlyCollection<EntitledModulePermissionKeys> modules, string actor, CancellationToken ct = default);
}
