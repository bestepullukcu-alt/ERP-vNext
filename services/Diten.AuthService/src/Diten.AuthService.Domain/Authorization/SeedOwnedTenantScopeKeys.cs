using System.Collections.Frozen;
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
/// <para><b>What each key gives, and what it does not (CT security review, 2026-10-01).</b>
/// <list type="bullet">
/// <item><c>tasks.approve</c> / <c>tasks.reject</c> — necessary, not sufficient. MOD-0023 also requires the caller to be
/// the task's RESOLVED PRINCIPAL (the one person the step was handed to, not merely one of its candidates), always
/// acts as the signed-in user, and refuses an approval by the person who started the workflow. The key does not let
/// anybody decide a task that is not theirs.</item>
/// <item><c>instances.start</c> — the widest of the three, accepted knowingly: its holder may start any PUBLISHED
/// definition of their own tenant against any object reference, and may name the candidates when the definition's
/// first step names none. Nothing crosses tenants, the starter cannot approve their own instance, and a source module
/// must honour only the instance IT started (CRM, the Task engine, the timesheet and the trusted MDM consumer all bind
/// to their own instance id) — so a stray instance decides nothing; it can only put an approval in somebody's inbox.
/// Requiring definition-fixed candidates on the human start path is recorded as follow-up work, not assumed.</item>
/// </list></para>
///
/// <para><b>What this is the only one of.</b> It is the only exception to the SYNC's tie-break. It is not the only
/// reason a <c>platform.*</c> key is Tenant: a seed literal may declare Tenant (the organisation, tenant-security and
/// tenant-navigation keys do), and the startup seed reconcile lowers an existing row to its literal — that is what
/// lowers these three. The gate on any NEW lowering is <c>permission-scope-baseline.csv</c>
/// (<c>PermissionScopePreservationTests</c>), which fails when a seeded key's scope moves.
/// <see cref="TenantRouteScopeCorrections"/> is the separate, route-sourced correction for SYNCED-ONLY keys the seed
/// cannot reach.</para>
///
/// <para><b>The precondition on an existing database.</b> The exception holds only for a SYSTEM row. A catalog-created
/// row for one of these keys (a database where the sync created the row before the seed carried it) would be lowered by
/// every startup and promoted again by the next sync. <see cref="RowsTheExceptionCannotHold"/> names such rows and the
/// seeder reports them at startup. Measured on the dev catalog 2026-10-01: all three are system rows.</para>
///
/// <para>Adding a key here is an owner + Control Tower decision (escalation boundary), not a code convenience.
/// <c>SeedOwnedTenantScopeKeysTests</c> pins the list to exactly these three keys and checks each one against the
/// production seed literal.</para>
/// </summary>
public static class SeedOwnedTenantScopeKeys
{
    // Frozen: a public HashSet behind IReadOnlySet can be downcast and added to by any code in the process.
    public static readonly IReadOnlySet<string> Keys = new[]
    {
        "platform.workflow.instances.start",
        "platform.workflow.tasks.approve",
        "platform.workflow.tasks.reject"
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>
    /// True when a catalog sync must leave this row Tenant whatever scope it sends: a listed key, on a seeded SYSTEM
    /// row, that is already Tenant. Anything else falls through to the generic most-restrictive-wins rule.
    /// </summary>
    public static bool KeepsTenantScope(Permission existing) =>
        existing.IsSystem
        && existing.Scope == PermissionScope.Tenant
        && Keys.Contains(existing.Key);

    /// <summary>
    /// The listed keys whose live row is NOT a system row — for them <see cref="KeepsTenantScope"/> can never be
    /// true, so the scope would flap between a startup (lowered) and the next catalog sync (promoted). Read-only;
    /// the seeder reports what this returns and changes nothing.
    /// </summary>
    public static IReadOnlyList<string> RowsTheExceptionCannotHold(IEnumerable<Permission> catalog) =>
        (catalog ?? Enumerable.Empty<Permission>())
            .Where(permission => !permission.IsDeleted && !permission.IsSystem && Keys.Contains(permission.Key))
            .Select(permission => permission.Key)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();
}
