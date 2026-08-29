using Diten.AuthService.Persistence.Seed;

namespace Diten.AuthService.Application.Tests.Authorization;

/// <summary>
/// MOD-0151 FU01 — canonical-seed literal tests for the 5 territory permission keys and the tenant-97c5 Admin grant.
/// Mirrors <c>DocumentManagementPermissionSeedTests</c> (source-text assertions over DataSeeder.cs) so the catalog
/// keys and grant wiring cannot silently drift. Forbidden/superseded keys are asserted absent.
/// </summary>
public sealed class TerritoryPermissionSeedTests
{
    // The 5 FU01 model/node permission catalog literals (module", "resource", "action form).
    [Theory]
    [InlineData("crm\", \"territory\", \"read")]
    [InlineData("crm\", \"territory.model\", \"read")]
    [InlineData("crm\", \"territory.model\", \"manage")]
    [InlineData("crm\", \"territory.node\", \"read")]
    [InlineData("crm\", \"territory.node\", \"manage")]
    public void Fu01_territory_permission_is_present_in_canonical_seed(string permissionConstructor)
    {
        var seederSource = File.ReadAllText(GetDataSeederPath());

        Assert.Contains(permissionConstructor, seederSource, StringComparison.Ordinal);
    }

    // Territory keys are tenant-scoped (module code "crm-territory").
    [Fact]
    public void Territory_keys_use_crm_territory_module_override()
    {
        var seederSource = File.ReadAllText(GetDataSeederPath());

        Assert.Contains("moduleOverride: \"crm-territory\"", seederSource, StringComparison.Ordinal);
    }

    // Superseded / later-FU keys must NOT be seeded (pack §17 / D7).
    [Theory]
    [InlineData("crm\", \"micro-zone\", \"manage")]
    [InlineData("crm\", \"territory\", \"delete")]
    [InlineData("crm\", \"territory\", \"assign-rep")]
    [InlineData("crm\", \"territory\", \"assign-account")]
    [InlineData("crm\", \"territory.assignment\", \"manage")]
    [InlineData("crm\", \"territory.resource\", \"manage")]
    [InlineData("crm\", \"territory.approval\", \"submit")]
    [InlineData("crm\", \"territory.evidence\", \"export")]
    public void Forbidden_territory_permission_is_absent_from_canonical_seed(string permissionConstructor)
    {
        var seederSource = File.ReadAllText(GetDataSeederPath());

        Assert.DoesNotContain(permissionConstructor, seederSource, StringComparison.Ordinal);
    }

    // The tenant-97c5 Admin grant method exists, is wired into SeedAsync, and uses the explicit 5-key allowlist.
    [Theory]
    [InlineData("private static async Task SeedTenant97c5CrmTerritoryGrantAsync")]
    [InlineData("await SeedTenant97c5CrmTerritoryGrantAsync(database);")]
    [InlineData("\"crm.territory.read\"")]
    [InlineData("\"crm.territory.model.read\"")]
    [InlineData("\"crm.territory.model.manage\"")]
    [InlineData("\"crm.territory.node.read\"")]
    [InlineData("\"crm.territory.node.manage\"")]
    public void Tenant97c5_territory_grant_is_wired(string expected)
    {
        var seederSource = File.ReadAllText(GetDataSeederPath());

        Assert.Contains(expected, seederSource, StringComparison.Ordinal);
    }

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
