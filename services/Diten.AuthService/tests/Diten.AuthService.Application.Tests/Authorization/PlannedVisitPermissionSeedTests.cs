using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Seed;

namespace Diten.AuthService.Application.Tests.Authorization;

/// <summary>
/// WP-MOB-B03 — canonical-seed tests for the 3 <c>crm.planned-visit.*</c> keys and the tenant-97c5 Admin grant.
/// Mirrors <see cref="TerritoryPermissionSeedTests"/> (source-text assertions over DataSeeder.cs) plus a runtime check
/// that the seeded shape resolves to the right key and to Tenant scope (never PlatformAdmin).
/// </summary>
public sealed class PlannedVisitPermissionSeedTests
{
    [Theory]
    [InlineData("crm\", \"planned-visit\", \"read")]
    [InlineData("crm\", \"planned-visit\", \"manage")]
    [InlineData("crm\", \"planned-visit\", \"confirm")]
    public void Planned_visit_permission_is_present_in_canonical_seed(string permissionConstructor)
        => Assert.Contains(permissionConstructor, File.ReadAllText(GetDataSeederPath()), StringComparison.Ordinal);

    [Fact]
    public void Planned_visit_keys_use_crm_planned_visit_module_override()
        => Assert.Contains("moduleOverride: \"crm-planned-visit\"", File.ReadAllText(GetDataSeederPath()), StringComparison.Ordinal);

    // The seeded constructor shape yields exactly the keys CrmService enforces, tenant-scoped.
    [Theory]
    [InlineData("read", "crm.planned-visit.read")]
    [InlineData("manage", "crm.planned-visit.manage")]
    [InlineData("confirm", "crm.planned-visit.confirm")]
    public void Seeded_shape_yields_canonical_key_with_tenant_scope(string action, string expectedKey)
    {
        var permission = new Permission("crm", "planned-visit", action, "x", "x", moduleOverride: "crm-planned-visit");

        Assert.Equal(expectedKey, permission.Key);
        Assert.Equal(PermissionScope.Tenant, permission.Scope);
        Assert.DoesNotContain("crm-planned-visit", DefaultRolePermissionTemplate.PlatformAdminModules);
    }

    // The tenant-97c5 Admin grant exists, is wired into SeedAsync, and uses the explicit 3-key allowlist.
    [Theory]
    [InlineData("private static async Task SeedTenant97c5CrmPlannedVisitGrantAsync")]
    [InlineData("await SeedTenant97c5CrmPlannedVisitGrantAsync(database);")]
    [InlineData("\"crm.planned-visit.read\", \"crm.planned-visit.manage\", \"crm.planned-visit.confirm\"")]
    public void Tenant97c5_planned_visit_grant_is_wired(string expected)
        => Assert.Contains(expected, File.ReadAllText(GetDataSeederPath()), StringComparison.Ordinal);

    private static string GetDataSeederPath()
    {
        var directory = Path.GetDirectoryName(typeof(DataSeeder).Assembly.Location)
            ?? throw new InvalidOperationException("Unable to resolve DataSeeder assembly directory.");

        while (directory is not null)
        {
            var candidate = Path.Combine(directory, "Seed", "DataSeeder.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = Directory.GetParent(directory)?.FullName;
        }

        var probe = new DirectoryInfo(AppContext.BaseDirectory);
        var relative = Path.Combine("services", "Diten.AuthService", "src",
            "Diten.AuthService.Persistence", "Seed", "DataSeeder.cs");
        while (probe is not null)
        {
            var candidate = Path.Combine(probe.FullName, relative);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            probe = probe.Parent;
        }

        throw new FileNotFoundException("DataSeeder.cs could not be found.");
    }
}
