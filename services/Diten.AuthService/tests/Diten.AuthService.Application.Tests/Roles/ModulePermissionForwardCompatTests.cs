using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Tests.Roles;

// İŞ3-FAZ1c — the Faz-1a old/new dual-match bridge is removed now that Permission.Module IS the manifest ModuleCode
// (auth→access-governance, mdm→legal-entity, platform.workflow.*→workflow) and no catalog row carries the old
// service-name Module. These tests pin the steady state: entitlements resolve by the NEW ModuleCode, the OLD value
// no longer resolves, and the Admin baseline breadth is the new codes only.
public sealed class ModulePermissionForwardCompatTests
{
    // Same Key ("mdm.legal-entities.read"), Module = the manifest code via moduleOverride (the Faz-1b catalog shape).
    private static Permission New(string module, string resource, string action, string? newModule) =>
        new(module, resource, action, $"{module}.{resource}.{action}", null, moduleOverride: newModule);

    private static IReadOnlyList<string> Keys(IEnumerable<Permission> perms) =>
        perms.Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal).ToList();

    // ---- Entitlements resolve by the NEW manifest ModuleCode ----

    [Fact]
    public void LegalEntity_entitlement_resolves_its_permissions()
    {
        var catalog = new List<Permission>
        {
            New("mdm", "legal-entities", "read", "legal-entity"),
            New("mdm", "legal-entities", "delete", "legal-entity"),
            New("auth", "users", "read", "access-governance") // different module → must NOT match
        };

        var keys = Keys(ModulePermissionResolver.ResolvePermissions("legal-entity", catalog));

        Assert.Equal(new[] { "mdm.legal-entities.delete", "mdm.legal-entities.read" }, keys);
    }

    [Fact]
    public void AccessGovernance_entitlement_resolves_its_permissions()
    {
        var catalog = new List<Permission>
        {
            New("auth", "users", "read", "access-governance"),
            New("auth", "roles", "assign-permission", "access-governance")
        };

        var keys = Keys(ModulePermissionResolver.ResolvePermissions("access-governance", catalog));

        Assert.Equal(new[] { "auth.roles.assign-permission", "auth.users.read" }, keys);
    }

    // ---- The OLD service-name entitlement code no longer resolves (bridge removed) ----

    [Fact]
    public void Old_mdm_entitlement_code_no_longer_resolves_legal_entity_permissions()
    {
        var catalog = new List<Permission>
        {
            New("mdm", "legal-entities", "read", "legal-entity"),
            New("mdm", "legal-entities", "delete", "legal-entity")
        };

        // Post-1c, entitlements carry the manifest ModuleCode; "mdm" is not it, so nothing resolves.
        Assert.Empty(ModulePermissionResolver.ResolvePermissions("mdm", catalog));
    }

    [Fact]
    public void Old_auth_entitlement_code_no_longer_resolves_access_governance_permissions()
    {
        var catalog = new List<Permission> { New("auth", "users", "read", "access-governance") };

        Assert.Empty(ModulePermissionResolver.ResolvePermissions("auth", catalog));
    }

    // ---- Workflow (platform-hosted) resolves ONLY the new Module=="workflow" form ----

    [Fact]
    public void Workflow_resolves_the_new_workflow_module_form()
    {
        var catalog = new List<Permission>
        {
            New("platform", "workflow.definitions", "view", "workflow"),
            New("platform", "workflow.tasks", "approve", "workflow"),
            new("platform", "tenants", "read", "Read Tenant", null) // platform-admin → excluded
        };

        var keys = Keys(ModulePermissionResolver.ResolvePermissions("workflow", catalog));

        Assert.Contains("platform.workflow.definitions.view", keys);
        Assert.Contains("platform.workflow.tasks.approve", keys);
        Assert.DoesNotContain("platform.tenants.read", keys);
    }

    [Fact]
    public void The_old_platform_hosted_form_can_no_longer_be_constructed()
    {
        // FIX-RBAC-PERM-MODULE-ATTRIBUTION — this used to assert that the old shape (Module=="platform" with a
        // workflow.* resource) resolves nothing. That shape is now UNCONSTRUCTIBLE: a key minted under the service
        // namespace is attributed to the module in its second segment, so an un-overridden platform.workflow.* row
        // comes out Module=="workflow" — the new form — and resolves like its siblings.
        //
        // That is a deliberate, measured widening and the ONLY one this change produces: "workflow" is the single
        // entry in PlatformHostedTenantModules, the one allow-listed branch that bypasses the platform exclusion.
        // In the live catalog it reaches exactly one previously-orphaned row, platform.workflow.escalations.run,
        // which joins the 12 workflow-operator keys an entitled tenant role already receives. Every other
        // re-attributed permission derives into a module that is NOT in that allow-list and stays excluded by Scope.
        var catalog = new List<Permission>
        {
            new("platform", "workflow.definitions", "view", "View", null),
            new("platform", "workflow.escalations", "run", "Run", null)
        };

        Assert.All(catalog, p => Assert.Equal("workflow", p.Module));
        Assert.Equal(
            new[] { "platform.workflow.definitions.view", "platform.workflow.escalations.run" },
            Keys(ModulePermissionResolver.ResolvePermissions("workflow", catalog)).OrderBy(k => k, StringComparer.Ordinal));
    }

    [Fact]
    public void Re_attribution_widens_entitlement_reach_only_through_the_workflow_allow_list()
    {
        // The guard on the widening above: a platform.* key re-attributed to any module that is NOT on the
        // platform-hosted allow-list must stay unreachable through the entitlement bridge, because its Scope is
        // still PlatformAdmin. Adding a module to PlatformHostedTenantModules makes this test speak up.
        var catalog = new List<Permission>
        {
            new("platform", "tenants", "read", "Read Tenant", null),
            new("platform", "document-management.contract", "view", "View Contract", null),
            new("platform", "notifications", "read", "Read Notifications", null)
        };

        foreach (var permission in catalog)
        {
            Assert.NotEqual("platform", permission.Module);                     // grouping moved
            Assert.Equal(PermissionScope.PlatformAdmin, permission.Scope);      // boundary did not
            Assert.Empty(ModulePermissionResolver.ResolvePermissions(permission.Module, catalog));
        }
    }

    // ---- AdminModules is the new codes only ----

    [Fact]
    public void AdminModules_are_the_new_manifest_codes_only()
    {
        Assert.Contains("access-governance", DefaultRolePermissionTemplate.AdminModules);
        Assert.Contains("legal-entity", DefaultRolePermissionTemplate.AdminModules);
        Assert.DoesNotContain("auth", DefaultRolePermissionTemplate.AdminModules);
        Assert.DoesNotContain("mdm", DefaultRolePermissionTemplate.AdminModules);
    }

    [Fact]
    public void Admin_baseline_includes_permissions_on_the_new_Module_values()
    {
        var catalog = new List<Permission>
        {
            New("auth", "users", "read", "access-governance"),
            New("mdm", "legal-entities", "delete", "legal-entity")
        };

        var adminKeys = Keys(DefaultRolePermissionTemplate.SelectFor(DefaultRolePermissionTemplate.AdminRole, catalog));

        Assert.Contains("auth.users.read", adminKeys);
        Assert.Contains("mdm.legal-entities.delete", adminKeys);
    }

    // The single-valued override map stays empty — never used for the old/new bridge.
    [Fact]
    public void ModuleCodeOverrides_remains_empty()
    {
        Assert.Empty(ModulePermissionResolver.ModuleCodeOverrides);
    }
}
