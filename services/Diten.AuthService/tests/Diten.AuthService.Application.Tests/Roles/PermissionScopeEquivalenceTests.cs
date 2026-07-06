using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Tests.Roles;

// İŞ3-FAZ0 — equivalence net (the project's hard condition). Proves that moving the escalation boundary from the
// Module NAME to the derived Permission.Scope changes NOTHING in Faz 0: for a catalog spanning every distinct seed
// Module, the new Scope-based predicates and grant sets are byte-identical to the old Module-based ones. The old
// logic is reproduced verbatim below as Legacy* reference methods and must stay in lock-step with the live code.
public sealed class PermissionScopeEquivalenceTests
{
    // ---- Legacy reference implementation (EXACT pre-Faz-0 Module-name logic; DO NOT "modernise") ----

    private static bool LegacyIsPlatform(Permission p) =>
        DefaultRolePermissionTemplate.PlatformAdminModules.Contains(p.Module);

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

    private static List<Permission> FullCatalog()
    {
        var deleted = new Permission("platform", "obsolete", "read", "Obsolete", null); // platform-admin + deleted
        deleted.IsDeleted = true;

        return
        [
            // auth / mdm — Tenant, AdminModules
            new("auth", "users", "read", "Read User", null),
            new("auth", "users", "create", "Create User", null),
            new("auth", "roles", "assign-permission", "Assign Permission", null),
            new("mdm", "legal-entities", "read", "Read Legal Entity", null),
            new("mdm", "legal-entities", "delete", "Delete Legal Entity", null),
            new("mdm", "legal-entities", "bulk-delete", "Bulk Delete", null),
            new("mdm", "legal-entities", "export", "Export", null),

            // organization — Tenant, NOT AdminModules (the delta-sensitive case the zero-delta guard protects)
            new("platform", "positions", "read", "Read Positions", null, moduleOverride: "organization"),
            new("platform", "positions", "delete", "Delete Positions", null, moduleOverride: "organization"),
            new("platform", "organization", "read-manager-chain", "Read Manager Chain", null, moduleOverride: "organization"),

            // goldenslim — Tenant, NOT AdminModules (delta-sensitive)
            new("goldenslim", "records", "read", "Read Golden Slim", null),
            new("goldenslim", "records", "create", "Create Golden Slim", null),

            // İŞ3-FAZ0-FIX — self-registered/synced modules that are NOT in the DataSeeder seed list. The seed-list
            // Scope reconcile left these without a Scope (limbo), dropping them from tenant-assignable (the regressed
            // 9). Their Module is their own module code (tenant-facing) → ClassifyScope = Tenant → tenant-assignable.
            new("goldenslim", "records", "export", "Export Golden Slim", null),
            new("goldenslim", "reports", "view", "View Golden Slim Reports", null),
            new("goldencompact", "records", "read", "Read Golden Compact", null),
            new("goldencompact", "records", "create", "Create Golden Compact", null),
            new("test-beta-mod", "test", "read", "Read Test Beta", null),
            new("test-beta-mod", "test", "create", "Create Test Beta", null),

            // reference-data — PlatformAdmin (via override), incl. a read (must NOT reach Viewer)
            new("platform", "BusinessReferenceData", "Read", "Read BRD", null, moduleOverride: "reference-data"),
            new("platform", "BusinessReferenceData", "Create", "Create BRD", null, moduleOverride: "reference-data"),

            // platform — PlatformAdmin, incl. a read + workflow + document-management (all Module="platform")
            new("platform", "tenants", "read", "Read Tenant", null),
            new("platform", "tenants", "create", "Create Tenant", null),
            new("platform", "workflow.definitions", "view", "View Workflow", null),
            new("platform", "document-management.contract", "view", "View DM Contract", null),

            // tenant-settings — Tenant (via override) AND the curated self-service keys (Admin yes, Viewer no)
            new("platform", "tenant-security", "read", "Read Tenant Security", null, moduleOverride: "tenant-settings"),
            new("platform", "tenant-security", "manage", "Manage Tenant Security", null, moduleOverride: "tenant-settings"),
            new("platform", "tenant-navigation", "manage", "Manage Tenant Navigation", null, moduleOverride: "tenant-settings"),

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
        Assert.Equal(expected, DefaultRolePermissionTemplate.ClassifyScope(p.Module));
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

    // Post-reconcile invariant (logic level): every permission's Scope equals ClassifyScope(its Module) — the exact
    // state the whole-collection reconcile enforces on Mongo. Covers the synced-only modules too.
    [Fact]
    public void Every_permission_scope_equals_classify_of_its_module()
    {
        foreach (var p in FullCatalog().Where(p => !p.IsDeleted))
        {
            Assert.Equal(DefaultRolePermissionTemplate.ClassifyScope(p.Module), p.Scope);
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
