using Diten.AuthService.Application.Common.Services;

namespace Diten.AuthService.Application.Tests.Permissions;

// MODULE-CATALOG AUTOMATION Phase 1 — catalog permission key grammar (mirrors Platform PKS-001 / D-6).
public sealed class PermissionKeyParserTests
{
    [Theory]
    [InlineData("goldenslim.records.read", "goldenslim", "records", "read")]
    [InlineData("mdm.legal-entities.create", "mdm", "legal-entities", "create")]
    [InlineData("PPM.Projects.View", "ppm", "projects", "view")]                                // uppercase normalized to lowercase
    [InlineData("platform.tenants.commercial.subscription.activate", "platform", "tenants.commercial.subscription", "activate")] // >= 3: deep resource path
    public void TryParse_accepts_canonical_keys(string key, string module, string resource, string action)
    {
        var ok = PermissionKeyParser.TryParse(key, out var m, out var r, out var a);

        Assert.True(ok);
        Assert.Equal(module, m);
        Assert.Equal(resource, r);
        Assert.Equal(action, a);
        // Key reconstructs losslessly through the Permission constructor's composition.
        Assert.Equal(key.ToLowerInvariant(), $"{m}.{r}.{a}");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("goldenslim")]               // 1 segment
    [InlineData("goldenslim.records")]       // 2 segments
    [InlineData("goldenslim..read")]         // collapsed empty segment → 2 segments
    [InlineData("goldenslim.records.read_all")] // underscore not allowed
    [InlineData("goldenslim.records.view!")] // illegal character
    [InlineData("goldenslim.records.1read")] // segment must start with a letter
    [InlineData("9module.records.read")]     // leading digit in module
    public void TryParse_rejects_malformed_keys(string? key)
    {
        var ok = PermissionKeyParser.TryParse(key, out var m, out var r, out var a);

        Assert.False(ok);
        Assert.Equal(string.Empty, m);
        Assert.Equal(string.Empty, r);
        Assert.Equal(string.Empty, a);
    }
}
