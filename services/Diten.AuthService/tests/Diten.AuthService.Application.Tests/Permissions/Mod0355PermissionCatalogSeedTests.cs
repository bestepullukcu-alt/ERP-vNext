using System.Text.RegularExpressions;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Seed;

namespace Diten.AuthService.Application.Tests.Permissions;

public sealed class Mod0355PermissionCatalogSeedTests
{
    private static readonly string[] ExactCoreKeys =
    [
        "management-governance.process-modeling.architectures.read",
        "management-governance.process-modeling.architectures.create",
        "management-governance.process-modeling.architectures.update",
        "management-governance.process-modeling.architectures.archive",
        "management-governance.process-modeling.definitions.read",
        "management-governance.process-modeling.definitions.create",
        "management-governance.process-modeling.definitions.update",
        "management-governance.process-modeling.definitions.archive",
        "management-governance.process-modeling.models.read",
        "management-governance.process-modeling.models.create",
        "management-governance.process-modeling.models.update",
        "management-governance.process-modeling.models.request-review",
        "management-governance.process-modeling.models.return-to-draft",
        "management-governance.process-modeling.models.publish",
        "management-governance.process-modeling.models.retire",
        "management-governance.process-modeling.models.create-revision"
    ];

    [Fact]
    public void Seed_contains_exactly_the_sixteen_core_permissions_with_tenant_scope_and_mod0355_attribution()
    {
        var rows = ExtractMod0355Rows();

        Assert.Equal(16, rows.Count);
        Assert.Equal(ExactCoreKeys.Order(), rows.Select(row => row.Key).Order());
        Assert.All(rows, row =>
        {
            Assert.Equal("MOD-0355", row.Module);
            Assert.Equal(PermissionScope.Tenant, row.Scope);
        });
    }

    [Fact]
    public void Seed_does_not_onboard_improvement_or_kpi_permissions()
    {
        var keys = ExtractMod0355Rows().Select(row => row.Key).ToArray();

        Assert.DoesNotContain(keys, key => key.Contains(".improvements.", StringComparison.Ordinal));
        Assert.DoesNotContain(keys, key => key.Contains(".kpi-bindings.", StringComparison.Ordinal));
    }

    [Fact]
    public void Catalog_presence_does_not_create_a_default_admin_or_viewer_grant()
    {
        var catalog = ExtractMod0355Rows();

        Assert.Empty(DefaultRolePermissionTemplate.SelectFor(DefaultRolePermissionTemplate.AdminRole, catalog));
        Assert.Empty(DefaultRolePermissionTemplate.SelectFor(DefaultRolePermissionTemplate.ViewerRole, catalog));
        Assert.All(
            catalog.Where(permission => string.Equals(permission.Action, "read", StringComparison.Ordinal)),
            permission => Assert.Contains(permission.Key, DefaultRolePermissionTemplate.EntitlementOnlyViewerPermissions));
    }

    private static IReadOnlyList<Permission> ExtractMod0355Rows()
    {
        var source = File.ReadAllText(GetDataSeederPath());
        var pattern = new Regex(
            """new\("management-governance", "(?<resource>process-modeling\.[^"]+)", "(?<action>[^"]+)", [^\n]+moduleOverride: "MOD-0355", scope: PermissionScope\.Tenant\)""",
            RegexOptions.CultureInvariant);

        return pattern.Matches(source)
            .Select(match => new Permission(
                "management-governance",
                match.Groups["resource"].Value,
                match.Groups["action"].Value,
                "test",
                null,
                moduleOverride: "MOD-0355",
                scope: PermissionScope.Tenant))
            .ToArray();
    }

    private static string GetDataSeederPath()
    {
        var directory = Path.GetDirectoryName(typeof(DataSeeder).Assembly.Location)
            ?? throw new InvalidOperationException("Unable to resolve DataSeeder assembly directory.");

        for (var current = new DirectoryInfo(directory); current is not null; current = current.Parent)
        {
            var candidate = Path.Combine(current.FullName, "Seed", "DataSeeder.cs");
            if (File.Exists(candidate)) return candidate;

            candidate = Path.Combine(current.FullName, "services", "Diten.AuthService", "src",
                "Diten.AuthService.Persistence", "Seed", "DataSeeder.cs");
            if (File.Exists(candidate)) return candidate;
        }

        throw new FileNotFoundException("DataSeeder.cs could not be found.");
    }
}
