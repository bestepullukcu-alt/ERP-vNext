using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Tests.Roles;

// S1 / AG-INFRA-COMPLETION — the shared baseline template is the single source of truth for both
// the DataSeeder (default tenant) and RoleProvisioningService (every tenant). These tests pin the
// selection so the two cannot drift, and assert the platform-escalation boundary for tenant roles.
public sealed class DefaultRolePermissionTemplateTests
{
    private static List<Permission> Catalog() =>
    [
        new("auth", "users", "read", "Read User", null, moduleOverride: "access-governance"),
        new("auth", "users", "create", "Create User", null, moduleOverride: "access-governance"),
        new("mdm", "legal-entities", "read", "Read Legal Entity", null, moduleOverride: "legal-entity"),
        new("mdm", "legal-entities", "delete", "Delete Legal Entity", null, moduleOverride: "legal-entity"),
        new("platform", "tenants", "read", "Read Tenant", null)
    ];

    [Fact]
    public void SuperAdmin_gets_the_full_catalog()
    {
        var keys = DefaultRolePermissionTemplate.SelectFor("SuperAdmin", Catalog()).Select(p => p.Key).ToList();

        Assert.Equal(5, keys.Count);
        Assert.Contains("platform.tenants.read", keys);
    }

    [Fact]
    public void Admin_gets_auth_and_mdm_only_never_platform()
    {
        var keys = DefaultRolePermissionTemplate.SelectFor("Admin", Catalog()).Select(p => p.Key).ToList();

        Assert.Equal(
            new[] { "auth.users.read", "auth.users.create", "mdm.legal-entities.read", "mdm.legal-entities.delete" }.OrderBy(k => k),
            keys.OrderBy(k => k));
        Assert.DoesNotContain("platform.tenants.read", keys);
    }

    [Fact]
    public void Viewer_gets_read_actions_only_never_platform()
    {
        var keys = DefaultRolePermissionTemplate.SelectFor("Viewer", Catalog()).Select(p => p.Key).ToList();

        Assert.Equal(
            new[] { "auth.users.read", "mdm.legal-entities.read" }.OrderBy(k => k),
            keys.OrderBy(k => k));
        // platform.tenants.read is a read action but platform-scoped → still excluded for tenant roles.
        Assert.DoesNotContain("platform.tenants.read", keys);
    }

    [Fact]
    public void Viewer_baseline_excludes_exact_Product_Item_Sku_Master_entitlement_permissions()
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
                moduleOverride: "product-item-sku-master"),
            new("mdm", "product-abbreviations", "request", "Request Product Abbreviations", null,
                moduleOverride: "product-item-sku-master"),
            new("mdm", "product-abbreviations", "cancel", "Cancel Product Abbreviations", null,
                moduleOverride: "product-item-sku-master"),
            new("mdm", "product-abbreviations", "approve", "Approve Product Abbreviations", null,
                moduleOverride: "product-item-sku-master"),
            new("mdm", "product-abbreviations", "reject", "Reject Product Abbreviations", null,
                moduleOverride: "product-item-sku-master"),
            new("mdm", "product-abbreviations", "correct", "Correct Product Abbreviations", null,
                moduleOverride: "product-item-sku-master"),
            new("mdm", "product-abbreviations", "retire", "Retire Product Abbreviations", null,
                moduleOverride: "product-item-sku-master"),
            new("mdm", "product-abbreviations", "audit", "Audit Product Abbreviations", null,
                moduleOverride: "product-item-sku-master"),
            new("mdm", "legal-entities", "read", "Read Legal Entities", null,
                moduleOverride: "legal-entity")
        };

        var viewerKeys = DefaultRolePermissionTemplate.SelectFor("Viewer", catalog).Select(p => p.Key).ToList();
        var adminKeys = DefaultRolePermissionTemplate.SelectFor("Admin", catalog).Select(p => p.Key).ToList();

        Assert.Equal(["mdm.legal-entities.read"], viewerKeys);
        Assert.Equal(["mdm.legal-entities.read"], adminKeys);
        Assert.DoesNotContain("mdm.gskus.read", viewerKeys);
        Assert.DoesNotContain("mdm.gskus.create", viewerKeys);
        Assert.DoesNotContain("mdm.gskus.create", adminKeys);
        Assert.DoesNotContain("mdm.lskus.read", viewerKeys);
        Assert.DoesNotContain("mdm.lskus.create", viewerKeys);
        Assert.DoesNotContain("mdm.lskus.create", adminKeys);
        Assert.DoesNotContain("mdm.product-abbreviations.read", viewerKeys);
        Assert.DoesNotContain("mdm.product-abbreviations.read", adminKeys);
        Assert.Equal(
            [
                "mdm.finished-goods.create",
                "mdm.finished-goods.read",
                "mdm.global-products.create",
                "mdm.global-products.read",
                "mdm.gskus.create",
                "mdm.gskus.read",
                "mdm.lskus.create",
                "mdm.lskus.read",
                "mdm.product-abbreviations.approve",
                "mdm.product-abbreviations.audit",
                "mdm.product-abbreviations.cancel",
                "mdm.product-abbreviations.correct",
                "mdm.product-abbreviations.read",
                "mdm.product-abbreviations.reject",
                "mdm.product-abbreviations.request",
                "mdm.product-abbreviations.retire"
            ],
            DefaultRolePermissionTemplate.EntitlementOnlyViewerPermissions.OrderBy(k => k).ToArray());
    }

    // FIX-TENANT-SELFSERVICE-PERMS — the tenant Admin receives the curated tenant-scoped platform.* self-service
    // keys, but NO other platform.* permission (escalation boundary preserved).
    [Fact]
    public void Admin_gets_tenant_self_service_platform_keys_but_no_other_platform()
    {
        var catalog = new List<Permission>
        {
            new("auth", "users", "read", "Read User", null, moduleOverride: "access-governance"),
            new("platform", "tenant-security", "read", "Read Tenant Security", null),
            new("platform", "tenant-security", "manage", "Manage Tenant Security", null),
            new("platform", "tenants", "read", "Read Tenant", null),                 // other platform.* → excluded
            new("platform", "workflow.definitions", "view", "View Workflow", null)    // other platform.* → excluded
        };

        var keys = DefaultRolePermissionTemplate.SelectFor("Admin", catalog).Select(p => p.Key).ToList();

        Assert.Contains("auth.users.read", keys);
        Assert.Contains("platform.tenant-security.read", keys);
        Assert.Contains("platform.tenant-security.manage", keys);
        Assert.DoesNotContain("platform.tenants.read", keys);
        Assert.DoesNotContain("platform.workflow.definitions.view", keys);
    }

    // Viewer is unchanged: tenant-security.read is a read action but platform-scoped → still excluded (the
    // self-service exception is Admin-only).
    [Fact]
    public void Viewer_does_not_get_tenant_self_service_platform_keys()
    {
        var catalog = new List<Permission>
        {
            new("auth", "users", "read", "Read User", null, moduleOverride: "access-governance"),
            new("platform", "tenant-security", "read", "Read Tenant Security", null),
            new("platform", "tenant-security", "manage", "Manage Tenant Security", null)
        };

        var keys = DefaultRolePermissionTemplate.SelectFor("Viewer", catalog).Select(p => p.Key).ToList();

        Assert.Equal(new[] { "auth.users.read" }, keys);
        Assert.DoesNotContain("platform.tenant-security.read", keys);
        Assert.DoesNotContain("platform.tenant-security.manage", keys);
    }

    // FIX-PERM-ATTRIBUTION-2 — organization is a genuine tenant-facing module (Organization/Position
    // Directory: /OrganizationUnits, /Positions, /PositionAssignments), so its read actions flow through
    // the Viewer branch like auth/mdm. reference-data is platform-admin-only (/Platform/ReferenceData/...)
    // despite having its own distinct Module value — IsPlatform must still treat it as platform-admin, so
    // its reads stay excluded from the tenant Viewer role (escalation boundary).
    [Fact]
    public void Viewer_gets_organization_reads_but_not_reference_data_reads()
    {
        var catalog = new List<Permission>
        {
            new("platform", "organization-units", "read", "Read Organization Units", null, moduleOverride: "organization"),
            new("platform", "positions", "read", "Read Positions", null, moduleOverride: "organization"),
            new("platform", "position-assignments", "read", "Read Position Assignments", null, moduleOverride: "organization"),
            new("platform", "BusinessReferenceData", "Read", "Read Business Reference Data", null, moduleOverride: "reference-data"),
            new("platform", "BusinessReferenceData.Consumer", "Read", "Read Published Business Reference Data", null, moduleOverride: "reference-data")
        };

        var keys = DefaultRolePermissionTemplate.SelectFor("Viewer", catalog).Select(p => p.Key).ToList();

        Assert.Contains("platform.organization-units.read", keys);
        Assert.Contains("platform.positions.read", keys);
        Assert.Contains("platform.position-assignments.read", keys);
        Assert.DoesNotContain("platform.businessreferencedata.read", keys);
        Assert.DoesNotContain("platform.businessreferencedata.consumer.read", keys);
    }

    // FEAT-ROLEPERMS-TENANT-SCOPE — IsTenantAssignable is the escalation boundary for MANUAL assignment
    // (the assign guard + the tenant-assignable catalog endpoint). It must agree with SelectFor's boundary:
    // non-platform-admin permissions and the curated tenant self-service platform.* keys are assignable;
    // every other platform.* / reference-data permission is not.
    [Theory]
    [InlineData("auth", "users", "read", true)]
    [InlineData("mdm", "legal-entities", "read", true)]
    [InlineData("goldenslim", "records", "read", true)]
    [InlineData("platform", "tenants", "read", false)]
    [InlineData("platform", "workflow.definitions", "view", false)]
    public void IsTenantAssignable_follows_the_platform_boundary(string module, string resource, string action, bool expected)
    {
        var permission = new Permission(module, resource, action, "x", null);
        Assert.Equal(expected, DefaultRolePermissionTemplate.IsTenantAssignable(permission));
    }

    [Fact]
    public void IsTenantAssignable_true_for_organization_module_and_tenant_self_service_keys()
    {
        // organization is a genuine tenant-facing module (Module="organization" via moduleOverride).
        var org = new Permission("platform", "positions", "read", "Read Positions", null, moduleOverride: "organization");
        Assert.True(DefaultRolePermissionTemplate.IsTenantAssignable(org));

        // curated tenant self-service platform.* keys stay assignable despite the platform module.
        var selfServiceRead = new Permission("platform", "tenant-security", "read", "Read Tenant Security", null);
        var selfServiceManage = new Permission("platform", "tenant-security", "manage", "Manage Tenant Security", null);
        Assert.True(DefaultRolePermissionTemplate.IsTenantAssignable(selfServiceRead));
        Assert.True(DefaultRolePermissionTemplate.IsTenantAssignable(selfServiceManage));
    }

    [Fact]
    public void IsTenantAssignable_false_for_reference_data_module()
    {
        var refData = new Permission("platform", "BusinessReferenceData", "Read", "Read BRD", null, moduleOverride: "reference-data");
        Assert.False(DefaultRolePermissionTemplate.IsTenantAssignable(refData));
    }

    // FEAT-ROLEPERMS-REDESIGN (Part A) — tenant-settings (Security/Menu Settings) is a distinct tenant self-service
    // module: assignable (Admin can hold it), granted to Admin by default, but NOT to the default Viewer.
    [Fact]
    public void IsTenantAssignable_true_for_tenant_settings_module()
    {
        var read = new Permission("platform", "tenant-security", "read", "Read Tenant Security", null, moduleOverride: "tenant-settings");
        var manage = new Permission("platform", "tenant-security", "manage", "Manage Tenant Security", null, moduleOverride: "tenant-settings");
        Assert.True(DefaultRolePermissionTemplate.IsTenantAssignable(read));
        Assert.True(DefaultRolePermissionTemplate.IsTenantAssignable(manage));
    }

    [Fact]
    public void Admin_gets_tenant_settings_keys_but_Viewer_does_not()
    {
        var catalog = new List<Permission>
        {
            new("auth", "users", "read", "Read User", null, moduleOverride: "access-governance"),
            new("platform", "tenant-security", "read", "Read Tenant Security", null, moduleOverride: "tenant-settings"),
            new("platform", "tenant-security", "manage", "Manage Tenant Security", null, moduleOverride: "tenant-settings")
        };

        var adminKeys = DefaultRolePermissionTemplate.SelectFor("Admin", catalog).Select(p => p.Key).ToList();
        Assert.Contains("platform.tenant-security.read", adminKeys);
        Assert.Contains("platform.tenant-security.manage", adminKeys);

        // Viewer must NOT receive the sensitive tenant-settings read despite it now being a non-platform module.
        var viewerKeys = DefaultRolePermissionTemplate.SelectFor("Viewer", catalog).Select(p => p.Key).ToList();
        Assert.Equal(new[] { "auth.users.read" }, viewerKeys);
    }

    // FIX-MENU-SETTINGS-ACCESS — platform.tenant-navigation.manage is a tenant self-service key (tenant-scoped menu
    // write): tenant-assignable, granted to the default Admin, but NOT to the default Viewer.
    [Fact]
    public void IsTenantAssignable_true_for_tenant_navigation_manage()
    {
        var manage = new Permission("platform", "tenant-navigation", "manage", "Manage Tenant Navigation", null, moduleOverride: "tenant-settings");
        Assert.True(DefaultRolePermissionTemplate.IsTenantAssignable(manage));
    }

    [Fact]
    public void Admin_gets_tenant_navigation_manage_but_Viewer_does_not()
    {
        var catalog = new List<Permission>
        {
            new("auth", "users", "read", "Read User", null, moduleOverride: "access-governance"),
            new("platform", "tenant-navigation", "manage", "Manage Tenant Navigation", null, moduleOverride: "tenant-settings")
        };

        var adminKeys = DefaultRolePermissionTemplate.SelectFor("Admin", catalog).Select(p => p.Key).ToList();
        Assert.Contains("platform.tenant-navigation.manage", adminKeys);

        // Viewer must NOT receive the sensitive tenant-wide menu write despite it being a non-platform module.
        var viewerKeys = DefaultRolePermissionTemplate.SelectFor("Viewer", catalog).Select(p => p.Key).ToList();
        Assert.DoesNotContain("platform.tenant-navigation.manage", viewerKeys);
        Assert.Equal(new[] { "auth.users.read" }, viewerKeys);
    }

    [Fact]
    public void Unknown_role_gets_nothing()
    {
        Assert.Empty(DefaultRolePermissionTemplate.SelectFor("Nope", Catalog()));
    }

    // BL-359 — MOD-0117-FU01: an explicit-grant-only permission never enters any default/startup role
    // template, including SuperAdmin's otherwise-unfiltered full-catalog branch (owner decision, 2026-09-11).
    [Fact]
    public void SuperAdmin_excludes_explicit_grant_only_permissions()
    {
        var catalog = Catalog();
        catalog.Add(new Permission("ppm", "portfolios", "assign-owner", "Assign Owner", null));

        var superAdminKeys = DefaultRolePermissionTemplate.SelectFor("SuperAdmin", catalog).Select(p => p.Key).ToList();

        Assert.DoesNotContain("ppm.portfolios.assign-owner", superAdminKeys);
        Assert.Equal(5, superAdminKeys.Count); // the base Catalog() rows only
    }

    [Fact]
    public void Admin_and_Viewer_exclude_explicit_grant_only_permissions_even_under_a_matching_module()
    {
        var catalog = new List<Permission>
        {
            new("mdm", "legal-entities", "read", "Read Legal Entity", null, moduleOverride: "legal-entity"),
            new("ppm", "portfolios", "assign-owner", "Assign Owner", null, moduleOverride: "legal-entity")
        };

        var adminKeys = DefaultRolePermissionTemplate.SelectFor("Admin", catalog).Select(p => p.Key).ToList();
        var viewerKeys = DefaultRolePermissionTemplate.SelectFor("Viewer", catalog).Select(p => p.Key).ToList();

        Assert.DoesNotContain("ppm.portfolios.assign-owner", adminKeys);
        Assert.DoesNotContain("ppm.portfolios.assign-owner", viewerKeys);
    }

    // WP-INFRA-AUTH-ACCOUNT-KIND-01 — the second explicit-grant-only key (K1-a: remove it from
    // ExplicitGrantOnlyPermissions.Keys and this goes red together with its siblings in the other grant paths).
    [Fact]
    public void Account_kind_manage_enters_no_default_role_even_under_the_Admin_module_while_lookup_reaches_Admin()
    {
        var catalog = Catalog();
        catalog.Add(new Permission("auth", "users", "lookup", "Lookup Users", null, moduleOverride: "access-governance"));
        catalog.Add(new Permission("auth", "users.account-kind", "manage", "Manage Account Kind", null, moduleOverride: "access-governance"));

        var superAdminKeys = DefaultRolePermissionTemplate.SelectFor("SuperAdmin", catalog).Select(p => p.Key).ToList();
        var adminKeys = DefaultRolePermissionTemplate.SelectFor("Admin", catalog).Select(p => p.Key).ToList();
        var viewerKeys = DefaultRolePermissionTemplate.SelectFor("Viewer", catalog).Select(p => p.Key).ToList();

        Assert.DoesNotContain("auth.users.account-kind.manage", superAdminKeys);
        Assert.DoesNotContain("auth.users.account-kind.manage", adminKeys);
        Assert.DoesNotContain("auth.users.account-kind.manage", viewerKeys);
        Assert.Contains("auth.users.lookup", adminKeys);      // ordinary tenant key: Admin breadth clause
        Assert.DoesNotContain("auth.users.lookup", viewerKeys); // not a read action
    }

    // MOD0024-TASK-READ-ACCESS-01 (BL-349, owner decision 2026-09-13) — the third explicit-grant-only key. Proven
    // the same way UsersAccountKindManage is proven directly above: even under its OWN module ("tasks", where the
    // Task Engine's other platform.tasks.* keys DO reach Admin's breadth clause were "tasks" listed there), it
    // reaches no default role — a tenant activating Task Engine must not hand every Admin/Viewer "read every task".
    [Fact]
    public void Tasks_read_all_enters_no_default_role_even_under_a_module_ordinary_task_keys_would_reach()
    {
        var catalog = Catalog();
        catalog.Add(new Permission("platform", "tasks", "read", "Read Task", null, moduleOverride: "tasks"));
        catalog.Add(new Permission("platform", "tasks", "read-all", "Read All Tasks", null, moduleOverride: "tasks"));

        var superAdminKeys = DefaultRolePermissionTemplate.SelectFor("SuperAdmin", catalog).Select(p => p.Key).ToList();
        var adminKeys = DefaultRolePermissionTemplate.SelectFor("Admin", catalog).Select(p => p.Key).ToList();
        var viewerKeys = DefaultRolePermissionTemplate.SelectFor("Viewer", catalog).Select(p => p.Key).ToList();

        Assert.DoesNotContain("platform.tasks.read-all", superAdminKeys);
        Assert.DoesNotContain("platform.tasks.read-all", adminKeys);
        Assert.DoesNotContain("platform.tasks.read-all", viewerKeys);
    }

    // WP-PSS-MOD0024-BL392-WORK-REPORT-READ-EXPLICIT-01 (BL-392, owner decision 2026-09-14) — the fourth
    // explicit-grant-only key. Placed under a module the Admin breadth clause DOES reach, with its ordinary sibling
    // report key beside it, so the exclusion is not vacuous: the sibling reaches Admin (and Viewer, being a read),
    // the tenant-wide key reaches no default role. Remove the key from ExplicitGrantOnlyPermissions.Keys and this
    // test goes red (measured 2026-09-14: the SuperAdmin full-catalog assertion is the first to fail).
    [Fact]
    public void Work_report_read_tenant_wide_enters_no_default_role_even_under_the_Admin_module_while_work_report_read_reaches_Admin()
    {
        var catalog = Catalog();
        catalog.Add(new Permission("platform", "tasks.work-report", "read", "Read Work Report", null,
            moduleOverride: "access-governance", scope: PermissionScope.Tenant));
        catalog.Add(new Permission("platform", "tasks.work-report", "read-tenant-wide", "View Work Report Tenant-Wide", null,
            moduleOverride: "access-governance", scope: PermissionScope.Tenant));

        var superAdminKeys = DefaultRolePermissionTemplate.SelectFor("SuperAdmin", catalog).Select(p => p.Key).ToList();
        var adminKeys = DefaultRolePermissionTemplate.SelectFor("Admin", catalog).Select(p => p.Key).ToList();
        var viewerKeys = DefaultRolePermissionTemplate.SelectFor("Viewer", catalog).Select(p => p.Key).ToList();

        Assert.DoesNotContain("platform.tasks.work-report.read-tenant-wide", superAdminKeys);
        Assert.DoesNotContain("platform.tasks.work-report.read-tenant-wide", adminKeys);
        Assert.DoesNotContain("platform.tasks.work-report.read-tenant-wide", viewerKeys);
        Assert.Contains("platform.tasks.work-report.read", adminKeys);  // ordinary key: Admin breadth clause
        Assert.Contains("platform.tasks.work-report.read", viewerKeys); // ordinary read: Viewer read clause
    }

    [Fact]
    public void Deleted_permissions_are_excluded()
    {
        var catalog = Catalog();
        catalog[0].MarkAsDeletedForTest(); // auth.users.read

        var keys = DefaultRolePermissionTemplate.SelectFor("Admin", catalog).Select(p => p.Key).ToList();

        Assert.DoesNotContain("auth.users.read", keys);
        Assert.Contains("auth.users.create", keys);
    }
}

internal static class PermissionTestExtensions
{
    // Permission has no public delete mutator; IsDeleted is a settable base property.
    public static void MarkAsDeletedForTest(this Permission permission) => permission.IsDeleted = true;
}
