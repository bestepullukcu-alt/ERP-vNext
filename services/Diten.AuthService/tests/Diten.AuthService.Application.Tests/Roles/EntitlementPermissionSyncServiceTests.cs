using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Domain.Authorization;
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

    private static List<Permission> ProductItemSkuMasterCatalog() =>
    [
        new("mdm", "global-products", "read", "Read Global Products", null, moduleOverride: "product-item-sku-master"),
        new("mdm", "global-products", "create", "Create Global Products", null, moduleOverride: "product-item-sku-master"),
        .. ProductAbbreviationEntitlementGrantProfile.PermissionKeys
            .Select(key => new Permission(
                "mdm",
                "product-abbreviations",
                key[(key.LastIndexOf('.') + 1)..],
                key,
                null,
                moduleOverride: "product-item-sku-master"))
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
            new("platform", "workflow.definitions", "view", "", null, moduleOverride: "workflow"),
            new("platform", "workflow.tasks", "approve", "", null, moduleOverride: "workflow"),
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
            new("platform", "workflow.definitions", "view", "", null, moduleOverride: "workflow")
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
        Assert.Single(rolePerms.Rows, rp => rp.RoleId == adminId && rp.PermissionId == gsRead);
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
            new("platform", "workflow.definitions", "view", "", null, moduleOverride: "workflow"),
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

    [Fact]
    public async Task Product_Item_Sku_Master_active_entitlement_grants_exact_role_matrix_idempotently_and_tenant_scoped()
    {
        var catalog = new List<Permission>
        {
            new("mdm", "global-products", "read", "Read Global Products", null,
                moduleOverride: "product-item-sku-master"),
            new("mdm", "global-products", "create", "Create Global Products", null,
                moduleOverride: "product-item-sku-master"),
            new("mdm", "finished-goods", "read", "Read Finished Goods", null,
                moduleOverride: "product-item-sku-master"),
            new("mdm", "finished-goods", "create", "Create Finished Goods", null,
                moduleOverride: "product-item-sku-master"),
            new("mdm", "gskus", "read", "Read GSKUs", null,
                moduleOverride: "product-item-sku-master"),
            new("mdm", "gskus", "create", "Create GSKUs", null,
                moduleOverride: "product-item-sku-master"),
            new("mdm", "lskus", "read", "Read LSKUs", null,
                moduleOverride: "product-item-sku-master"),
            new("mdm", "lskus", "create", "Create LSKUs", null,
                moduleOverride: "product-item-sku-master"),
            new("mdm", "gskus", "update", "Update GSKUs", null,
                moduleOverride: "product-item-sku-master"),
            new("mdm", "product-abbreviations", "read", "Read Product Abbreviations", null,
                moduleOverride: "product-abbreviation-register")
        };
        var (svc, roles, rolePerms) = BuildWith(catalog);
        var module = new EntitledModulePermissionKeys("product-item-sku-master",
            [
                "mdm.global-products.read",
                "mdm.global-products.create",
                "mdm.finished-goods.read",
                "mdm.finished-goods.create",
                "mdm.gskus.read",
                "mdm.gskus.create",
                "mdm.lskus.read",
                "mdm.lskus.create"
            ]);

        await svc.SyncTenantModulesWithKeysAsync(TenantA, [module], Actor, CancellationToken.None);
        var afterFirst = rolePerms.Rows.Count;
        await svc.SyncTenantModulesWithKeysAsync(TenantA, [module], Actor, CancellationToken.None);

        Assert.Equal(afterFirst, rolePerms.Rows.Count);
        Assert.Equal(
            [
                "mdm.finished-goods.create",
                "mdm.finished-goods.read",
                "mdm.global-products.create",
                "mdm.global-products.read",
                "mdm.gskus.create",
                "mdm.gskus.read",
                "mdm.lskus.create",
                "mdm.lskus.read"
            ],
            rolePerms.KeysFor(roles.IdOf(TenantA, "Admin"), catalog).OrderBy(k => k).ToArray());
        Assert.Equal(
            ["mdm.finished-goods.read", "mdm.global-products.read", "mdm.gskus.read", "mdm.lskus.read"],
            rolePerms.KeysFor(roles.IdOf(TenantA, "Viewer"), catalog).OrderBy(k => k).ToArray());
        Assert.DoesNotContain(
            rolePerms.KeysFor(roles.IdOf(TenantA, "Viewer"), catalog),
            key => key.EndsWith(".create", StringComparison.Ordinal));
        Assert.All(rolePerms.Rows, rp => Assert.Equal("product-item-sku-master", rp.SourceModuleCode));
        Assert.All(rolePerms.Rows, rp => Assert.Equal(TenantA, rp.TenantId));
        Assert.DoesNotContain(rolePerms.Rows, rp => rp.TenantId == TenantB);
        Assert.Empty(rolePerms.KeysFor(roles.IdOf(TenantA, "Custom"), catalog));
        Assert.DoesNotContain(rolePerms.Rows, rp => rp.PermissionId == catalog.Single(p => p.Key == "mdm.gskus.update").Id);
        Assert.DoesNotContain(rolePerms.Rows, rp => rp.PermissionId == catalog.Single(p => p.Key == "mdm.product-abbreviations.read").Id);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("disabled")]
    [InlineData("expired")]
    public async Task Product_Item_Sku_Master_non_active_entitlement_denies_both_LSKU_permissions(string entitlementState)
    {
        // The authoritative entitlement reader represents each of these states by omitting the module from the
        // active set. The generic sync must therefore grant nothing; it must not infer an entitlement from the
        // module code or catalog entries.
        Assert.Contains(entitlementState, new[] { "missing", "disabled", "expired" });
        var catalog = new List<Permission>
        {
            new("mdm", "lskus", "read", "Read LSKUs", null,
                moduleOverride: "product-item-sku-master"),
            new("mdm", "lskus", "create", "Create LSKUs", null,
                moduleOverride: "product-item-sku-master")
        };
        var (svc, roles, rolePerms) = BuildWith(catalog);

        await svc.SyncTenantModulesWithKeysAsync(
            TenantA,
            Array.Empty<EntitledModulePermissionKeys>(),
            Actor,
            CancellationToken.None);

        Assert.Empty(rolePerms.KeysFor(roles.IdOf(TenantA, "Admin"), catalog));
        Assert.Empty(rolePerms.KeysFor(roles.IdOf(TenantA, "Viewer"), catalog));
        Assert.Empty(rolePerms.Rows);
    }

    [Fact]
    public async Task Product_Item_Sku_Master_revoke_removes_only_matching_module_source_for_the_event_tenant()
    {
        var catalog = new List<Permission>
        {
            new("mdm", "global-products", "read", "Read Global Products", null,
                moduleOverride: "product-item-sku-master"),
            new("mdm", "global-products", "create", "Create Global Products", null,
                moduleOverride: "product-item-sku-master"),
            new("mdm", "finished-goods", "read", "Read Finished Goods", null,
                moduleOverride: "product-item-sku-master"),
            new("mdm", "finished-goods", "create", "Create Finished Goods", null,
                moduleOverride: "product-item-sku-master"),
            new("mdm", "gskus", "read", "Read GSKUs", null,
                moduleOverride: "product-item-sku-master"),
            new("mdm", "gskus", "create", "Create GSKUs", null,
                moduleOverride: "product-item-sku-master"),
            new("mdm", "lskus", "read", "Read LSKUs", null,
                moduleOverride: "product-item-sku-master"),
            new("mdm", "lskus", "create", "Create LSKUs", null,
                moduleOverride: "product-item-sku-master"),
            new("mdm", "product-abbreviations", "read", "Read Product Abbreviations", null,
                moduleOverride: "product-abbreviation-register")
        };
        var (svc, roles, rolePerms) = BuildWith(catalog);
        var adminA = roles.IdOf(TenantA, "Admin");
        var adminB = roles.IdOf(TenantB, "Admin");

        foreach (var permission in catalog.Where(p => p.Module == "product-item-sku-master"))
        {
            rolePerms.Seed(RolePermission.ModuleGrant(
                adminA, permission.Id, TenantA, Actor, "product-item-sku-master"));
            rolePerms.Seed(RolePermission.ModuleGrant(
                adminB, permission.Id, TenantB, Actor, "product-item-sku-master"));
        }

        var globalProductReadId = catalog[0].Id;
        rolePerms.Seed(RolePermission.ModuleGrant(adminA, globalProductReadId, TenantA, Actor, "another-module"));
        rolePerms.Seed(RolePermission.SystemGrant(adminA, globalProductReadId, TenantA, "system"));
        rolePerms.Seed(RolePermission.ManualGrant(adminA, globalProductReadId, TenantA, "operator"));
        var abbreviationReadId = catalog.Single(p => p.Key == "mdm.product-abbreviations.read").Id;
        rolePerms.Seed(RolePermission.ModuleGrant(
            adminA, abbreviationReadId, TenantA, Actor, "product-abbreviation-register"));

        await svc.RevokeModuleAsync(TenantA, "product-item-sku-master", Actor, CancellationToken.None);

        Assert.DoesNotContain(rolePerms.Rows, rp => rp.TenantId == TenantA
            && rp.GrantSource == GrantSource.Module
            && rp.SourceModuleCode == "product-item-sku-master");
        Assert.Contains(rolePerms.Rows, rp => rp.TenantId == TenantA && rp.SourceModuleCode == "another-module");
        Assert.Contains(rolePerms.Rows, rp => rp.TenantId == TenantA && rp.GrantSource == GrantSource.System);
        Assert.Contains(rolePerms.Rows, rp => rp.TenantId == TenantA && rp.GrantSource == GrantSource.Manual);
        Assert.Contains(rolePerms.Rows, rp => rp.TenantId == TenantB
            && rp.SourceModuleCode == "product-item-sku-master");
        Assert.Contains(rolePerms.Rows, rp => rp.TenantId == TenantA
            && rp.PermissionId == abbreviationReadId
            && rp.SourceModuleCode == "product-abbreviation-register");
    }

    [Fact]
    public async Task Product_Item_Sku_Master_sync_does_not_invent_catalog_missing_permissions()
    {
        var catalog = new List<Permission>
        {
            new("mdm", "global-products", "read", "Read Global Products", null,
                moduleOverride: "product-item-sku-master"),
            new("mdm", "finished-goods", "read", "Read Finished Goods", null,
                moduleOverride: "product-item-sku-master")
        };
        var (svc, roles, rolePerms) = BuildWith(catalog);
        var module = new EntitledModulePermissionKeys("product-item-sku-master",
            [
                "mdm.global-products.read",
                "mdm.global-products.create",
                "mdm.finished-goods.read",
                "mdm.finished-goods.create",
                "mdm.gskus.read",
                "mdm.gskus.create",
                "mdm.lskus.read",
                "mdm.lskus.create"
            ]);

        await svc.SyncTenantModulesWithKeysAsync(TenantA, [module], Actor, CancellationToken.None);

        Assert.Equal(
            ["mdm.finished-goods.read", "mdm.global-products.read"],
            rolePerms.KeysFor(roles.IdOf(TenantA, "Admin"), catalog).OrderBy(k => k).ToArray());
        Assert.Equal(
            ["mdm.finished-goods.read", "mdm.global-products.read"],
            rolePerms.KeysFor(roles.IdOf(TenantA, "Viewer"), catalog).OrderBy(k => k).ToArray());
        Assert.DoesNotContain(rolePerms.Rows, rp => catalog.All(permission => permission.Id != rp.PermissionId));
    }

    [Fact]
    public async Task Product_abbreviation_profile_reconciles_exact_six_role_matrix_idempotently()
    {
        var catalog = ProductItemSkuMasterCatalog();
        var (svc, roles, rolePerms) = BuildWith(catalog);
        var module = new EntitledModulePermissionKeys(
            ProductAbbreviationEntitlementGrantProfile.ModuleCode,
            catalog.Select(permission => permission.Key).ToArray());

        await svc.GrantModuleWithKeysAsync(TenantA, module.ModuleCode, module.PermissionKeys, Actor);
        var firstCount = rolePerms.Rows.Count;
        await svc.GrantModuleWithKeysAsync(TenantA, module.ModuleCode, module.PermissionKeys, Actor);

        Assert.Equal(firstCount, rolePerms.Rows.Count);
        Assert.Equal(
            ["mdm.global-products.create", "mdm.global-products.read", ProductAbbreviationEntitlementGrantProfile.Read],
            rolePerms.KeysFor(roles.IdOf(TenantA, "Admin"), catalog).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(
            ["mdm.global-products.read", ProductAbbreviationEntitlementGrantProfile.Read],
            rolePerms.KeysFor(roles.IdOf(TenantA, "Viewer"), catalog).OrderBy(key => key, StringComparer.Ordinal));

        foreach (var template in ProductAbbreviationEntitlementGrantProfile.DedicatedRoles)
        {
            Assert.Equal(
                template.PermissionKeys.OrderBy(key => key, StringComparer.Ordinal),
                rolePerms.KeysFor(roles.IdOf(TenantA, template.RoleName), catalog)
                    .OrderBy(key => key, StringComparer.Ordinal));
        }

        Assert.DoesNotContain(rolePerms.Rows, grant => grant.TenantId == TenantB);
        Assert.All(rolePerms.Rows, grant => Assert.Equal("product-item-sku-master", grant.SourceModuleCode));
    }

    [Fact]
    public async Task Product_abbreviation_profile_removes_only_stale_module_sourced_subset_grants()
    {
        var catalog = ProductItemSkuMasterCatalog();
        var (svc, roles, rolePerms) = BuildWith(catalog);
        var adminId = roles.IdOf(TenantA, "Admin");
        var approve = catalog.Single(permission => permission.Key == ProductAbbreviationEntitlementGrantProfile.Approve);
        rolePerms.Seed(RolePermission.ModuleGrant(adminId, approve.Id, TenantA, Actor, "product-item-sku-master"));
        rolePerms.Seed(RolePermission.SystemGrant(adminId, approve.Id, TenantA, "system"));
        rolePerms.Seed(RolePermission.ManualGrant(adminId, approve.Id, TenantA, "operator"));
        rolePerms.Seed(RolePermission.ModuleGrant(adminId, approve.Id, TenantA, Actor, "another-module"));

        await svc.GrantModuleWithKeysAsync(
            TenantA,
            ProductAbbreviationEntitlementGrantProfile.ModuleCode,
            catalog.Select(permission => permission.Key).ToArray(),
            Actor);

        Assert.DoesNotContain(rolePerms.Rows, grant => grant.RoleId == adminId
            && grant.PermissionId == approve.Id
            && grant.GrantSource == GrantSource.Module
            && grant.SourceModuleCode == "product-item-sku-master");
        Assert.Contains(rolePerms.Rows, grant => grant.RoleId == adminId && grant.PermissionId == approve.Id && grant.GrantSource == GrantSource.System);
        Assert.Contains(rolePerms.Rows, grant => grant.RoleId == adminId && grant.PermissionId == approve.Id && grant.GrantSource == GrantSource.Manual);
        Assert.Contains(rolePerms.Rows, grant => grant.RoleId == adminId && grant.PermissionId == approve.Id && grant.SourceModuleCode == "another-module");
    }

    [Fact]
    public async Task Product_abbreviation_non_system_role_name_collision_fails_before_grant_or_role_creation()
    {
        var catalog = ProductItemSkuMasterCatalog();
        var (svc, roles, rolePerms) = BuildWith(catalog);
        roles.SeedNonSystem(TenantA, ProductAbbreviationEntitlementGrantProfile.ApproverRole);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GrantModuleWithKeysAsync(
            TenantA,
            ProductAbbreviationEntitlementGrantProfile.ModuleCode,
            catalog.Select(permission => permission.Key).ToArray(),
            Actor));

        Assert.Empty(rolePerms.Rows);
        Assert.False(roles.Exists(TenantA, ProductAbbreviationEntitlementGrantProfile.RequesterRole));
        Assert.False(roles.Exists(TenantA, ProductAbbreviationEntitlementGrantProfile.StewardRole));
        Assert.False(roles.Exists(TenantA, ProductAbbreviationEntitlementGrantProfile.AuditorRole));
    }

    [Fact]
    public async Task Product_abbreviation_partial_manifest_fails_before_mutation()
    {
        var catalog = ProductItemSkuMasterCatalog();
        var (svc, roles, rolePerms) = BuildWith(catalog);
        var partial = catalog.Select(permission => permission.Key)
            .Where(key => key != ProductAbbreviationEntitlementGrantProfile.Audit)
            .ToArray();

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GrantModuleWithKeysAsync(
            TenantA,
            ProductAbbreviationEntitlementGrantProfile.ModuleCode,
            partial,
            Actor));

        Assert.Empty(rolePerms.Rows);
        Assert.False(roles.Exists(TenantA, ProductAbbreviationEntitlementGrantProfile.RequesterRole));
    }

    [Fact]
    public async Task Product_abbreviation_cancellation_propagates_before_mutation()
    {
        var catalog = ProductItemSkuMasterCatalog();
        var (svc, roles, rolePerms) = BuildWith(catalog);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => svc.GrantModuleWithKeysAsync(
            TenantA,
            ProductAbbreviationEntitlementGrantProfile.ModuleCode,
            catalog.Select(permission => permission.Key).ToArray(),
            Actor,
            cancellation.Token));

        Assert.Empty(rolePerms.Rows);
        Assert.False(roles.Exists(TenantA, ProductAbbreviationEntitlementGrantProfile.RequesterRole));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("disabled")]
    [InlineData("expired")]
    public async Task Product_abbreviation_non_active_entitlement_leaves_no_module_sourced_grant(string state)
    {
        Assert.Contains(state, new[] { "missing", "disabled", "expired" });
        var catalog = ProductItemSkuMasterCatalog();
        var (svc, roles, rolePerms) = BuildWith(catalog);
        var keys = catalog.Select(permission => permission.Key).ToArray();
        await svc.GrantModuleWithKeysAsync(
            TenantA,
            ProductAbbreviationEntitlementGrantProfile.ModuleCode,
            keys,
            Actor);

        await svc.SyncTenantModulesWithKeysAsync(
            TenantA,
            Array.Empty<EntitledModulePermissionKeys>(),
            Actor);

        Assert.DoesNotContain(rolePerms.Rows, grant => grant.TenantId == TenantA
            && grant.GrantSource == GrantSource.Module
            && grant.SourceModuleCode == ProductAbbreviationEntitlementGrantProfile.ModuleCode);
        Assert.All(
            ProductAbbreviationEntitlementGrantProfile.DedicatedRoles,
            template => Assert.True(roles.Exists(TenantA, template.RoleName)));
    }

    [Fact]
    public async Task Product_abbreviation_sync_cancellation_is_not_swallowed_by_best_effort_loop()
    {
        var catalog = ProductItemSkuMasterCatalog();
        var (svc, roles, rolePerms) = BuildWith(catalog);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => svc.SyncTenantModulesWithKeysAsync(
            TenantA,
            [new EntitledModulePermissionKeys(
                ProductAbbreviationEntitlementGrantProfile.ModuleCode,
                catalog.Select(permission => permission.Key).ToArray())],
            Actor,
            cancellation.Token));

        Assert.Empty(rolePerms.Rows);
        Assert.False(roles.Exists(TenantA, ProductAbbreviationEntitlementGrantProfile.RequesterRole));
    }

    [Fact]
    public async Task Product_legal_entity_scope_composite_profile_reconciles_exact_nine_role_matrix_idempotently()
    {
        var catalog = ProductItemSkuMasterCompositeCatalog();
        var (svc, roles, rolePerms) = BuildWith(catalog);
        var keys = catalog.Select(permission => permission.Key).ToArray();

        await svc.GrantModuleWithKeysAsync(TenantA, ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode, keys, Actor);
        var firstCount = rolePerms.Rows.Count;
        await svc.GrantModuleWithKeysAsync(TenantA, ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode, keys, Actor);

        Assert.Equal(firstCount, rolePerms.Rows.Count);
        Assert.Equal(
            [
                "mdm.global-products.create",
                "mdm.global-products.read",
                ProductAbbreviationEntitlementGrantProfile.Read,
                ProductLegalEntityScopeEntitlementGrantProfile.Read
            ],
            rolePerms.KeysFor(roles.IdOf(TenantA, "Admin"), catalog).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(
            ["mdm.global-products.read", ProductAbbreviationEntitlementGrantProfile.Read],
            rolePerms.KeysFor(roles.IdOf(TenantA, "Viewer"), catalog).OrderBy(key => key, StringComparer.Ordinal));

        foreach (var template in ProductAbbreviationEntitlementGrantProfile.DedicatedRoles)
        {
            Assert.Equal(
                template.PermissionKeys.OrderBy(key => key, StringComparer.Ordinal),
                rolePerms.KeysFor(roles.IdOf(TenantA, template.RoleName), catalog)
                    .OrderBy(key => key, StringComparer.Ordinal));
        }
        foreach (var template in ProductLegalEntityScopeEntitlementGrantProfile.DedicatedRoles)
        {
            Assert.Equal(
                template.PermissionKeys.OrderBy(key => key, StringComparer.Ordinal),
                rolePerms.KeysFor(roles.IdOf(TenantA, template.RoleName), catalog)
                    .OrderBy(key => key, StringComparer.Ordinal));
        }

        Assert.DoesNotContain(rolePerms.Rows, grant => grant.TenantId == TenantB);
        Assert.All(rolePerms.Rows, grant => Assert.Equal("product-item-sku-master", grant.SourceModuleCode));
    }

    [Theory]
    [MemberData(nameof(AllCompositeDedicatedRoleNames))]
    public async Task Product_legal_entity_scope_collision_preflights_all_ABB_and_scope_roles_before_mutation(
        string collisionRoleName)
    {
        var catalog = ProductItemSkuMasterCompositeCatalog();
        var (svc, roles, rolePerms) = BuildWith(catalog);
        roles.SeedNonSystem(TenantA, collisionRoleName);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GrantModuleWithKeysAsync(
            TenantA,
            ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode,
            catalog.Select(permission => permission.Key).ToArray(),
            Actor));

        Assert.Empty(rolePerms.Rows);
        Assert.True(roles.Exists(TenantA, collisionRoleName));
        foreach (var roleName in AllCompositeDedicatedRoleNames().Select(row => (string)row[0]))
        {
            if (!string.Equals(roleName, collisionRoleName, StringComparison.Ordinal))
            {
                Assert.False(roles.Exists(TenantA, roleName));
            }
        }
    }

    public static IEnumerable<object[]> AllCompositeDedicatedRoleNames() =>
        ProductAbbreviationEntitlementGrantProfile.DedicatedRoles
            .Select(template => new object[] { template.RoleName })
            .Concat(ProductLegalEntityScopeEntitlementGrantProfile.DedicatedRoles
                .Select(template => new object[] { template.RoleName }));

    [Fact]
    public async Task Product_legal_entity_scope_catalog_missing_and_partial_descriptor_fail_before_mutation()
    {
        var complete = ProductItemSkuMasterCompositeCatalog();
        var missingCatalog = complete
            .Where(permission => permission.Key != ProductLegalEntityScopeEntitlementGrantProfile.Rollback)
            .ToList();
        var (missingService, missingRoles, missingGrants) = BuildWith(missingCatalog);

        await Assert.ThrowsAsync<InvalidOperationException>(() => missingService.GrantModuleWithKeysAsync(
            TenantA,
            ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode,
            complete.Select(permission => permission.Key).ToArray(),
            Actor));
        Assert.Empty(missingGrants.Rows);
        Assert.False(missingRoles.Exists(TenantA, ProductLegalEntityScopeEntitlementGrantProfile.StewardRole));

        var (partialService, partialRoles, partialGrants) = BuildWith(complete);
        await Assert.ThrowsAsync<InvalidOperationException>(() => partialService.GrantModuleWithKeysAsync(
            TenantA,
            ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode,
            complete.Select(permission => permission.Key)
                .Where(key => key != ProductLegalEntityScopeEntitlementGrantProfile.Rollback)
                .ToArray(),
            Actor));
        Assert.Empty(partialGrants.Rows);
        Assert.False(partialRoles.Exists(TenantA, ProductLegalEntityScopeEntitlementGrantProfile.StewardRole));
    }

    [Theory]
    [InlineData("scope-only")]
    [InlineData("generic-only")]
    [InlineData("empty")]
    public async Task Product_legal_entity_scope_rejects_non_authoritative_module_subsets_before_mutation(string variant)
    {
        var catalog = ProductItemSkuMasterCompositeCatalog();
        var (svc, roles, rolePerms) = BuildWith(catalog);
        var keys = variant switch
        {
            "scope-only" => ProductLegalEntityScopeEntitlementGrantProfile.PermissionKeys.ToArray(),
            "generic-only" => catalog
                .Where(permission => !ProductAbbreviationEntitlementGrantProfile.IsProductAbbreviationKey(permission.Key)
                                     && !ProductLegalEntityScopeEntitlementGrantProfile.IsProductLegalEntityScopeKey(permission.Key))
                .Select(permission => permission.Key)
                .ToArray(),
            "empty" => [],
            _ => throw new InvalidOperationException("Unknown test variant.")
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GrantModuleWithKeysAsync(
            TenantA,
            ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode,
            keys,
            Actor));

        Assert.Empty(rolePerms.Rows);
        Assert.False(roles.Exists(TenantA, ProductLegalEntityScopeEntitlementGrantProfile.StewardRole));
        Assert.False(roles.Exists(TenantA, ProductAbbreviationEntitlementGrantProfile.RequesterRole));
    }

    [Fact]
    public async Task Product_legal_entity_scope_authoritative_sync_does_not_swallow_incomplete_snapshot()
    {
        var catalog = ProductItemSkuMasterCompositeCatalog();
        var (svc, roles, rolePerms) = BuildWith(catalog);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.SyncTenantModulesWithKeysAsync(
            TenantA,
            [new EntitledModulePermissionKeys(
                ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode,
                ProductLegalEntityScopeEntitlementGrantProfile.PermissionKeys.ToArray())],
            Actor));

        Assert.Empty(rolePerms.Rows);
        Assert.False(roles.Exists(TenantA, ProductLegalEntityScopeEntitlementGrantProfile.StewardRole));
    }

    [Fact]
    public async Task Product_legal_entity_scope_reconcile_removes_only_stale_matching_module_source_grants()
    {
        var catalog = ProductItemSkuMasterCompositeCatalog();
        var orphan = PermissionFor("mdm.product-legal-entity-scopes.orphaned");
        var (svc, roles, rolePerms) = BuildWith(catalog);
        var adminId = roles.IdOf(TenantA, "Admin");
        var configure = catalog.Single(permission => permission.Key == ProductLegalEntityScopeEntitlementGrantProfile.Configure);
        rolePerms.Seed(RolePermission.ModuleGrant(adminId, configure.Id, TenantA, Actor, "product-item-sku-master"));
        rolePerms.Seed(RolePermission.SystemGrant(adminId, configure.Id, TenantA, "system"));
        rolePerms.Seed(RolePermission.ManualGrant(adminId, configure.Id, TenantA, "operator"));
        rolePerms.Seed(RolePermission.ModuleGrant(adminId, configure.Id, TenantA, Actor, "another-module"));
        rolePerms.Seed(RolePermission.ModuleGrant(adminId, orphan.Id, TenantA, Actor, "product-item-sku-master"));

        await svc.GrantModuleWithKeysAsync(
            TenantA,
            ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode,
            catalog.Where(permission => permission.Id != orphan.Id).Select(permission => permission.Key).ToArray(),
            Actor);

        Assert.DoesNotContain(rolePerms.Rows, grant => grant.RoleId == adminId
            && grant.PermissionId == configure.Id
            && grant.GrantSource == GrantSource.Module
            && grant.SourceModuleCode == ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode);
        Assert.Contains(rolePerms.Rows, grant => grant.RoleId == adminId && grant.PermissionId == configure.Id && grant.GrantSource == GrantSource.System);
        Assert.Contains(rolePerms.Rows, grant => grant.RoleId == adminId && grant.PermissionId == configure.Id && grant.GrantSource == GrantSource.Manual);
        Assert.Contains(rolePerms.Rows, grant => grant.RoleId == adminId && grant.PermissionId == configure.Id && grant.SourceModuleCode == "another-module");
        Assert.DoesNotContain(rolePerms.Rows, grant => grant.PermissionId == orphan.Id);
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("blank")]
    [InlineData("case-alias")]
    public async Task Product_legal_entity_scope_rejects_non_canonical_descriptor_sets_before_mutation(string variant)
    {
        var catalog = ProductItemSkuMasterCompositeCatalog();
        var (svc, roles, rolePerms) = BuildWith(catalog);
        var keys = catalog.Select(permission => permission.Key).ToList();
        switch (variant)
        {
            case "duplicate": keys.Add(ProductLegalEntityScopeEntitlementGrantProfile.Read); break;
            case "blank": keys.Add(" "); break;
            case "case-alias":
                keys[keys.IndexOf(ProductLegalEntityScopeEntitlementGrantProfile.Read)] =
                    ProductLegalEntityScopeEntitlementGrantProfile.Read.ToUpperInvariant();
                break;
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GrantModuleWithKeysAsync(
            TenantA,
            ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode,
            keys,
            Actor));

        Assert.Empty(rolePerms.Rows);
        Assert.False(roles.Exists(TenantA, ProductLegalEntityScopeEntitlementGrantProfile.StewardRole));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("disabled")]
    [InlineData("expired")]
    public async Task Product_legal_entity_scope_non_active_entitlement_revokes_all_matching_profile_grants(string state)
    {
        Assert.Contains(state, new[] { "missing", "disabled", "expired" });
        var catalog = ProductItemSkuMasterCompositeCatalog();
        var (svc, roles, rolePerms) = BuildWith(catalog);
        await svc.GrantModuleWithKeysAsync(
            TenantA,
            ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode,
            catalog.Select(permission => permission.Key).ToArray(),
            Actor);

        await svc.SyncTenantModulesWithKeysAsync(TenantA, [], Actor);

        Assert.DoesNotContain(rolePerms.Rows, grant => grant.TenantId == TenantA
            && grant.GrantSource == GrantSource.Module
            && grant.SourceModuleCode == ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode);
        Assert.All(ProductLegalEntityScopeEntitlementGrantProfile.DedicatedRoles,
            template => Assert.True(roles.Exists(TenantA, template.RoleName)));
    }

    [Fact]
    public async Task Product_legal_entity_scope_cancellation_propagates_before_mutation()
    {
        var catalog = ProductItemSkuMasterCompositeCatalog();
        var (svc, roles, rolePerms) = BuildWith(catalog);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => svc.GrantModuleWithKeysAsync(
            TenantA,
            ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode,
            catalog.Select(permission => permission.Key).ToArray(),
            Actor,
            cancellation.Token));

        Assert.Empty(rolePerms.Rows);
        Assert.False(roles.Exists(TenantA, ProductLegalEntityScopeEntitlementGrantProfile.StewardRole));
    }

    [Fact]
    public async Task Repository_cancellation_propagates_even_when_caller_token_is_not_marked_cancelled()
    {
        var catalog = ProductItemSkuMasterCompositeCatalog();
        var (svc, roles, rolePerms) = BuildWith(catalog, throwRepositoryCancellation: true);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => svc.SyncTenantModulesWithKeysAsync(
            TenantA,
            [new EntitledModulePermissionKeys(
                ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode,
                catalog.Select(permission => permission.Key).ToArray())],
            Actor,
            CancellationToken.None));

        Assert.Empty(rolePerms.Rows);
        Assert.False(roles.Exists(TenantA, ProductLegalEntityScopeEntitlementGrantProfile.StewardRole));
    }

    [Fact]
    public async Task Product_identity_lifecycle_profile_composes_exact_thirteen_seven_eight_roles_with_ABB_and_scope()
    {
        var catalog = ProductIdentityLifecycleCompositeCatalog();
        var (svc, roles, rolePerms) = BuildWith(catalog);
        var declaredKeys = ProductIdentityDeclaredKeys(catalog);
        Assert.Equal(30, declaredKeys.Length);

        await svc.GrantModuleWithKeysAsync(
            TenantA,
            ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode,
            declaredKeys,
            Actor);

        Assert.Equal(13, ProductRoleKeys(roles, rolePerms, catalog, ProductIdentityLifecycleEntitlementGrantProfile.StewardRole).Count);
        Assert.Equal(7, ProductRoleKeys(roles, rolePerms, catalog, ProductIdentityLifecycleEntitlementGrantProfile.ApproverRole).Count);
        Assert.Equal(8, ProductRoleKeys(roles, rolePerms, catalog, ProductIdentityLifecycleEntitlementGrantProfile.RetirementStewardRole).Count);

        var admin = ProductRoleKeys(roles, rolePerms, catalog, DefaultRolePermissionTemplate.AdminRole);
        var viewer = ProductRoleKeys(roles, rolePerms, catalog, DefaultRolePermissionTemplate.ViewerRole);
        Assert.DoesNotContain(admin, ProductIdentityLifecycleEntitlementGrantProfile.PermissionKeys.Contains);
        Assert.DoesNotContain(viewer, ProductIdentityLifecycleEntitlementGrantProfile.PermissionKeys.Contains);
        Assert.DoesNotContain(admin, ProductIdentityLifecycleEntitlementGrantProfile.SharedDependencyKeys.Contains);
        Assert.DoesNotContain(viewer, ProductIdentityLifecycleEntitlementGrantProfile.SharedDependencyKeys.Contains);
        Assert.DoesNotContain("mdm.brands.read", admin);
        Assert.DoesNotContain("mdm.brands.read", viewer);
        Assert.Equal(8 + 1 + 1, admin.Count);
        Assert.Equal(4 + 1, viewer.Count);

        Assert.All(ProductAbbreviationEntitlementGrantProfile.DedicatedRoles,
            template => Assert.Equal(
                template.PermissionKeys.OrderBy(key => key, StringComparer.Ordinal),
                ProductRoleKeys(roles, rolePerms, catalog, template.RoleName).OrderBy(key => key, StringComparer.Ordinal)));
        Assert.All(ProductLegalEntityScopeEntitlementGrantProfile.DedicatedRoles,
            template => Assert.Equal(
                template.PermissionKeys.OrderBy(key => key, StringComparer.Ordinal),
                ProductRoleKeys(roles, rolePerms, catalog, template.RoleName).OrderBy(key => key, StringComparer.Ordinal)));
    }

    [Fact]
    public async Task Product_identity_lifecycle_reconcile_fails_when_repository_does_not_converge()
    {
        var catalog = ProductIdentityLifecycleCompositeCatalog();
        var (service, _, grants) = BuildWith(catalog);
        grants.DropAssignments = true;

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GrantModuleWithKeysAsync(
            TenantA,
            ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode,
            ProductIdentityDeclaredKeys(catalog),
            Actor));
    }

    [Fact]
    public async Task Product_identity_lifecycle_reconcile_accepts_existing_manual_dependency_without_duplicate_module_grant()
    {
        var catalog = ProductIdentityLifecycleCompositeCatalog();
        var (service, roles, grants) = BuildWith(catalog);
        var declaredKeys = ProductIdentityDeclaredKeys(catalog);
        await service.GrantModuleWithKeysAsync(
            TenantA,
            ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode,
            declaredKeys,
            Actor);

        var stewardId = roles.IdOf(TenantA, ProductIdentityLifecycleEntitlementGrantProfile.StewardRole);
        var start = catalog.Single(permission =>
            permission.Key == ProductIdentityLifecycleEntitlementGrantProfile.WorkflowInstancesStart);
        grants.Rows.RemoveAll(grant => grant.RoleId == stewardId && grant.PermissionId == start.Id);
        grants.Seed(RolePermission.ManualGrant(stewardId, start.Id, TenantA, "operator"));

        await service.GrantModuleWithKeysAsync(
            TenantA,
            ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode,
            declaredKeys,
            Actor);

        Assert.Single(grants.Rows, grant =>
            grant.RoleId == stewardId
            && grant.PermissionId == start.Id
            && grant.GrantSource == GrantSource.Manual);
        Assert.DoesNotContain(grants.Rows, grant =>
            grant.RoleId == stewardId
            && grant.PermissionId == start.Id
            && grant.GrantSource == GrantSource.Module);
    }

    [Fact]
    public async Task Product_identity_profile_dependency_drift_and_collision_fail_before_mutation()
    {
        var missingDependencyCatalog = ProductIdentityLifecycleCompositeCatalog()
            .Where(permission => permission.Key != ProductIdentityLifecycleEntitlementGrantProfile.WorkflowTasksReject)
            .ToList();
        var declaredKeys = ProductIdentityDeclaredKeys(missingDependencyCatalog);
        var (missingService, missingRoles, missingGrants) = BuildWith(missingDependencyCatalog);

        await Assert.ThrowsAsync<InvalidOperationException>(() => missingService.GrantModuleWithKeysAsync(
            TenantA,
            ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode,
            declaredKeys,
            Actor));
        Assert.Empty(missingGrants.Rows);
        Assert.False(missingRoles.Exists(TenantA, ProductIdentityLifecycleEntitlementGrantProfile.StewardRole));

        var collisionCatalog = ProductIdentityLifecycleCompositeCatalog();
        var (collisionService, collisionRoles, collisionGrants) = BuildWith(collisionCatalog);
        collisionRoles.SeedNonSystem(TenantA, ProductIdentityLifecycleEntitlementGrantProfile.ApproverRole);
        await Assert.ThrowsAsync<InvalidOperationException>(() => collisionService.GrantModuleWithKeysAsync(
            TenantA,
            ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode,
            ProductIdentityDeclaredKeys(collisionCatalog),
            Actor));
        Assert.Empty(collisionGrants.Rows);
        Assert.False(collisionRoles.Exists(TenantA, ProductIdentityLifecycleEntitlementGrantProfile.StewardRole));
    }

    [Fact]
    public async Task Product_identity_profile_rejects_arbitrary_cross_module_read_before_mutation()
    {
        var catalog = ProductIdentityLifecycleCompositeCatalog();
        catalog.Add(new Permission(
            "mdm",
            "products",
            "read",
            "Read Products",
            null,
            moduleOverride: "brand-product-master",
            scope: PermissionScope.Tenant));
        var declaredKeys = ProductIdentityDeclaredKeys(catalog)
            .Append("mdm.products.read")
            .ToArray();
        var (service, roles, grants) = BuildWith(catalog);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GrantModuleWithKeysAsync(
            TenantA,
            ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode,
            declaredKeys,
            Actor));

        Assert.Empty(grants.Rows);
        Assert.False(roles.Exists(TenantA, ProductIdentityLifecycleEntitlementGrantProfile.StewardRole));
    }

    [Fact]
    public async Task Product_identity_reconcile_removes_only_stale_product_sourced_brand_grant()
    {
        var catalog = ProductIdentityLifecycleCompositeCatalog();
        var brandRead = new Permission(
            "mdm",
            "brands",
            "read",
            "Read Brands",
            null,
            moduleOverride: "brand-product-master",
            scope: PermissionScope.Tenant);
        catalog.Add(brandRead);
        var (service, roles, grants) = BuildWith(catalog);
        var adminId = roles.IdOf(TenantA, DefaultRolePermissionTemplate.AdminRole);
        grants.Seed(RolePermission.ModuleGrant(
            adminId,
            brandRead.Id,
            TenantA,
            Actor,
            ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode));
        grants.Seed(RolePermission.ManualGrant(adminId, brandRead.Id, TenantA, "operator"));
        grants.Seed(RolePermission.SystemGrant(adminId, brandRead.Id, TenantA, "system"));
        grants.Seed(RolePermission.ModuleGrant(
            adminId,
            brandRead.Id,
            TenantA,
            Actor,
            "brand-product-master"));

        await service.GrantModuleWithKeysAsync(
            TenantA,
            ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode,
            ProductIdentityDeclaredKeys(catalog),
            Actor);

        Assert.DoesNotContain(grants.Rows, grant =>
            grant.PermissionId == brandRead.Id
            && grant.GrantSource == GrantSource.Module
            && grant.SourceModuleCode == ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode);
        Assert.Contains(grants.Rows, grant =>
            grant.PermissionId == brandRead.Id && grant.GrantSource == GrantSource.Manual);
        Assert.Contains(grants.Rows, grant =>
            grant.PermissionId == brandRead.Id && grant.GrantSource == GrantSource.System);
        Assert.Contains(grants.Rows, grant =>
            grant.PermissionId == brandRead.Id
            && grant.GrantSource == GrantSource.Module
            && grant.SourceModuleCode == "brand-product-master");
    }

    [Theory]
    [InlineData(ProductIdentityLifecycleEntitlementGrantProfile.StewardRole)]
    [InlineData(ProductIdentityLifecycleEntitlementGrantProfile.ApproverRole)]
    [InlineData(ProductIdentityLifecycleEntitlementGrantProfile.RetirementStewardRole)]
    public async Task Every_product_identity_role_name_collision_fails_before_any_profile_mutation(string roleName)
    {
        var catalog = ProductIdentityLifecycleCompositeCatalog();
        var (svc, roles, rolePerms) = BuildWith(catalog);
        roles.SeedNonSystem(TenantA, roleName);

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.GrantModuleWithKeysAsync(
            TenantA,
            ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode,
            ProductIdentityDeclaredKeys(catalog),
            Actor));

        Assert.Empty(rolePerms.Rows);
        Assert.DoesNotContain(
            ProductIdentityLifecycleEntitlementGrantProfile.DedicatedRoles,
            template => template.RoleName != roleName && roles.Exists(TenantA, template.RoleName));
    }

    [Fact]
    public async Task Inactive_lifecycle_profile_cleans_only_matching_module_grants_without_recreating_roles()
    {
        var catalog = ProductIdentityLifecycleCompositeCatalog();
        var (svc, roles, rolePerms) = BuildWith(catalog);
        var allDeclared = ProductIdentityDeclaredKeys(catalog);
        await svc.GrantModuleWithKeysAsync(
            TenantA,
            ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode,
            allDeclared,
            Actor);

        var approverId = roles.IdOf(TenantA, ProductIdentityLifecycleEntitlementGrantProfile.ApproverRole);
        var manualPermission = catalog.Single(permission =>
            permission.Key == ProductIdentityLifecycleEntitlementGrantProfile.GlobalProductsRead);
        rolePerms.Seed(RolePermission.ManualGrant(approverId, manualPermission.Id, TenantA, "operator"));

        var withoutLifecycle = allDeclared
            .Where(key => !ProductIdentityLifecycleEntitlementGrantProfile.PermissionKeys.Contains(key))
            .ToArray();
        await svc.GrantModuleWithKeysAsync(
            TenantA,
            ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode,
            withoutLifecycle,
            Actor);

        Assert.DoesNotContain(rolePerms.Rows, grant => grant.RoleId == approverId
                                                       && grant.GrantSource == GrantSource.Module
                                                       && grant.SourceModuleCode == ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode);
        Assert.Contains(rolePerms.Rows, grant => grant.RoleId == approverId && grant.GrantSource == GrantSource.Manual);
    }

    [Fact]
    public async Task Sync_with_keys_continues_independent_modules_but_throws_accumulated_failure()
    {
        var catalog = ProductIdentityLifecycleCompositeCatalog();
        catalog.Add(new Permission("goldenslim", "records", "read", "Read", null));
        var (svc, roles, rolePerms) = BuildWith(catalog);
        var invalidProductKeys = ProductIdentityDeclaredKeys(catalog)
            .Where(key => key != ProductIdentityLifecycleEntitlementGrantProfile.GskusSubmit)
            .ToArray();

        var failure = await Assert.ThrowsAsync<AggregateException>(() => svc.SyncTenantModulesWithKeysAsync(
            TenantA,
            [
                new EntitledModulePermissionKeys(ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode, invalidProductKeys),
                new EntitledModulePermissionKeys("goldenslim", ["goldenslim.records.read"])
            ],
            Actor));

        Assert.Single(failure.InnerExceptions);
        var adminId = roles.IdOf(TenantA, DefaultRolePermissionTemplate.AdminRole);
        Assert.Contains(rolePerms.KeysFor(adminId, catalog), key => key == "goldenslim.records.read");
        Assert.False(roles.Exists(TenantA, ProductIdentityLifecycleEntitlementGrantProfile.StewardRole));
    }

    private static IReadOnlyList<string> ProductRoleKeys(
        FakeRoleRepository roles,
        FakeRolePermissionRepository rolePerms,
        List<Permission> catalog,
        string roleName)
    {
        var roleId = roles.IdOf(TenantA, roleName);
        return rolePerms.Rows
            .Where(grant => grant.RoleId == roleId
                            && grant.GrantSource == GrantSource.Module
                            && grant.SourceModuleCode == ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode)
            .Select(grant => catalog.Single(permission => permission.Id == grant.PermissionId).Key)
            .ToList();
    }

    private static List<Permission> ProductIdentityLifecycleCompositeCatalog() =>
    [
        .. ProductIdentityLifecycleEntitlementGrantProfile.BasePermissionKeys.Select(PermissionFor),
        .. ProductIdentityLifecycleEntitlementGrantProfile.PermissionKeys.Select(PermissionFor),
        .. ProductAbbreviationEntitlementGrantProfile.PermissionKeys.Select(PermissionFor),
        .. ProductLegalEntityScopeEntitlementGrantProfile.PermissionKeys.Select(PermissionFor),
        new("platform", "work-aggregation.inbox", "view", "Inbox", null,
            moduleOverride: "work-aggregation", scope: PermissionScope.Tenant),
        new("platform", "workflow.instances", "start", "Start", null,
            moduleOverride: "workflow", scope: PermissionScope.Tenant),
        new("platform", "workflow.tasks", "approve", "Approve", null,
            moduleOverride: "workflow", scope: PermissionScope.Tenant),
        new("platform", "workflow.tasks", "reject", "Reject", null,
            moduleOverride: "workflow", scope: PermissionScope.Tenant)
    ];

    private static string[] ProductIdentityDeclaredKeys(IEnumerable<Permission> catalog)
        => catalog
            .Where(permission => permission.Module == ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode)
            .Select(permission => permission.Key)
            .ToArray();

    private static List<Permission> ProductItemSkuMasterCompositeCatalog() =>
    [
        new("mdm", "global-products", "read", "Read Global Products", null, moduleOverride: "product-item-sku-master"),
        new("mdm", "global-products", "create", "Create Global Products", null, moduleOverride: "product-item-sku-master"),
        .. ProductAbbreviationEntitlementGrantProfile.PermissionKeys.Select(key => PermissionFor(key)),
        .. ProductLegalEntityScopeEntitlementGrantProfile.PermissionKeys.Select(key => PermissionFor(key))
    ];

    private static Permission PermissionFor(string key)
    {
        var separator = key.LastIndexOf('.');
        return new Permission(
            "mdm",
            key["mdm.".Length..separator],
            key[(separator + 1)..],
            key,
            null,
            moduleOverride: ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode);
    }

    private static (EntitlementPermissionSyncService svc, FakeRoleRepository roles, FakeRolePermissionRepository rolePerms) BuildWith(
        List<Permission> catalog,
        bool throwRepositoryCancellation = false)
    {
        var roles = new FakeRoleRepository(TenantA, TenantB);
        var rolePerms = new FakeRolePermissionRepository();
        var svc = new EntitlementPermissionSyncService(new FakePermissionRepository(catalog, throwRepositoryCancellation), roles, rolePerms, NullLogger<EntitlementPermissionSyncService>.Instance);
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
                foreach (var name in new[] { "Admin", "Viewer", "Custom" })
                {
                    var role = new Role(name, name, null, t);
                    role.MarkAsSystem();
                    _roles[(t, name)] = role;
                }
            }
        }

        public Guid IdOf(Guid tenantId, string name) => _roles[(tenantId, name)].Id;

        public bool Exists(Guid tenantId, string name) => _roles.ContainsKey((tenantId, name));

        public void SeedNonSystem(Guid tenantId, string name)
            => _roles[(tenantId, name)] = new Role(name, name, null, tenantId);

        public Task<Role?> GetByNameAndTenantAsync(string name, Guid tenantId, CancellationToken ct)
            => Task.FromResult(_roles.TryGetValue((tenantId, name), out var r) ? r : null);

        public Task<Role?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<IEnumerable<Role>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> CreateAsync(Role role, CancellationToken ct) => throw new NotSupportedException();
        public Task<Role> UpsertSystemRoleAsync(string name, string displayName, string? description, Guid tenantId, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (_roles.TryGetValue((tenantId, name), out var existing))
            {
                return Task.FromResult(existing);
            }

            var role = new Role(name, displayName, description, tenantId);
            role.MarkAsSystem();
            _roles[(tenantId, name)] = role;
            return Task.FromResult(role);
        }
        public Task<Role> UpdateAsync(Role role, CancellationToken ct) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, Guid tenantId, CancellationToken ct) => throw new NotSupportedException();
    }

    private sealed class FakePermissionRepository(List<Permission> catalog, bool throwCancellation = false) : IPermissionRepository
    {
        public Task<IEnumerable<Permission>> GetAllAsync(CancellationToken ct)
        {
            if (throwCancellation)
            {
                throw new OperationCanceledException();
            }
            ct.ThrowIfCancellationRequested();
            return Task.FromResult<IEnumerable<Permission>>(catalog);
        }
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
        public bool DropAssignments { get; set; }

        public void Seed(RolePermission rp) => Rows.Add(rp);

        public IEnumerable<string> KeysFor(Guid roleId, List<Permission> catalog) =>
            Rows.Where(rp => rp.RoleId == roleId).Select(rp => catalog.Single(p => p.Id == rp.PermissionId).Key);

        public Task AssignAsync(RolePermission rolePermission, CancellationToken ct)
        {
            if (DropAssignments)
            {
                return Task.CompletedTask;
            }

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
