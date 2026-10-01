using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Domain.Authorization;

/// <summary>
/// MOD-0018-FU24/FU25, decided by the owner + Control Tower on 2026-10-01 — the ONLY sanctioned exception to the
/// catalog sync's "most restrictive wins" tie-break (<c>InternalPermissionsController.Sync</c>).
///
/// <para><b>Why it exists.</b> Starting a workflow instance and approving or rejecting an assigned approval task are
/// things a TENANT user does from the Task Center; the three keys below are therefore Tenant in the seed
/// (<c>DataSeeder.BuildCanonicalPermissions</c>), and the startup seed-list reconcile
/// (<c>ReconcilePermissionScopesAsync</c>) brings an existing row to that value. The Workflow manifest, however,
/// registers the same keys on a <c>/Platform/...</c> page, so the route rule makes every catalog sync send
/// PlatformAdmin for them. Without this list the tie-break would promote the three rows back to PlatformAdmin on the
/// next manifest replay, and no tenant role could be granted them (AssignPermission answers 403) — an approver who is
/// not an administrator could not be given the approval permission at all.</para>
///
/// <para><b>Why it is not a general mechanism.</b> Scope is the tenant/platform-admin escalation boundary. This is a
/// closed list of exact keys, and <see cref="KeepsTenantScope"/> holds only when ALL of these are true: the key is on
/// the list; the row is a SYSTEM row (seeded — a catalog-created row never qualifies); the row is ALREADY Tenant (the
/// seed put it there). It works in one direction only — "keep it Tenant against an incoming PlatformAdmin". It never
/// lowers a PlatformAdmin row, and it grants and revokes nothing: no rolePermissions row is added or removed by it.
/// Every other <c>platform.workflow.*</c> key (definitions.*, instances.view, tasks.delegate, tasks.request-info,
/// tasks.cancel, transitions.*, escalations.*) stays PlatformAdmin under the generic tie-break.</para>
///
/// <para><b>What the permission alone does not give.</b> Holding the key is necessary, not sufficient: MOD-0023 also
/// requires the person to be a candidate of that approval step.</para>
///
/// <para><b>The two paths, and no third.</b> <see cref="TenantRouteScopeCorrections"/> is the only ROUTE-sourced
/// PlatformAdmin → Tenant correction (synced-only keys); this class is the only SEED-sourced exception. Outside these
/// two there is no way for a platform key to be, or to stay, Tenant.</para>
///
/// <para>Adding a key here is an owner + Control Tower decision (escalation boundary), not a code convenience.
/// <c>SeedOwnedTenantScopeKeysTests</c> pins the list to exactly these three keys and checks each one against the
/// production seed literal.</para>
/// </summary>
public static class SeedOwnedTenantScopeKeys
{
    public static readonly IReadOnlySet<string> Keys = new HashSet<string>(StringComparer.Ordinal)
    {
        "platform.workflow.instances.start",
        "platform.workflow.tasks.approve",
        "platform.workflow.tasks.reject"
    };

    /// <summary>
    /// True when a catalog sync must leave this row Tenant whatever scope it sends: a listed key, on a seeded SYSTEM
    /// row, that is already Tenant. Anything else falls through to the generic most-restrictive-wins rule.
    /// </summary>
    public static bool KeepsTenantScope(Permission existing) =>
        existing.IsSystem
        && existing.Scope == PermissionScope.Tenant
        && Keys.Contains(existing.Key);
}
