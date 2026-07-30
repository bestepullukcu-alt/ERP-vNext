using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Tests.Permissions;

/// <summary>
/// Governance-to-catalog acceptance fixture only. It proves the existing Permission catalog shape
/// can register the closed Phase 2A contract; it is deliberately not a production manifest or seeder.
/// </summary>
public sealed class PpmPhase2APermissionContractTests
{
    private static readonly string[] Expected =
    [
        "ppm.portfolios.read", "ppm.portfolios.create", "ppm.portfolios.update", "ppm.portfolios.change-lifecycle",
        "ppm.initiatives.read", "ppm.initiatives.create", "ppm.initiatives.update", "ppm.initiatives.change-lifecycle",
        "ppm.programs.read", "ppm.programs.create", "ppm.programs.update", "ppm.programs.change-lifecycle",
        "ppm.projects.read", "ppm.projects.create", "ppm.projects.update", "ppm.projects.change-lifecycle"
    ];

    [Fact]
    public void Closed_contract_has_exactly_sixteen_unique_lowercase_keys()
    {
        Assert.Equal(16, Expected.Length);
        Assert.Equal(16, Expected.Distinct(StringComparer.Ordinal).Count());
        Assert.All(Expected, key => Assert.Equal(key.ToLowerInvariant(), key));
    }

    [Fact]
    public void Existing_catalog_entity_shape_registers_exact_contract_without_aliases()
    {
        var catalog = Expected.Select(CreatePermission).ToArray();
        Assert.Equal(Expected.Order(), catalog.Select(x => x.Key).Order());
        Assert.All(catalog, permission => Assert.Equal("ppm", permission.Module));
    }

    [Theory]
    [InlineData("ppm.portfolios.archive")]
    [InlineData("ppm.projects.archive")]
    [InlineData("ppm.investments.read")]
    [InlineData("ppm.benefits.read")]
    [InlineData("ppm.external-context.read")]
    public void Out_of_scope_or_alias_key_is_not_in_phase_2a_contract(string key)
        => Assert.DoesNotContain(key, Expected);

    private static Permission CreatePermission(string key)
    {
        var parts = key.Split('.');
        return new Permission(parts[0], parts[1], parts[2], key, null);
    }
}
