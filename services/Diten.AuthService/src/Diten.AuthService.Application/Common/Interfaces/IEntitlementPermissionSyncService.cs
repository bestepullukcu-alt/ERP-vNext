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
}
