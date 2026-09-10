using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Tests.Roles;

// İŞ3-FAZ0 — equivalence net (the project's hard condition). Proves that moving the escalation boundary from the
// Module NAME to the derived Permission.Scope changes NOTHING: for a catalog spanning every distinct seed Module,
// the new Scope-based predicates and grant sets are byte-identical to the old Module-based ones. The old logic is
// reproduced verbatim below as Legacy* reference methods and must stay in lock-step with the live code.
//
// FIX-RBAC-PERM-MODULE-ATTRIBUTION — the legacy oracle can no longer read Permission.Module, because Module is no
// longer the Scope basis: a permission minted under a SERVICE namespace is now attributed to the module that owns
// it ("platform.tenants.read" → Module "tenants"), while its Scope stays classified from the attribution it had
// before ("platform"). Reading the NEW Module here would make the oracle claim platform-admin keys are tenant
// permissions and the equivalence would "pass" by drifting with the code. So every fixture row states its own
// LEGACY attribution and the oracle reads that — the equivalence proves what it always claimed: re-attribution
// moves no permission across the boundary.
public sealed class PermissionScopeEquivalenceTests
{
    // ---- Legacy reference implementation (EXACT pre-Faz-0 Module-name logic; DO NOT "modernise") ----

    // The attribution each fixture permission carried BEFORE the module re-attribution — i.e. `moduleOverride ??
    // keyNamespace`, exactly the ctor's Scope basis. Stated per row in FullCatalog, never recomputed from the rule.
    private static readonly Dictionary<string, string> LegacyModuleByKey = new(StringComparer.Ordinal);

    private static string LegacyModule(Permission p) => LegacyModuleByKey[p.Key];

    private static bool LegacyIsPlatform(Permission p) =>
        DefaultRolePermissionTemplate.PlatformAdminModules.Contains(LegacyModule(p));

    private static bool LegacyIsTenantAssignable(Permission p) =>
        !LegacyIsPlatform(p) || DefaultRolePermissionTemplate.TenantSelfServicePermissions.Contains(p.Key);

    private static List<Permission> LegacySelectFor(string roleName, IEnumerable<Permission> catalog)
    {
        var available = catalog.Where(p => !p.IsDeleted);
        return roleName switch
        {
            DefaultRolePermissionTemplate.SuperAdminRole => available.ToList(),
            DefaultRolePermissionTemplate.AdminRole => available
                .Where(p => (!LegacyIsPlatform(p) && DefaultRolePermissionTemplate.AdminModules.Contains(p.Module))
                            || DefaultRolePermissionTemplate.TenantSelfServicePermissions.Contains(p.Key))
                .ToList(),
            DefaultRolePermissionTemplate.ViewerRole => available
                .Where(p => !LegacyIsPlatform(p)
                            && string.Equals(p.Action, DefaultRolePermissionTemplate.ReadAction, StringComparison.OrdinalIgnoreCase)
                            && !DefaultRolePermissionTemplate.TenantSelfServicePermissions.Contains(p.Key))
                .ToList(),
            _ => new List<Permission>()
        };
    }

    // ---- A catalog covering EVERY distinct DataSeeder Module class + read/non-read + self-service + a deleted row ----

    // Builds a permission AND records its legacy (pre-re-attribution) module attribution in one place, so a fixture
    // row cannot be added without stating which side of the escalation boundary it used to sit on.
    private static Permission P(string module, string resource, string action, string displayName, string? moduleOverride = null)
    {
        var permission = new Permission(module, resource, action, displayName, null, moduleOverride: moduleOverride);
        LegacyModuleByKey[permission.Key] = moduleOverride ?? module;
        return permission;
    }

    private static List<Permission> FullCatalog()
    {
        var deleted = P("platform", "obsolete", "read", "Obsolete"); // platform-admin + deleted
        deleted.IsDeleted = true;

        return
        [
            // auth / mdm — Tenant, AdminModules
            P("auth", "users", "read", "Read User"),
            P("auth", "users", "create", "Create User"),
            P("auth", "roles", "assign-permission", "Assign Permission"),
            P("mdm", "legal-entities", "read", "Read Legal Entity"),
            P("mdm", "legal-entities", "delete", "Delete Legal Entity"),
            P("mdm", "legal-entities", "bulk-delete", "Bulk Delete"),
            P("mdm", "legal-entities", "export", "Export"),

            // organization — Tenant, NOT AdminModules (the delta-sensitive case the zero-delta guard protects)
            P("platform", "positions", "read", "Read Positions", moduleOverride: "organization"),
            P("platform", "positions", "delete", "Delete Positions", moduleOverride: "organization"),
            P("platform", "organization", "read-manager-chain", "Read Manager Chain", moduleOverride: "organization"),

            // goldenslim — Tenant, NOT AdminModules (delta-sensitive)
            P("goldenslim", "records", "read", "Read Golden Slim"),
            P("goldenslim", "records", "create", "Create Golden Slim"),

            // İŞ3-FAZ0-FIX — self-registered/synced modules that are NOT in the DataSeeder seed list. The seed-list
            // Scope reconcile left these without a Scope (limbo), dropping them from tenant-assignable (the regressed
            // 9). Their Module is their own module code (tenant-facing) → ClassifyScope = Tenant → tenant-assignable.
            P("goldenslim", "records", "export", "Export Golden Slim"),
            P("goldenslim", "reports", "view", "View Golden Slim Reports"),
            P("goldencompact", "records", "read", "Read Golden Compact"),
            P("goldencompact", "records", "create", "Create Golden Compact"),
            P("test-beta-mod", "test", "read", "Read Test Beta"),
            P("test-beta-mod", "test", "create", "Create Test Beta"),

            // reference-data — PlatformAdmin (via override), incl. a read (must NOT reach Viewer)
            P("platform", "BusinessReferenceData", "Read", "Read BRD", moduleOverride: "reference-data"),
            P("platform", "BusinessReferenceData", "Create", "Create BRD", moduleOverride: "reference-data"),

            // platform — PlatformAdmin, incl. a read + workflow + document-management (all Module="platform")
            P("platform", "tenants", "read", "Read Tenant"),
            P("platform", "tenants", "create", "Create Tenant"),
            P("platform", "workflow.definitions", "view", "View Workflow"),
            P("platform", "document-management.contract", "view", "View DM Contract"),

            // tenant-settings — Tenant (via override) AND the curated self-service keys (Admin yes, Viewer no)
            P("platform", "tenant-security", "read", "Read Tenant Security", moduleOverride: "tenant-settings"),
            P("platform", "tenant-security", "manage", "Manage Tenant Security", moduleOverride: "tenant-settings"),
            P("platform", "tenant-navigation", "manage", "Manage Tenant Navigation", moduleOverride: "tenant-settings"),

            deleted
        ];
    }

    private static IReadOnlyList<string> Keys(IEnumerable<Permission> perms) =>
        perms.Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();

    // ---- Per-permission predicate equivalence ----

    [Fact]
    public void IsPlatform_is_bit_identical_to_legacy_for_every_permission()
    {
        foreach (var p in FullCatalog())
        {
            Assert.Equal(LegacyIsPlatform(p), DefaultRolePermissionTemplate.IsPlatform(p));
        }
    }

    [Fact]
    public void IsTenantAssignable_is_bit_identical_to_legacy_for_every_permission()
    {
        foreach (var p in FullCatalog())
        {
            Assert.Equal(LegacyIsTenantAssignable(p), DefaultRolePermissionTemplate.IsTenantAssignable(p));
        }
    }

    // ---- Grant-set snapshots (byte-identical old vs new) ----

    [Fact]
    public void Admin_grant_set_is_byte_identical_old_vs_new()
    {
        var catalog = FullCatalog();
        Assert.Equal(
            Keys(LegacySelectFor(DefaultRolePermissionTemplate.AdminRole, catalog)),
            Keys(DefaultRolePermissionTemplate.SelectFor(DefaultRolePermissionTemplate.AdminRole, catalog)));
    }

    [Fact]
    public void Viewer_grant_set_is_byte_identical_old_vs_new()
    {
        var catalog = FullCatalog();
        Assert.Equal(
            Keys(LegacySelectFor(DefaultRolePermissionTemplate.ViewerRole, catalog)),
            Keys(DefaultRolePermissionTemplate.SelectFor(DefaultRolePermissionTemplate.ViewerRole, catalog)));
    }

    [Fact]
    public void SuperAdmin_grant_set_is_byte_identical_old_vs_new()
    {
        var catalog = FullCatalog();
        Assert.Equal(
            Keys(LegacySelectFor(DefaultRolePermissionTemplate.SuperAdminRole, catalog)),
            Keys(DefaultRolePermissionTemplate.SelectFor(DefaultRolePermissionTemplate.SuperAdminRole, catalog)));
    }

    [Fact]
    public void TenantAssignable_acceptance_set_is_byte_identical_old_vs_new()
    {
        var catalog = FullCatalog();
        Assert.Equal(
            Keys(catalog.Where(LegacyIsTenantAssignable)),
            Keys(catalog.Where(DefaultRolePermissionTemplate.IsTenantAssignable)));
    }

    // ---- Zero-delta guard: the retained AdminModules breadth keeps Tenant-scoped-but-non-Admin modules OUT ----

    [Fact]
    public void Admin_excludes_organization_and_goldenslim_zero_delta_guard()
    {
        // If the AdminModules breadth clause were dropped for a naive `Scope == Tenant`, these Tenant-scoped
        // permissions would newly enter the Admin baseline — the exact Faz-0 delta this preserves against.
        var catalog = FullCatalog();
        var adminKeys = Keys(DefaultRolePermissionTemplate.SelectFor(DefaultRolePermissionTemplate.AdminRole, catalog));

        Assert.DoesNotContain("goldenslim.records.read", adminKeys);
        Assert.DoesNotContain("goldenslim.records.create", adminKeys);
        Assert.DoesNotContain("platform.positions.read", adminKeys);      // organization
        Assert.DoesNotContain("platform.positions.delete", adminKeys);    // organization
    }

    // ---- Scope derivation (the ctor bridge) ----

    [Theory]
    [InlineData("auth", null, PermissionScope.Tenant)]
    [InlineData("mdm", null, PermissionScope.Tenant)]
    [InlineData("goldenslim", null, PermissionScope.Tenant)]
    [InlineData("platform", "organization", PermissionScope.Tenant)]
    [InlineData("platform", "tenant-settings", PermissionScope.Tenant)]
    [InlineData("platform", null, PermissionScope.PlatformAdmin)]
    [InlineData("platform", "reference-data", PermissionScope.PlatformAdmin)]
    public void Ctor_derives_scope_from_effective_module(string module, string? moduleOverride, PermissionScope expected)
    {
        var p = new Permission(module, "res", "read", "x", null, moduleOverride: moduleOverride);
        Assert.Equal(expected, p.Scope);

        // FIX-RBAC-PERM-MODULE-ATTRIBUTION — the Scope basis is the attribution BEFORE derivation. Classifying
        // p.Module instead would read "res" for the un-overridden platform row and wrongly call it Tenant.
        Assert.Equal(expected, DefaultRolePermissionTemplate.ClassifyScope(moduleOverride ?? module));
    }

    [Fact]
    public void Explicit_scope_argument_overrides_derivation()
    {
        var p = new Permission("auth", "users", "read", "x", null, scope: PermissionScope.PlatformAdmin);
        Assert.Equal(PermissionScope.PlatformAdmin, p.Scope);
    }

    [Fact]
    public void SetScope_mutates_scope()
    {
        var p = new Permission("auth", "users", "read", "x", null);
        Assert.Equal(PermissionScope.Tenant, p.Scope);
        p.SetScope(PermissionScope.PlatformAdmin);
        Assert.Equal(PermissionScope.PlatformAdmin, p.Scope);
    }

    // ---- İŞ3-FAZ0-FIX: the whole-collection Scope reconcile cannot leave any row in "None"/limbo ----

    // ClassifyScope is TOTAL — every module string (incl. synced module codes absent from every list, and the empty
    // string) maps to a defined Tenant/PlatformAdmin value. So the reconcile, which sets Scope = ClassifyScope(Module)
    // for EVERY row in the collection, can never leave a permission without a valid Scope.
    [Theory]
    [InlineData("goldenslim", PermissionScope.Tenant)]
    [InlineData("goldencompact", PermissionScope.Tenant)]
    [InlineData("test-beta-mod", PermissionScope.Tenant)]
    [InlineData("some-future-synced-module", PermissionScope.Tenant)]
    [InlineData("", PermissionScope.Tenant)]
    [InlineData("platform", PermissionScope.PlatformAdmin)]
    [InlineData("reference-data", PermissionScope.PlatformAdmin)]
    public void ClassifyScope_is_total_and_never_none(string module, PermissionScope expected)
    {
        var scope = DefaultRolePermissionTemplate.ClassifyScope(module);
        Assert.Equal(expected, scope);
        Assert.True(scope is PermissionScope.Tenant or PermissionScope.PlatformAdmin);
    }

    // Post-reconcile invariant (logic level): every permission's Scope equals ClassifyScope of the attribution it was
    // MINTED under. FIX-RBAC-PERM-MODULE-ATTRIBUTION — this used to read p.Module, which was the same value; the two
    // parted company when a service-namespaced key started being attributed to the module that owns it. Scope is the
    // half that must not move, so the invariant now names it explicitly.
    [Fact]
    public void Every_permission_scope_equals_classify_of_its_minted_attribution()
    {
        foreach (var p in FullCatalog().Where(p => !p.IsDeleted))
        {
            Assert.Equal(DefaultRolePermissionTemplate.ClassifyScope(LegacyModule(p)), p.Scope);
        }
    }

    // The companion the split makes necessary: re-attribution changed the GROUPING for exactly the rows minted under
    // a service namespace, and left the boundary alone. If the ctor ever classified Scope from the derived Module,
    // these platform-admin keys would come back Tenant.
    [Fact]
    public void Reattributed_permissions_keep_their_platform_admin_scope()
    {
        var catalog = FullCatalog();

        foreach (var key in new[] { "platform.tenants.read", "platform.tenants.create", "platform.document-management.contract.view" })
        {
            var p = catalog.Single(x => x.Key == key);
            Assert.NotEqual("platform", p.Module);                          // grouping moved to the owning module
            Assert.Equal(PermissionScope.PlatformAdmin, p.Scope);           // boundary did not
            Assert.True(DefaultRolePermissionTemplate.IsPlatform(p));
            Assert.False(DefaultRolePermissionTemplate.IsTenantAssignable(p));
        }
    }

    // The regressed 9 (synced-only tenant modules) are tenant-assignable again under the new Scope logic.
    [Theory]
    [InlineData("goldenslim.records.export")]
    [InlineData("goldenslim.reports.view")]
    [InlineData("goldencompact.records.read")]
    [InlineData("goldencompact.records.create")]
    [InlineData("test-beta-mod.test.read")]
    [InlineData("test-beta-mod.test.create")]
    public void Synced_tenant_module_permissions_are_tenant_assignable(string key)
    {
        var perm = FullCatalog().Single(p => p.Key == key);
        Assert.True(DefaultRolePermissionTemplate.IsTenantAssignable(perm));
        Assert.False(DefaultRolePermissionTemplate.IsPlatform(perm));
    }
}
