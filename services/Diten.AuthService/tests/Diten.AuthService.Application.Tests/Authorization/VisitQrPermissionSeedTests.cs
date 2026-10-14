using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Seed;

namespace Diten.AuthService.Application.Tests.Authorization;

/// <summary>
/// WP-VW-W3E (W3E-BE-a, §2.7) — the two <c>crm.visit-qr.*</c> keys are in the canonical seed (English descriptions,
/// tenant-scoped), seeded as catalog rows only (granted to no role, not explicit-grant-only). Mirrors
/// <see cref="DoctorInsightsPermissionSeedTests"/>.
/// </summary>
public sealed class VisitQrPermissionSeedTests
{
    [Theory]
    [InlineData("crm\", \"visit-qr\", \"read\"")]
    [InlineData("crm\", \"visit-qr\", \"manage\"")]
    public void Visit_qr_permission_is_present_in_canonical_seed(string permissionConstructor)
        => Assert.Contains(permissionConstructor, File.ReadAllText(SeederPath()), StringComparison.Ordinal);

    [Theory]
    [InlineData("read", "crm.visit-qr.read")]
    [InlineData("manage", "crm.visit-qr.manage")]
    public void Seeded_shape_yields_the_key_crm_enforces_with_tenant_scope(string action, string expectedKey)
    {
        var permission = new Permission("crm", "visit-qr", action, "x", "x", moduleOverride: "crm-visit-qr");

        Assert.Equal(expectedKey, permission.Key);
        Assert.Equal(PermissionScope.Tenant, permission.Scope);
        Assert.DoesNotContain("crm-visit-qr", DefaultRolePermissionTemplate.PlatformAdminModules);
    }

    [Fact]
    public void The_keys_are_catalog_rows_only_and_described_in_english()
    {
        var source = File.ReadAllText(SeederPath());
        Assert.DoesNotContain("\"crm.visit-qr.", source, StringComparison.Ordinal); // no role allowlist names them
        foreach (var key in new[] { "crm.visit-qr.read", "crm.visit-qr.manage" })
        {
            Assert.DoesNotContain(key, ExplicitGrantOnlyPermissions.Keys);
        }

        var lines = File.ReadAllLines(SeederPath()).Where(l => l.Contains("\"visit-qr\"", StringComparison.Ordinal)).ToList();
        Assert.Equal(2, lines.Count);
        Assert.All(lines, l => Assert.Contains("moduleOverride: \"crm-visit-qr\"", l));
        Assert.All(lines, l => Assert.DoesNotMatch("[çğıöşüÇĞİÖŞÜ]", l));
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
