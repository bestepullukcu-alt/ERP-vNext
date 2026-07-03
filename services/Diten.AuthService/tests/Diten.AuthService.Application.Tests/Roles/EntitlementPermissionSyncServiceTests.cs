using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;

namespace Diten.AuthService.Application.Tests.Roles;

// S3 / AG-INFRA-COMPLETION — entitlement → role-permission sync. Implements the S2 revoke spec over
// the S1 grant-source fields. Hand-written fakes (codebase convention).
public sealed class EntitlementPermissionSyncServiceTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private const string Actor = "entitlement-sync";

    // Catalog spanning two modules that SHARE one permission key shape (different modules, so distinct
    // Permission rows) is not needed; sharing across modules is modelled via two ModuleGrant rows for
    // the SAME permission id below.
    private static List<Permission> Catalog() =>
    [
        new("mdm", "legal-entities", "read", "Read Legal Entity", null),
        new("mdm", "legal-entities", "create", "Create Legal Entity", null),
        new("mdm", "legal-entities", "delete", "Delete Legal Entity", null),
        new("platform", "tenants", "read", "Read Tenant", null)
    ];

    [Fact]
    public async Task Grant_adds_module_permissions_admin_full_viewer_read_with_source_tag()
    {
        var (svc, roles, rolePerms, catalog) = Build();

        await svc.GrantModuleAsync(TenantA, "MDM", Actor, CancellationToken.None);

        var adminId = roles.IdOf(TenantA, "Admin");
        var viewerId = roles.IdOf(TenantA, "Viewer");

        // Admin: all 3 mdm permissions; Viewer: only the read.
        Assert.Equal(
            new[] { "mdm.legal-entities.read", "mdm.legal-entities.create", "mdm.legal-entities.delete" }.OrderBy(k => k),
            rolePerms.KeysFor(adminId, catalog).OrderBy(k => k));
        Assert.Equal(new[] { "mdm.legal-entities.read" }, rolePerms.KeysFor(viewerId, catalog));

        // All written as Module grants tagged with the normalized module code; platform.* never added.
        Assert.All(rolePerms.Rows, rp => Assert.Equal(GrantSource.Module, rp.GrantSource));
        Assert.All(rolePerms.Rows, rp => Assert.Equal("mdm", rp.SourceModuleCode));
        Assert.DoesNotContain(rolePerms.Rows, rp => catalog.Single(p => p.Id == rp.PermissionId).Key.StartsWith("platform."));
    }

    [Fact]
    public async Task Disable_removes_only_that_modules_grants_and_preserves_system_and_manual()
    {
        var (svc, roles, rolePerms, _) = Build();
        var adminId = roles.IdOf(TenantA, "Admin");
        var mdmRead = CatalogPermissionId("mdm.legal-entities.read");

        // Pre-existing baseline (System) + operator (Manual) grants on the SAME permission key the module also grants.
        rolePerms.Seed(RolePermission.SystemGrant(adminId, mdmRead, TenantA, "system"));
        rolePerms.Seed(RolePermission.ManualGrant(adminId, mdmRead, TenantA, "operator"));

        await svc.GrantModuleAsync(TenantA, "MDM", Actor, CancellationToken.None);
        await svc.RevokeModuleAsync(TenantA, "MDM", Actor, CancellationToken.None);

        var remaining = rolePerms.Rows.Where(rp => rp.RoleId == adminId).ToList();
        Assert.Contains(remaining, rp => rp.GrantSource == GrantSource.System);
        Assert.Contains(remaining, rp => rp.GrantSource == GrantSource.Manual);
        Assert.DoesNotContain(remaining, rp => rp.GrantSource == GrantSource.Module);
    }

    [Fact]
    public async Task Shared_permission_survives_until_the_last_entitlement_is_removed()
    {
        // Two modules ("mdm" and "bank") both granting the same permission row to Admin is simulated by
        // granting "mdm" then seeding a second Module grant from "bank" on one of mdm's permissions.
        var (svc, roles, rolePerms, _) = Build();
        var adminId = roles.IdOf(TenantA, "Admin");
        var mdmRead = CatalogPermissionId("mdm.legal-entities.read");

        await svc.GrantModuleAsync(TenantA, "MDM", Actor, CancellationToken.None);
        rolePerms.Seed(RolePermission.ModuleGrant(adminId, mdmRead, TenantA, Actor, "bank")); // shared via another module

        await svc.RevokeModuleAsync(TenantA, "MDM", Actor, CancellationToken.None);

        var rowsForRead = rolePerms.Rows.Where(rp => rp.RoleId == adminId && rp.PermissionId == mdmRead).ToList();
        Assert.Single(rowsForRead);                              // the mdm row is gone…
        Assert.Equal("bank", rowsForRead[0].SourceModuleCode);   // …the bank row keeps the permission effective
    }

    [Fact]
    public async Task Grant_is_idempotent_no_duplicate_module_grants_on_redelivery()
    {
        var (svc, _, rolePerms, _) = Build();

        await svc.GrantModuleAsync(TenantA, "MDM", Actor, CancellationToken.None);
        var afterFirst = rolePerms.Rows.Count;
        await svc.GrantModuleAsync(TenantA, "MDM", Actor, CancellationToken.None);

        Assert.Equal(afterFirst, rolePerms.Rows.Count);
    }

    [Fact]
    public async Task Disable_is_idempotent_when_nothing_to_remove()
    {
        var (svc, _, rolePerms, _) = Build();

        await svc.RevokeModuleAsync(TenantA, "MDM", Actor, CancellationToken.None); // never granted

        Assert.Empty(rolePerms.Rows);
    }

    [Fact]
    public async Task Only_the_event_tenant_is_affected()
    {
        var (svc, roles, rolePerms, _) = Build();

        await svc.GrantModuleAsync(TenantA, "MDM", Actor, CancellationToken.None);

        var tenantBAdmin = roles.IdOf(TenantB, "Admin");
        Assert.All(rolePerms.Rows, rp => Assert.Equal(TenantA, rp.TenantId));
        Assert.DoesNotContain(rolePerms.Rows, rp => rp.RoleId == tenantBAdmin);
    }

    [Fact]
    public async Task Unmatched_module_code_is_a_no_op()
    {
        var (svc, _, rolePerms, _) = Build();

        await svc.GrantModuleAsync(TenantA, "DOES-NOT-EXIST", Actor, CancellationToken.None);

        Assert.Empty(rolePerms.Rows);
    }

    [Fact]
    public async Task Platform_module_code_is_a_no_op()
    {
        var (svc, _, rolePerms, _) = Build();

        await svc.GrantModuleAsync(TenantA, "platform", Actor, CancellationToken.None);

        Assert.Empty(rolePerms.Rows);
    }

    // ── FIX-2: reconcile against the full entitled set ──

    [Fact]
    public async Task SyncTenantModules_grants_entitled_and_revokes_stale_module_grants_preserving_system_and_manual()
    {
        var catalog = new List<Permission>
        {
            new("mdm", "legal-entities", "read", "", null),
            new("goldenslim", "records", "read", "", null),
            new("goldenslim", "records", "create", "", null)
        };
        var (svc, roles, rolePerms) = BuildWith(catalog);
        var adminId = roles.IdOf(TenantA, "Admin");
        var mdmRead = catalog.Single(p => p.Key == "mdm.legal-entities.read").Id;
        var gsRead = catalog.Single(p => p.Key == "goldenslim.records.read").Id;

        // Prior state: mdm Module-granted (will become stale), plus a System + Manual grant that must survive.
        await svc.GrantModuleAsync(TenantA, "mdm", Actor, CancellationToken.None);
        rolePerms.Seed(RolePermission.SystemGrant(adminId, mdmRead, TenantA, "system"));
        rolePerms.Seed(RolePermission.ManualGrant(adminId, gsRead, TenantA, "operator"));

        // Reconcile to entitled = { goldenslim }: grants goldenslim, revokes the stale mdm Module-grants.
        await svc.SyncTenantModulesAsync(TenantA, new[] { "goldenslim" }, Actor, CancellationToken.None);

        var admin = rolePerms.Rows.Where(rp => rp.RoleId == adminId).ToList();
        Assert.Contains(admin, rp => rp.GrantSource == GrantSource.Module && rp.SourceModuleCode == "goldenslim");
        Assert.DoesNotContain(admin, rp => rp.GrantSource == GrantSource.Module && rp.SourceModuleCode == "mdm");
        Assert.Contains(admin, rp => rp.GrantSource == GrantSource.System);
        Assert.Contains(admin, rp => rp.GrantSource == GrantSource.Manual);
    }

    [Fact]
    public async Task SyncTenantModules_grants_platform_hosted_workflow_via_allow_list_but_not_platform_admin()
    {
        var catalog = new List<Permission>
        {
            new("platform", "workflow.definitions", "view", "", null),
            new("platform", "workflow.tasks", "approve", "", null),
            new("platform", "tenants", "read", "", null) // platform-admin umbrella → must stay blocked
        };
        var (svc, roles, rolePerms) = BuildWith(catalog);
        var adminId = roles.IdOf(TenantA, "Admin");

        await svc.SyncTenantModulesAsync(TenantA, new[] { "workflow" }, Actor, CancellationToken.None);

        var keys = rolePerms.KeysFor(adminId, catalog).ToList();
        Assert.Contains("platform.workflow.definitions.view", keys);
        Assert.Contains("platform.workflow.tasks.approve", keys);
        Assert.DoesNotContain("platform.tenants.read", keys); // escalation boundary preserved
        Assert.All(rolePerms.Rows, rp => Assert.Equal(GrantSource.Module, rp.GrantSource));
        Assert.All(rolePerms.Rows, rp => Assert.Equal("workflow", rp.SourceModuleCode));
    }

    // FIX-2b — a module permission that overlaps an existing System (baseline) grant must NOT cause a duplicate
    // insert (E11000) that aborts the whole sync; later modules (workflow) must still be granted. Idempotent re-run.
    [Fact]
    public async Task SyncTenantModules_with_baseline_overlap_does_not_throw_and_still_grants_other_modules()
    {
        var catalog = new List<Permission>
        {
            new("goldenslim", "records", "read", "", null),
            new("goldenslim", "records", "create", "", null),
            new("platform", "workflow.definitions", "view", "", null)
        };
        var (svc, roles, rolePerms) = BuildWith(catalog);
        rolePerms.EnforceUniqueIndex = true; // behave like the real unique index
        var adminId = roles.IdOf(TenantA, "Admin");
        var gsRead = catalog.Single(p => p.Key == "goldenslim.records.read").Id;

        // The role already holds goldenslim.records.read as a System (baseline) grant — the overlap that the old
        // GrantSource-filtered check missed.
        rolePerms.Seed(RolePermission.SystemGrant(adminId, gsRead, TenantA, "system"));

        // Must not throw, and workflow must be granted despite the goldenslim overlap.
        await svc.SyncTenantModulesAsync(TenantA, new[] { "goldenslim", "workflow" }, Actor, CancellationToken.None);

        var adminKeys = rolePerms.KeysFor(adminId, catalog).ToList();
        Assert.Contains("goldenslim.records.read", adminKeys);                 // still effective (System grant kept)
        Assert.Contains("goldenslim.records.create", adminKeys);              // new Module grant
        Assert.Contains("platform.workflow.definitions.view", adminKeys);    // proves the sync did NOT abort
        // The overlapping permission keeps exactly one row (the System baseline) — never duplicated.
        Assert.Single(rolePerms.Rows.Where(rp => rp.RoleId == adminId && rp.PermissionId == gsRead));
        Assert.Equal(GrantSource.System,
            rolePerms.Rows.Single(rp => rp.RoleId == adminId && rp.PermissionId == gsRead).GrantSource);

        // Idempotent: a second reconcile adds nothing and still does not throw.
        var before = rolePerms.Rows.Count;
        await svc.SyncTenantModulesAsync(TenantA, new[] { "goldenslim", "workflow" }, Actor, CancellationToken.None);
        Assert.Equal(before, rolePerms.Rows.Count);
    }

    // ── FIX-3: catalog-key-driven sync (namespace-agnostic) ──

    // The organization regression: its permissions live across THREE platform.* resource roots, which the
    // Module==ModuleCode convention (and the platform.* escalation boundary) can never resolve. Driving the grant
    // by the module's DECLARED catalog keys grants them anyway — Admin full, Viewer read-only, tagged to the module.
    [Fact]
    public async Task SyncTenantModulesWithKeys_grants_declared_keys_across_platform_namespaces()
    {
        var catalog = new List<Permission>
        {
            new("platform", "organization-units", "read", "", null),
            new("platform", "organization-units", "create", "", null),
            new("platform", "positions", "read", "", null),
            new("platform", "tenants", "read", "", null) // NOT declared by the module → must stay blocked
        };
        var (svc, roles, rolePerms) = BuildWith(catalog);
        var adminId = roles.IdOf(TenantA, "Admin");
        var viewerId = roles.IdOf(TenantA, "Viewer");

        var modules = new[]
        {
            new EntitledModulePermissionKeys("ORGANIZATION", new[]
            {
                "platform.organization-units.read",
                "platform.organization-units.create",
                "platform.positions.read"
            })
        };

        await svc.SyncTenantModulesWithKeysAsync(TenantA, modules, Actor, CancellationToken.None);

        var adminKeys = rolePerms.KeysFor(adminId, catalog).ToList();
        Assert.Contains("platform.organization-units.read", adminKeys);
        Assert.Contains("platform.organization-units.create", adminKeys);
        Assert.Contains("platform.positions.read", adminKeys);
        Assert.DoesNotContain("platform.tenants.read", adminKeys); // undeclared key never leaks in

        // Viewer gets only the read-action declared keys.
        var viewerKeys = rolePerms.KeysFor(viewerId, catalog).OrderBy(k => k).ToList();
        Assert.Equal(new[] { "platform.organization-units.read", "platform.positions.read" }, viewerKeys);

        // Everything tagged as a Module grant against the normalized module code (revoke-by-source safe).
        Assert.All(rolePerms.Rows, rp => Assert.Equal(GrantSource.Module, rp.GrantSource));
        Assert.All(rolePerms.Rows, rp => Assert.Equal("organization", rp.SourceModuleCode));
    }

    // Regression guard: a module that declares NO keys (empty list — e.g. ships no descriptors, or the catalog
    // pull failed) must fall back to the convention + allow-list resolver, so workflow / goldenslim still grant.
    [Fact]
    public async Task SyncTenantModulesWithKeys_empty_keys_falls_back_to_convention()
    {
        var catalog = new List<Permission>
        {
            new("goldenslim", "records", "read", "", null),
            new("platform", "workflow.definitions", "view", "", null),
            new("platform", "tenants", "read", "", null) // platform-admin umbrella → stays blocked
        };
        var (svc, roles, rolePerms) = BuildWith(catalog);
        var adminId = roles.IdOf(TenantA, "Admin");

        var modules = new[]
        {
            new EntitledModulePermissionKeys("goldenslim", Array.Empty<string>()),
            new EntitledModulePermissionKeys("workflow", Array.Empty<string>())
        };

        await svc.SyncTenantModulesWithKeysAsync(TenantA, modules, Actor, CancellationToken.None);

        var adminKeys = rolePerms.KeysFor(adminId, catalog).ToList();
        Assert.Contains("goldenslim.records.read", adminKeys);                // convention
        Assert.Contains("platform.workflow.definitions.view", adminKeys);    // allow-list
        Assert.DoesNotContain("platform.tenants.read", adminKeys);           // escalation boundary preserved
    }

    // ── harness ──

    private static (EntitlementPermissionSyncService svc, FakeRoleRepository roles, FakeRolePermissionRepository rolePerms) BuildWith(List<Permission> catalog)
    {
        var roles = new FakeRoleRepository(TenantA, TenantB);
        var rolePerms = new FakeRolePermissionRepository();
        var svc = new EntitlementPermissionSyncService(new FakePermissionRepository(catalog), roles, rolePerms, NullLogger<EntitlementPermissionSyncService>.Instance);
        return (svc, roles, rolePerms);
    }

    private static readonly List<Permission> SharedCatalog = Catalog();
    private static Guid CatalogPermissionId(string key) => SharedCatalog.Single(p => p.Key == key).Id;

    private static (EntitlementPermissionSyncService svc, FakeRoleRepository roles, FakeRolePermissionRepository rolePerms, List<Permission> catalog) Build()
    {
        var catalog = SharedCatalog;
        var roles = new FakeRoleRepository(TenantA, TenantB);
        var rolePerms = new FakeRolePermissionRepository();
        var svc = new EntitlementPermissionSyncService(new FakePermissionRepository(catalog), roles, rolePerms, NullLogger<EntitlementPermissionSyncService>.Instance);
        return (svc, roles, rolePerms, catalog);
    }

    private sealed class FakeRoleRepository : IRoleRepository
    {
        private readonly Dictionary<(Guid, string), Role> _roles = new();

        public FakeRoleRepository(params Guid[] tenants)
        {
            foreach (var t in tenants)
            {
                foreach (var name in new[] { "Admin", "Viewer" })
                {
                    var role = new Role(name, name, null, t);
                    role.MarkAsSystem();
                    _roles[(t, name)] = role;
                }
            }
        }

        public Guid IdOf(Guid tenantId, string name) => _roles[(tenantId, name)].Id;

        public Task<Role?> GetByNameAndTenantAsync(string name, Guid tenantId, CancellationToken ct)
            => Task.FromResult(_roles.TryGetValue((tenantId, name), out var r) ? r : null);

        public Task<Role?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Role>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> CreateAsync(Role role, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpsertSystemRoleAsync(string name, string displayName, string? description, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpdateAsync(Role role, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakePermissionRepository(List<Permission> catalog) : IPermissionRepository
    {
        public Task<IEnumerable<Permission>> GetAllAsync(CancellationToken ct) => Task.FromResult<IEnumerable<Permission>>(catalog);
        public Task<Permission?> GetByIdAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<Permission?> GetByKeyAsync(string key, CancellationToken ct) => throw new NotSupportedException();
        public Task<Permission?> GetByKeyIncludingDeletedAsync(string key, CancellationToken ct) => throw new NotSupportedException();
        public Task ReactivateAsync(Guid id, string displayName, string? description, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Permission>> GetByModuleAsync(string module, CancellationToken ct) => throw new NotSupportedException();
        public Task<Permission> CreateAsync(Permission permission, CancellationToken ct) => throw new NotSupportedException();
        public Task UpdateAsync(Permission permission, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakeRolePermissionRepository : IRolePermissionRepository
    {
        public List<RolePermission> Rows { get; } = [];

        // Opt-in mirror of the Mongo unique index (RoleId, PermissionId, TenantId): when on, AssignAsync throws
        // on a duplicate exactly like a real E11000. Off by default so unrelated tests are unaffected.
        public bool EnforceUniqueIndex { get; set; }

        public void Seed(RolePermission rp) => Rows.Add(rp);

        public IEnumerable<string> KeysFor(Guid roleId, List<Permission> catalog) =>
            Rows.Where(rp => rp.RoleId == roleId).Select(rp => catalog.Single(p => p.Id == rp.PermissionId).Key);

        public Task AssignAsync(RolePermission rolePermission, CancellationToken ct)
        {
            if (EnforceUniqueIndex && Rows.Any(rp =>
                    rp.RoleId == rolePermission.RoleId
                    && rp.PermissionId == rolePermission.PermissionId
                    && rp.TenantId == rolePermission.TenantId
                    && !rp.IsDeleted))
            {
                throw new InvalidOperationException("E11000 duplicate key (RoleId, PermissionId, TenantId).");
            }

            Rows.Add(rolePermission);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<RolePermission>> GetByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<RolePermission>>(
                Rows.Where(rp => rp.RoleId == roleId && rp.TenantId == tenantId && !rp.IsDeleted).ToList());

        public Task RemoveByIdAsync(Guid id, Guid tenantId, CancellationToken ct)
        {
            Rows.RemoveAll(rp => rp.Id == id && rp.TenantId == tenantId);
            return Task.CompletedTask;
        }

        public Task<IEnumerable<string>> GetPermissionsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<string>> GetPermissionsByRolesAsync(List<Guid> roleIds, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task RevokeAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<long> RemoveByPermissionIdAsync(Guid permissionId, CancellationToken ct) => Task.FromResult(0L);
    }
}
