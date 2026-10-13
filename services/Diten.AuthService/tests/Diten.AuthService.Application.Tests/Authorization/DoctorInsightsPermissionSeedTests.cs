using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Seed;

namespace Diten.AuthService.Application.Tests.Authorization;

/// <summary>
/// WP-VW-W4 (W4-BE-c, §2.7) — the five <c>crm.doctor-insights.*</c> keys are in the canonical seed (English descriptions,
/// tenant-scoped, granted to no role), and seeded as catalog rows only (granted to no role).
/// Mirrors <see cref="PlannedVisitPermissionSeedTests"/>.
/// </summary>
public sealed class DoctorInsightsPermissionSeedTests
{
    [Theory]
    [InlineData("crm\", \"doctor-insights\", \"read\"")]
    [InlineData("crm\", \"doctor-insights\", \"manage\"")]
    [InlineData("crm\", \"doctor-insights\", \"read-all\"")]
    [InlineData("crm\", \"doctor-insights\", \"read-aggregate\"")]
    [InlineData("crm\", \"doctor-insights\", \"thresholds.manage\"")]
    public void Doctor_insights_permission_is_present_in_canonical_seed(string permissionConstructor)
        => Assert.Contains(permissionConstructor, File.ReadAllText(SeederPath()), StringComparison.Ordinal);

    [Theory]
    [InlineData("read", "crm.doctor-insights.read")]
    [InlineData("manage", "crm.doctor-insights.manage")]
    [InlineData("read-all", "crm.doctor-insights.read-all")]
    [InlineData("read-aggregate", "crm.doctor-insights.read-aggregate")]
    [InlineData("thresholds.manage", "crm.doctor-insights.thresholds.manage")]
    public void Seeded_shape_yields_the_key_crm_enforces_with_tenant_scope(string action, string expectedKey)
    {
        var permission = new Permission("crm", "doctor-insights", action, "x", "x", moduleOverride: "crm-doctor-insights");

        Assert.Equal(expectedKey, permission.Key);
        Assert.Equal(PermissionScope.Tenant, permission.Scope);
        Assert.DoesNotContain("crm-doctor-insights", DefaultRolePermissionTemplate.PlatformAdminModules);
    }

    [Fact]
    public void The_keys_are_catalog_rows_only_no_grant_is_seeded()
    {
        // read-all is NOT (yet) explicit-grant-only: that list is owner-decided (AccountKindTests pins it) — raised in the
        // W4-BE-c report as an open decision.
        var source = File.ReadAllText(SeederPath());
        // catalog rows only — no role allowlist names a doctor insights key
        Assert.Equal(5, CountOf(source, "moduleOverride: \"crm-doctor-insights\""));
        Assert.DoesNotContain("\"crm.doctor-insights.", source, StringComparison.Ordinal);
    }

    [Fact]
    public void The_descriptions_are_english()
    {
        var lines = File.ReadAllLines(SeederPath()).Where(l => l.Contains("\"doctor-insights\"", StringComparison.Ordinal)).ToList();
        Assert.Equal(5, lines.Count);
        Assert.All(lines, l => Assert.DoesNotMatch("[çğıöşüÇĞİÖŞÜ]", l));
    }

    private static int CountOf(string text, string value)
    {
        var count = 0;
        for (var i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + value.Length, StringComparison.Ordinal)) count++;
        return count;
    }

    private static string SeederPath()
    {
        var probe = new DirectoryInfo(AppContext.BaseDirectory);
        var relative = Path.Combine("services", "Diten.AuthService", "src", "Diten.AuthService.Persistence", "Seed", "DataSeeder.cs");
        while (probe is not null)
        {
            var candidate = Path.Combine(probe.FullName, relative);
            if (File.Exists(candidate)) return candidate;
            probe = probe.Parent;
        }

        throw new FileNotFoundException("DataSeeder.cs not found from " + AppContext.BaseDirectory + " (" + typeof(DataSeeder).Name + ").");
    }
}
