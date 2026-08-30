using Diten.AuthService.Application.Common.Authorization;

namespace Diten.AuthService.Application.Tests.Roles;

public sealed class PpmEntitlementPermissionPolicyTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void Both_entitlement_and_explicit_grant_are_required()
    {
        var allowed = Evaluate(entitled: true, granted: true);
        var noEntitlement = Evaluate(entitled: false, granted: true);
        var noGrant = Evaluate(entitled: true, granted: false);

        Assert.True(allowed.IsAllowed);
        Assert.Equal(PpmExplicitGrantState.Effective, allowed.GrantState);
        Assert.Equal(PpmAuthorizationDenial.EntitlementInactive, noEntitlement.Denial);
        Assert.Equal(PpmExplicitGrantState.Dormant, noEntitlement.GrantState);
        Assert.Equal(PpmAuthorizationDenial.ExplicitGrantMissing, noGrant.Denial);
        Assert.Equal(PpmExplicitGrantState.Absent, noGrant.GrantState);
    }

    [Theory]
    [InlineData("ppm")]
    [InlineData(" PPM")]
    [InlineData("PPM ")]
    [InlineData("MOD-0117")]
    public void Non_exact_module_identity_fails_closed(string moduleCode)
    {
        var decision = PpmEntitlementPermissionPolicy.Evaluate(
            Tenant, Tenant, moduleCode, "ppm.tasks.read", true, true);

        Assert.False(decision.IsAllowed);
        Assert.Equal(PpmAuthorizationDenial.InvalidModuleCode, decision.Denial);
        Assert.True(decision.AuditRequired);
    }

    [Fact]
    public void Only_exact_ppm_identity_requires_the_explicit_grant_path()
    {
        Assert.True(PpmEntitlementPermissionPolicy.RequiresExplicitGrant("PPM"));
        Assert.False(PpmEntitlementPermissionPolicy.RequiresExplicitGrant("ppm"));
        Assert.False(PpmEntitlementPermissionPolicy.RequiresExplicitGrant("MDM"));
    }

    private static PpmAuthorizationDecision Evaluate(bool entitled, bool granted) =>
        PpmEntitlementPermissionPolicy.Evaluate(
            Tenant, Tenant, "PPM", "ppm.tasks.read", entitled, granted);
}
