using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Tests.Authorization;

/// <summary>
/// WP-VW-W4 (W4-BE-a, G2) — <c>crm.visit-report.read-aggregate</c> (competitor usage counts, no doctor identity) is in the
/// canonical seed with an English description, tenant-scoped, granted to no role. Mirrors
/// <see cref="DoctorInsightsPermissionSeedTests"/>.
/// </summary>
public sealed class VisitReportAggregatePermissionSeedTests
{
    [Fact]
    public void The_key_is_seeded_once_with_an_english_description_and_no_grant()
    {
        var source = File.ReadAllText(SeederPath());
        var lines = File.ReadAllLines(SeederPath()).Where(l => l.Contains("\"visit-report\", \"read-aggregate\"", StringComparison.Ordinal)).ToList();
        Assert.Single(lines);
        Assert.DoesNotMatch("[çğıöşüÇĞİÖŞÜ]", lines[0]);
        Assert.DoesNotContain("\"crm.visit-report.read-aggregate\"", source, StringComparison.Ordinal); // no role allowlist names it
    }

    [Fact]
    public void Seeded_shape_yields_the_key_crm_enforces_with_tenant_scope()
    {
        var permission = new Permission("crm", "visit-report", "read-aggregate", "x", "x", moduleOverride: "crm-visit-report");
        Assert.Equal("crm.visit-report.read-aggregate", permission.Key);
        Assert.Equal(PermissionScope.Tenant, permission.Scope);
        Assert.DoesNotContain("crm-visit-report", DefaultRolePermissionTemplate.PlatformAdminModules);
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

        throw new FileNotFoundException("DataSeeder.cs not found from " + AppContext.BaseDirectory);
    }
}
