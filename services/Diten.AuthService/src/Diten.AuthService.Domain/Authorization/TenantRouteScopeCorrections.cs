using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Domain.Authorization;

/// <summary>
/// BL-411 (CT benchmark D3, 2026-09-15) — the ONLY sanctioned PlatformAdmin → Tenant scope correction in AuthService.
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
/// removes no grant. <c>TenantRouteScopeCorrectionTests</c> cross-checks each entry against the production Tasks and
/// CRM manifests and Platform's route rule, and pins the list to exactly these five keys (the two BL-411 template keys
/// and, since WP-VW-W3-AUTH, the three document-repository visit-evidence keys).</para>
///
/// <para><b>What changes for grants afterwards (CT decision 2026-09-15, accepted as intended).</b> Once the keys are
/// Tenant, the next entitlement sync for a tenant entitled to <c>tasks</c> gives that tenant's Admin role both keys
/// (Admin receives the module's full permission set), and Viewer neither (Viewer receives read actions only); a tenant
/// administrator may also grant them to any tenant role through AssignPermission. MOD-0024 templates are tenant
/// configuration maintained by the tenant's administrators. WP-VW-W3-AUTH: the same path is expected for a tenant entitled to
/// <c>crm</c> and the three visit-evidence keys (the CRM manifest declares them on /CRM/VisitWorkspace).</para>
///
/// <para><b>Gate 3 today.</b> Every allowlisted route is a tenant route (proven from the production manifest), so the
/// route gate cannot reject anything with the current list; it stays as a fail-closed guard for a future entry.</para>
///
/// <para><b>WP-0023-FX (owner decision 2026-10-07).</b> Seven workflow RUNTIME keys were added (start, view, act on a
/// task). Their registration is not a page route but a Tenant API permission of the Workflow manifest, so the entry
/// kind <see cref="RegistrationKind.TenantApiPermission"/> was introduced; Gate 3 accepts it only with a named module
/// and no route. The tests pin the list to the two BL-411 keys plus these seven, and prove from the production
/// Workflow manifest that each of the seven is declared Tenant there and on no page — the template design keys are
/// declared on /Platform pages, which is why adding one of them here turns the tests red. Every applied row is also
/// written to <c>authAuditLogs</c> (<c>permission_scope_corrected</c>).</para>
///
/// <para>Adding a key here is a Control Tower decision (escalation boundary, risk R3), not a code convenience.</para>
/// </summary>
public static class TenantRouteScopeCorrections
{
    /// <summary>How the owning manifest registers an allowlisted key as tenant-scoped.</summary>
    public enum RegistrationKind
    {
        /// <summary>A page or page action on a tenant route (the route derives Tenant, BL-411).</summary>
        TenantPageRoute = 0,

        /// <summary>
        /// WP-0023-FX — an API-only permission the manifest declares with Scope "Tenant"
        /// (module-self-registration-standard §2d) and on no /Platform page.
        /// </summary>
        TenantApiPermission = 1,
    }

    /// <summary>
    /// One allowlisted key and how its module manifest registers it: a tenant page route, or (WP-0023-FX) a Tenant
    /// API permission of <paramref name="DeclaringModuleCode"/>. <paramref name="Decision"/> names the CT/owner
    /// decision that put it on the list.
    /// </summary>
    public sealed record Entry(
        string PermissionKey,
        string? RegisteredRoutePath,
        RegistrationKind Kind = RegistrationKind.TenantPageRoute,
        string? DeclaringModuleCode = null,
        string Decision = "BL-411");

    /// <summary>
    /// One row the planner decided to correct. Carries no target scope: the only write is PlatformAdmin → Tenant.
    /// For an API permission <paramref name="RegisteredRoutePath"/> reads <c>api-permission:{module}</c>.
    /// </summary>
    public sealed record Correction(Guid PermissionId, string PermissionKey, string RegisteredRoutePath, string Decision = "BL-411");

    public static readonly IReadOnlyList<Entry> Allowlist =
    [
        new("platform.tasks.checklist-templates.manage", "/Tasks/ChecklistTemplates"),
        new("platform.tasks.templates.manage", "/Tasks/Templates"),

        // WP-VW-W3-AUTH (CT decision 2026-10-12, WP-VW-W3 §37, open blocker "permission scope") — the document repository's
        // scope-bound visit-evidence keys (W3-PLT). They open ONLY the CrmVisitEvidence scope (a rep's own photo and
        // signature; read-all = the tenant's evidence for a manager), so by nature they are tenant permissions. Platform's
        // scopeless A1 auto-registration can create them before the CRM manifest reaches Auth, which stamps them
        // PlatformAdmin, and the sync tie-break never lowers that. Their page home is the CRM Visit Workspace.
        // The GENERAL platform.document-repository.read / .manage are deliberately NOT here: they stay PlatformAdmin.
        new("platform.document-repository.visit-evidence.write", "/CRM/VisitWorkspace"),
        new("platform.document-repository.visit-evidence.read", "/CRM/VisitWorkspace"),
        new("platform.document-repository.visit-evidence.read-all", "/CRM/VisitWorkspace"),
        // WP-0023-FX (owner decision 2026-10-07) — workflow RUNTIME keys. They were PlatformAdmin only because the
        // Workflow manifest declared them on /Platform/Workflow pages; a QA role must be able to start, see and act
        // on approvals. The Workflow manifest now declares exactly these seven as Tenant API permissions and none of
        // them on a page (pinned by TenantRouteScopeCorrectionTests against the production manifest). Template design,
        // publish, SLA, escalation and transition-gate keys are NOT here and stay PlatformAdmin.
        new("platform.workflow.instances.start", null, RegistrationKind.TenantApiPermission, "workflow", "WP-0023-FX"),
        new("platform.workflow.instances.view", null, RegistrationKind.TenantApiPermission, "workflow", "WP-0023-FX"),
        new("platform.workflow.tasks.approve", null, RegistrationKind.TenantApiPermission, "workflow", "WP-0023-FX"),
        new("platform.workflow.tasks.reject", null, RegistrationKind.TenantApiPermission, "workflow", "WP-0023-FX"),
        new("platform.workflow.tasks.delegate", null, RegistrationKind.TenantApiPermission, "workflow", "WP-0023-FX"),
        new("platform.workflow.tasks.request-info", null, RegistrationKind.TenantApiPermission, "workflow", "WP-0023-FX"),
        new("platform.workflow.tasks.cancel", null, RegistrationKind.TenantApiPermission, "workflow", "WP-0023-FX"),
    ];

    /// <summary>True when <paramref name="permissionKey"/> is on the list (exact, ordinal).</summary>
    public static bool IsAllowlisted(string? permissionKey) =>
        permissionKey is not null && Allowlist.Any(e => string.Equals(e.PermissionKey, permissionKey, StringComparison.Ordinal));

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

            // Gate 3 — the registration must be tenant: a tenant page route, or a Tenant API permission of a named
            // module with no route at all (a route on an API entry is a malformed entry and never qualifies).
            var registration = entry.Kind switch
            {
                RegistrationKind.TenantPageRoute when IsTenantRoute(entry.RegisteredRoutePath) => entry.RegisteredRoutePath!.Trim(),
                RegistrationKind.TenantApiPermission
                    when entry.RegisteredRoutePath is null && !string.IsNullOrWhiteSpace(entry.DeclaringModuleCode)
                    => $"api-permission:{entry.DeclaringModuleCode}",
                _ => null
            };
            if (registration is null)
            {
                continue;
            }

            plan.Add(new Correction(permission.Id, permission.Key, registration, entry.Decision));
        }

        return plan;
    }
}
