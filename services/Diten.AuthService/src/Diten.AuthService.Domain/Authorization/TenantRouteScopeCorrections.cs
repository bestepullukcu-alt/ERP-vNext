using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Domain.Authorization;

/// <summary>
/// BL-411 (CT benchmark D3, 2026-09-15) — the ONLY sanctioned ROUTE-sourced PlatformAdmin → Tenant scope correction
/// in AuthService.
///
/// <para><b>Where this sits (owner + CT, 2026-10-01).</b> This class is the only ROUTE-sourced lowering, and it exists
/// for SYNCED-ONLY keys: rows the seed has no literal for, which the seed reconcile therefore cannot reach. A SEEDED
/// key's scope is its seed literal, and the startup seed reconcile lowers (or raises) an existing row to it — the gate
/// on that is <c>permission-scope-baseline.csv</c>. <see cref="SeedOwnedTenantScopeKeys"/> is a third, different thing:
/// it lowers nothing; it is the one exception to the catalog sync's tie-break, so that three Workflow execution keys
/// the seed declares Tenant are not promoted back. The list below (the two BL-411 keys) is unchanged by that decision.</para>
///
/// <para><b>Why it exists.</b> <c>platform.tasks.checklist-templates.manage</c> and <c>platform.tasks.templates.manage</c>
/// are tenant configuration (Blueprint MOD-0024 system of record "Task templates, checklist templates"; tenant-scoped
/// entities; tenant routes /Tasks/ChecklistTemplates and /Tasks/Templates, which Platform's route rule derives as
/// Tenant). In the dev catalog both rows were created by Platform's A1 auto-registration worker, which syncs with
/// Module/Scope = null (<c>PlatformPermissionAutoRegistrationWorker</c>), before the Tasks manifest reached Auth, so the
/// Permission constructor classified the "platform" key prefix as PlatformAdmin. The catalog sync's tie-break never
/// downgrades (<c>InternalPermissionsController.Sync</c>), so every later manifest sync that sends Tenant leaves them
/// PlatformAdmin, and no tenant role can be granted them (AssignPermission answers 403).</para>
///
/// <para><b>Why it is not a general mechanism.</b> Scope is the tenant/platform-admin escalation boundary. A rule of
/// "lower any key whose route looks tenant" would let a single wrong route or a single wrong sender open platform
/// permissions to tenant roles. So this is a closed list of exact keys, each with the page route its manifest
/// registers, and three gates that must ALL hold before a row changes: the key is on the list; the row is currently
/// PlatformAdmin (so a second run changes nothing); the registered route is a tenant route (the same rule as Platform's
/// <c>ModulePageDescriptorNormalizer.ScopeFromRoute</c>). It never raises a scope, and the correction run itself adds or
/// removes no grant. <c>TenantRouteScopeCorrectionTests</c> cross-checks each entry against the production Tasks
/// manifest and Platform's route rule, and pins the list to exactly these two keys.</para>
///
/// <para><b>What changes for grants afterwards (CT decision 2026-09-15, accepted as intended).</b> Once the keys are
/// Tenant, the next entitlement sync for a tenant entitled to <c>tasks</c> gives that tenant's Admin role both keys
/// (Admin receives the module's full permission set), and Viewer neither (Viewer receives read actions only); a tenant
/// administrator may also grant them to any tenant role through AssignPermission. MOD-0024 templates are tenant
/// configuration maintained by the tenant's administrators.</para>
///
/// <para><b>Gate 3 today.</b> Both allowlisted routes are tenant routes (proven from the production manifest), so the
/// route gate cannot reject anything with the current list; it stays as a fail-closed guard for a future entry.</para>
///
/// <para>Adding a key here is a Control Tower decision (escalation boundary, risk R3), not a code convenience.</para>
/// </summary>
public static class TenantRouteScopeCorrections
{
    /// <summary>One allowlisted key and the page route its module manifest registers it on.</summary>
    public sealed record Entry(string PermissionKey, string RegisteredRoutePath);

    /// <summary>One row the planner decided to correct. Carries no target scope: the only write is PlatformAdmin → Tenant.</summary>
    public sealed record Correction(Guid PermissionId, string PermissionKey, string RegisteredRoutePath);

    public static readonly IReadOnlyList<Entry> Allowlist =
    [
        new("platform.tasks.checklist-templates.manage", "/Tasks/ChecklistTemplates"),
        new("platform.tasks.templates.manage", "/Tasks/Templates"),
    ];

    /// <summary>
    /// Same rule as Platform's <c>ModulePageDescriptorNormalizer.ScopeFromRoute</c> (the author of every route-derived
    /// Scope the catalog sync carries): <c>/Platform</c> and <c>/Platform/...</c> are platform-admin, everything else is
    /// tenant. A blank route is NOT treated as tenant here — an entry without a real route never corrects anything.
    /// </summary>
    public static bool IsTenantRoute(string? routePath)
    {
        var route = (routePath ?? string.Empty).Trim();
        if (route.Length == 0)
        {
            return false;
        }

        var isPlatform = route.StartsWith("/Platform/", StringComparison.OrdinalIgnoreCase)
                         || string.Equals(route, "/Platform", StringComparison.OrdinalIgnoreCase);
        return !isPlatform;
    }

    /// <summary>
    /// Pure core: decides which rows need the correction. No IO, so a test can drive it with a hand-built catalog.
    /// Soft-deleted rows are included on purpose — Scope belongs to the key, and a catalog reactivation does not
    /// touch Scope, so a deleted row left PlatformAdmin would come back PlatformAdmin.
    /// </summary>
    public static IReadOnlyList<Correction> Plan(IEnumerable<Permission> permissions)
    {
        var plan = new List<Correction>();

        foreach (var permission in permissions ?? Enumerable.Empty<Permission>())
        {
            // Gate 1 — narrowness: exact, allowlisted keys only.
            var entry = Allowlist.FirstOrDefault(e => string.Equals(e.PermissionKey, permission.Key, StringComparison.Ordinal));
            if (entry is null)
            {
                continue;
            }

            // Gate 2 — idempotency: a row already Tenant is left alone, so a re-run is a no-op.
            if (permission.Scope != PermissionScope.PlatformAdmin)
            {
                continue;
            }

            // Gate 3 — the registered page route must be a tenant route.
            if (!IsTenantRoute(entry.RegisteredRoutePath))
            {
                continue;
            }

            plan.Add(new Correction(permission.Id, permission.Key, entry.RegisteredRoutePath));
        }

        return plan;
    }
}
