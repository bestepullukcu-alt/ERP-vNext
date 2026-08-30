using Diten.AuthService.Application.Common.Authorization;

namespace Diten.AuthService.Application.Tests.Roles;

public sealed class PpmEntitlementSyncRegressionTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public void Cross_tenant_grant_is_never_effective_and_is_auditable()
    {
        var decision = PpmEntitlementPermissionPolicy.Evaluate(
            TenantA, TenantB, "PPM", "ppm.work-records.read", true, true);

        Assert.False(decision.IsAllowed);
        Assert.Equal(PpmAuthorizationDenial.TenantMismatch, decision.Denial);
        Assert.Equal(PpmExplicitGrantState.Absent, decision.GrantState);
        Assert.True(decision.AuditRequired);
    }

    [Fact]
    public void Empty_tenant_identity_fails_closed()
    {
        var decision = PpmEntitlementPermissionPolicy.Evaluate(
            Guid.Empty, Guid.Empty, "PPM", "ppm.work-records.read", true, true);

        Assert.False(decision.IsAllowed);
        Assert.Equal(PpmAuthorizationDenial.TenantMismatch, decision.Denial);
    }

    [Theory]
    [InlineData("ppm.tasks.*")]
    [InlineData("ppm.tasks.view")]
    [InlineData("PPM.tasks.read")]
    [InlineData(" ppm.tasks.read")]
    public void Non_catalog_permissions_fail_closed_without_fallback(string permission)
    {
        var decision = PpmEntitlementPermissionPolicy.Evaluate(
            TenantA, TenantA, "PPM", permission, true, true);

        Assert.False(decision.IsAllowed);
        Assert.Equal(PpmAuthorizationDenial.UnknownPermission, decision.Denial);
        Assert.True(decision.AuditRequired);
    }

    [Fact]
    public void Ppm_contract_does_not_accept_mdm_identity_or_permissions()
    {
        var moduleDecision = PpmEntitlementPermissionPolicy.Evaluate(
            TenantA, TenantA, "MDM", "ppm.tasks.read", true, true);
        var permissionDecision = PpmEntitlementPermissionPolicy.Evaluate(
            TenantA, TenantA, "PPM", "mdm.legal-entities.read", true, true);

        Assert.Equal(PpmAuthorizationDenial.InvalidModuleCode, moduleDecision.Denial);
        Assert.Equal(PpmAuthorizationDenial.UnknownPermission, permissionDecision.Denial);
    }
}
