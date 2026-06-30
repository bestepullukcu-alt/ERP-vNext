using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Tests.Roles;

// S2 / AG-INFRA-COMPLETION Part A — explicit, testable ModuleCode → permission-module mapping
// (replacing the implicit DataSeeder string-match). Convention + override map, platform-excluded,
// unmatched → empty (fail-safe).
public sealed class ModulePermissionResolverTests
{
    private static List<Permission> Catalog() =>
    [
        new("auth", "users", "read", "Read User", null),
        new("mdm", "legal-entities", "read", "Read Legal Entity", null),
        new("mdm", "legal-entities", "delete", "Delete Legal Entity", null),
        new("platform", "tenants", "read", "Read Tenant", null)
    ];

    [Fact]
    public void Convention_maps_module_code_to_lowercase_permission_module()
    {
        var keys = ModulePermissionResolver.ResolvePermissions("MDM", Catalog()).Select(p => p.Key).ToList();

        Assert.Equal(
            new[] { "mdm.legal-entities.read", "mdm.legal-entities.delete" }.OrderBy(k => k),
            keys.OrderBy(k => k));
    }

    [Theory]
    [InlineData("MDM")]
    [InlineData("mdm")]
    [InlineData("  Mdm  ")]
    public void Module_code_matching_is_case_and_whitespace_insensitive(string moduleCode)
    {
        var keys = ModulePermissionResolver.ResolvePermissions(moduleCode, Catalog()).Select(p => p.Key);

        Assert.Equal(2, keys.Count());
    }

    [Fact]
    public void Unmatched_module_code_yields_empty_set_not_an_error()
    {
        Assert.Empty(ModulePermissionResolver.ResolvePermissions("DOES-NOT-EXIST", Catalog()));
    }

    // FIX-2 — curated allow-list: workflow is a tenant-scoped, platform-HOSTED module, so its
    // platform.workflow.* permissions resolve even though the broad platform.* exclusion blocks the rest.
    [Fact]
    public void Platform_hosted_workflow_resolves_its_namespace_but_not_the_platform_admin_umbrella()
    {
        var catalog = new List<Permission>
        {
            new("platform", "workflow.definitions", "view", "View Workflow", null),
            new("platform", "workflow.tasks", "approve", "Approve Workflow Task", null),
            new("platform", "tenants", "read", "Read Tenant", null) // platform-admin → must stay excluded
        };

        var keys = ModulePermissionResolver.ResolvePermissions("workflow", catalog).Select(p => p.Key).ToList();

        Assert.Contains("platform.workflow.definitions.view", keys);
        Assert.Contains("platform.workflow.tasks.approve", keys);
        Assert.DoesNotContain("platform.tenants.read", keys);
    }

    [Fact]
    public void Workflow_is_in_the_platform_hosted_allow_list()
    {
        Assert.Contains("workflow", ModulePermissionResolver.PlatformHostedTenantModules);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Null_or_blank_module_code_yields_empty_set(string? moduleCode)
    {
        Assert.Empty(ModulePermissionResolver.ResolvePermissions(moduleCode, Catalog()));
        Assert.Equal(string.Empty, ModulePermissionResolver.ResolvePermissionModule(moduleCode));
    }

    [Fact]
    public void Platform_module_code_never_resolves_permissions_for_a_tenant()
    {
        // Even though a platform.* permission exists in the catalog, the platform module is the
        // escalation boundary and must never enter a tenant via the entitlement bridge.
        Assert.Empty(ModulePermissionResolver.ResolvePermissions("platform", Catalog()));
    }

    [Fact]
    public void Platform_permissions_are_excluded_even_when_an_override_points_at_them()
    {
        var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["sneaky"] = "platform" };

        Assert.Empty(ModulePermissionResolver.ResolvePermissions("SNEAKY", Catalog(), overrides));
    }

    [Fact]
    public void Override_map_takes_precedence_over_convention()
    {
        // Mechanism test only — the production override map ships empty (no guessed mappings).
        var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["mod-0018"] = "auth" };

        Assert.Equal("auth", ModulePermissionResolver.ResolvePermissionModule("MOD-0018", overrides));
        var keys = ModulePermissionResolver.ResolvePermissions("MOD-0018", Catalog(), overrides).Select(p => p.Key);
        Assert.Equal(new[] { "auth.users.read" }, keys);
    }

    [Fact]
    public void Production_override_map_is_empty_no_guessed_mappings()
    {
        Assert.Empty(ModulePermissionResolver.ModuleCodeOverrides);
    }

    [Fact]
    public void GoldenSlim_module_code_resolves_exactly_its_four_records_permissions()
    {
        // Golden Slim dev module is entitlement-gated: catalog ModuleCode "GOLDENSLIM" must resolve
        // to exactly the 4 seeded goldenslim.records.* permissions (and nothing else).
        List<Permission> catalog =
        [
            new("auth", "users", "read", "Read User", null),
            new("goldenslim", "records", "read", "Read Golden Slim Record", null),
            new("goldenslim", "records", "create", "Create Golden Slim Record", null),
            new("goldenslim", "records", "update", "Update Golden Slim Record", null),
            new("goldenslim", "records", "delete", "Delete Golden Slim Record", null)
        ];

        var keys = ModulePermissionResolver.ResolvePermissions("GOLDENSLIM", catalog).Select(p => p.Key).ToList();

        Assert.Equal(
            new[]
            {
                "goldenslim.records.read",
                "goldenslim.records.create",
                "goldenslim.records.update",
                "goldenslim.records.delete"
            }.OrderBy(k => k),
            keys.OrderBy(k => k));
    }

    [Fact]
    public void Deleted_permissions_are_excluded()
    {
        var catalog = Catalog();
        catalog[1].IsDeleted = true; // mdm.legal-entities.read

        var keys = ModulePermissionResolver.ResolvePermissions("MDM", catalog).Select(p => p.Key).ToList();

        Assert.DoesNotContain("mdm.legal-entities.read", keys);
        Assert.Contains("mdm.legal-entities.delete", keys);
    }
}
