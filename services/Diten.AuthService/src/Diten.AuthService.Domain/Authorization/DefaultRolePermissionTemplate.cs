using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Domain.Authorization;

/// <summary>
/// Single source of truth for the baseline permissions each default/system role receives.
/// Shared by the persistence <c>DataSeeder</c> (default tenant) and the runtime
/// <c>RoleProvisioningService</c> (every tenant) so the two cannot drift (OD-FE9-03 Option B).
/// Platform permissions are never granted to tenant roles (privilege-escalation boundary).
/// </summary>
public static class DefaultRolePermissionTemplate
{
    public const string SuperAdminRole = "SuperAdmin";
    public const string AdminRole = "Admin";
    public const string ViewerRole = "Viewer";

    public const string PlatformModule = "platform";
    public const string ReadAction = "read";

    // FIX-PERM-ATTRIBUTION-2 — reference-data is a genuine, distinct Module (RoleAssignments grouping,
    // ModulePermissionResolver entitlement matching) but its screens are ALL platform-admin-only
    // (/Platform/ReferenceData/...; tenant gets 403), same escalation boundary as platform.* itself.
    // İŞ3-FAZ0 — this list is now used ONLY by the seed-time Scope classifier (ClassifyScope); live authz
    // decisions read Permission.Scope. Kept as the transition bridge so Faz 0 changes zero behaviour; Faz 2 removes it.
    public static readonly IReadOnlySet<string> PlatformAdminModules =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { PlatformModule, "reference-data" };

    /// <summary>
    /// Modules whose permissions the tenant Admin role receives in full — the curated Admin baseline BREADTH (NOT the
    /// escalation boundary, which is Scope). İŞ3-FAZ1c — now the NEW manifest ModuleCodes only (auth→access-governance,
    /// mdm→legal-entity); the old service-name entries were dropped once the catalog no longer carries them. Owner
    /// decision: the Admin baseline stays a curated module list.
    /// </summary>
    public static readonly IReadOnlyList<string> AdminModules = new[] { "access-governance", "legal-entity" };

    /// <summary>
    /// İŞ3-FAZ0 TRANSITION BRIDGE — derives a permission's authz <see cref="PermissionScope"/> from its existing
    /// (effective) Module classification: <c>PlatformAdmin</c> iff the Module is in <see cref="PlatformAdminModules"/>,
    /// else <c>Tenant</c>. This is by-construction identical to the old Module-name <c>IsPlatform</c>, so wiring Scope
    /// through it keeps the escalation boundary bit-for-bit unchanged in Faz 0. Removed in Faz 2 when Module = ModuleCode.
    /// </summary>
    public static PermissionScope ClassifyScope(string module)
        => PlatformAdminModules.Contains(module) ? PermissionScope.PlatformAdmin : PermissionScope.Tenant;

    /// <summary>
    /// FIX-TENANT-SELFSERVICE-PERMS — curated exception to the platform.* escalation boundary: tenant SELF-SERVICE
    /// capabilities that live under the platform.* namespace but operate ONLY within the tenant's own scope (the
    /// backend forces the caller's tenant_id), so they are NOT an escalation. The tenant Admin role receives exactly
    /// these platform.* keys and no others; matched by Permission.Key (case-insensitive). Add a key here ONLY for a
    /// genuinely tenant-scoped self-service capability.
    /// </summary>
    public static readonly IReadOnlySet<string> TenantSelfServicePermissions =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "platform.tenant-security.read",
            "platform.tenant-security.manage",
            // FIX-MENU-SETTINGS-ACCESS — tenant-scoped Menu (navigation) Settings write. Backend forces the caller's
            // tenant_id, so this platform.* key is self-service, not escalation: tenant Admin holds it, Viewer does not.
            "platform.tenant-navigation.manage"
        };

    /// <summary>
    /// Returns the catalog permissions that <paramref name="roleName"/> should be granted.
    /// Deleted permissions are always excluded; platform permissions are excluded from tenant
    /// roles. Unknown roles get nothing.
    /// </summary>
    public static IReadOnlyList<Permission> SelectFor(string roleName, IEnumerable<Permission> catalog)
    {
        var available = catalog.Where(p => !p.IsDeleted);

        return roleName switch
        {
            // SuperAdmin (default-tenant only) keeps the full catalog.
            SuperAdminRole => available.ToList(),

            // Tenant Admin: its own modules in full, plus the curated tenant self-service platform.* keys
            // (tenant-scoped, not escalation). Every other platform-admin permission stays excluded.
            // İŞ3-FAZ0 — the escalation clause is now Scope-based (`Scope == Tenant`, was `!IsPlatform`); this is
            // bit-identical because Scope is derived via ClassifyScope. The AdminModules BREADTH clause is retained
            // UNCHANGED: dropping it (i.e. granting every Tenant-scoped module) would newly add organization.* and
            // goldenslim.* to the Admin baseline — a real, non-zero delta that Faz 0 forbids. See the delta note above.
            AdminRole => available
                .Where(p => (p.Scope == PermissionScope.Tenant && AdminModules.Contains(p.Module))
                            || TenantSelfServicePermissions.Contains(p.Key))
                .ToList(),

            // FEAT-ROLEPERMS-REDESIGN (Part A) — the tenant-settings (Security/Menu Settings) keys are now a distinct
            // module (Module="tenant-settings"), so they'd otherwise fall into the Viewer read branch. They're a
            // sensitive tenant self-service capability: the Admin gets them via the whitelist clause above, but the
            // default Viewer must NOT — so exclude the curated tenant self-service keys here explicitly.
            // İŞ3-FAZ0 — `!IsPlatform` → `Scope == Tenant` (bit-identical via ClassifyScope).
            ViewerRole => available
                .Where(p => p.Scope == PermissionScope.Tenant
                            && string.Equals(p.Action, ReadAction, StringComparison.OrdinalIgnoreCase)
                            && !TenantSelfServicePermissions.Contains(p.Key))
                .ToList(),

            _ => Array.Empty<Permission>()
        };
    }

    // İŞ3-FAZ0 — the escalation boundary now reads the explicit Scope signal instead of the Module name. Behaviour is
    // unchanged in Faz 0 because Scope is derived from the same PlatformAdminModules set via ClassifyScope.
    public static bool IsPlatform(Permission permission)
        => permission.Scope == PermissionScope.PlatformAdmin;

    /// <summary>
    /// FEAT-ROLEPERMS-TENANT-SCOPE — a permission a TENANT role may hold: any tenant-scoped permission, plus the
    /// curated tenant self-service platform.* keys. This is the escalation boundary for MANUAL assignment
    /// (AssignPermissionCommandHandler) and the tenant-assignable catalog endpoint, mirroring the same boundary that
    /// <see cref="SelectFor"/> enforces during default provisioning so the two cannot drift.
    /// İŞ3-FAZ0 — `!IsPlatform` → `Scope == Tenant` (bit-identical via ClassifyScope).
    /// </summary>
    public static bool IsTenantAssignable(Permission permission)
        => permission.Scope == PermissionScope.Tenant || TenantSelfServicePermissions.Contains(permission.Key);
}
