using Diten.AuthService.Application.Common.Authorization;

namespace Diten.AuthService.Application.Tests.Permissions;

public sealed class PpmPermissionCatalogTests
{
    [Fact]
    public void Catalog_contains_exact_cross_product_of_six_resources_and_four_actions()
    {
        string[] resources =
        [
            "work-records", "tasks", "workstreams", "calendar",
            "effort-logs", "meeting-reports"
        ];
        string[] actions = ["read", "create", "update", "change-lifecycle"];
        var expected = resources
            .SelectMany(resource => actions.Select(action => $"ppm.{resource}.{action}"))
            .ToArray();

        Assert.Equal(24, PpmPermissionCatalog.Permissions.Count);
        Assert.Equal(expected, PpmPermissionCatalog.Permissions);
        Assert.Equal(24, PpmPermissionCatalog.Permissions.Distinct(StringComparer.Ordinal).Count());
    }

    [Theory]
    [InlineData("PPM", true)]
    [InlineData("ppm", false)]
    [InlineData(" PPM", false)]
    [InlineData("PPM ", false)]
    [InlineData("MOD-0117", false)]
    [InlineData("*", false)]
    [InlineData(null, false)]
    public void Module_code_is_exact_and_has_no_alias_case_or_trim_fallback(string? value, bool expected) =>
        Assert.Equal(expected, PpmPermissionCatalog.IsModuleCode(value));

    [Theory]
    [InlineData("ppm.tasks.read", true)]
    [InlineData("PPM.tasks.read", false)]
    [InlineData(" ppm.tasks.read", false)]
    [InlineData("ppm.tasks.read ", false)]
    [InlineData("ppm.tasks.*", false)]
    [InlineData("ppm.*.read", false)]
    [InlineData("ppm.tasks.view", false)]
    [InlineData("ppm.portfolios.archive", false)]
    [InlineData(null, false)]
    public void Permission_lookup_is_closed_and_ordinal(string? value, bool expected) =>
        Assert.Equal(expected, PpmPermissionCatalog.Contains(value));
}
