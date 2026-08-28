using Diten.AuthService.Application.Common.Authorization;

namespace Diten.AuthService.Application.Tests.Roles;

public sealed class EntitlementPermissionPolicyTests
{
    private readonly EntitlementPermissionPolicy _sut = new();

    [Theory]
    [InlineData("PPM")]
    [InlineData("ppm")]
    [InlineData(" PpM ")]
    public void Ppm_is_explicit_only(string moduleCode)
        => Assert.Equal(EntitlementPermissionMode.ExplicitOnlyPreserveOnEntitlementRemoval, _sut.Resolve(moduleCode));

    [Fact]
    public void Mod0355_is_explicit_only_for_canonical_and_sync_normalized_codes_without_whitespace_alias()
    {
        Assert.Equal(EntitlementPermissionMode.ExplicitOnlyPreserveOnEntitlementRemoval, _sut.Resolve("MOD-0355"));
        Assert.Equal(EntitlementPermissionMode.ExplicitOnlyPreserveOnEntitlementRemoval, _sut.Resolve("mod-0355"));
        Assert.Equal(EntitlementPermissionMode.LegacyAutoGrantAndRevoke, _sut.Resolve(" MOD-0355 "));
    }

    [Theory]
    [InlineData("MDM")]
    [InlineData("workflow")]
    [InlineData("")]
    [InlineData(null)]
    public void Non_ppm_modules_retain_legacy_behavior(string? moduleCode)
        => Assert.Equal(EntitlementPermissionMode.LegacyAutoGrantAndRevoke, _sut.Resolve(moduleCode));
}
