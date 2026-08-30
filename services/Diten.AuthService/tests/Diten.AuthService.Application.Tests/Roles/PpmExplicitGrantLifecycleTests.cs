using Diten.AuthService.Application.Common.Authorization;

namespace Diten.AuthService.Application.Tests.Roles;

public sealed class PpmExplicitGrantLifecycleTests
{
    private static readonly Guid Tenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public void Entitlement_removal_dormants_but_does_not_delete_explicit_grant()
    {
        const bool explicitGrantStillExists = true;

        var decision = Evaluate(entitled: false, granted: explicitGrantStillExists);
        var transition = PpmEntitlementPermissionPolicy.EvaluateEntitlementTransition(
            wasActive: true, isActive: false, hasExplicitGrant: explicitGrantStillExists);

        Assert.False(decision.IsAllowed);
        Assert.Equal(PpmExplicitGrantState.Dormant, decision.GrantState);
        Assert.Equal(PpmAuthorizationDenial.EntitlementInactive, decision.Denial);
        Assert.True(decision.AuditRequired);
        Assert.True(transition.ExplicitGrantPreserved);
        Assert.False(transition.CreatesGrant);
        Assert.True(transition.AuditRequired);
        Assert.Equal(PpmEntitlementPermissionPolicy.EntitlementDeactivatedAuditAction, transition.AuditAction);
    }

    [Fact]
    public void Re_entitlement_reactivates_only_an_existing_explicit_grant()
    {
        var existingGrant = Evaluate(entitled: true, granted: true);
        var deletedGrant = Evaluate(entitled: true, granted: false);
        var existingTransition = PpmEntitlementPermissionPolicy.EvaluateEntitlementTransition(
            wasActive: false, isActive: true, hasExplicitGrant: true);
        var absentTransition = PpmEntitlementPermissionPolicy.EvaluateEntitlementTransition(
            wasActive: false, isActive: true, hasExplicitGrant: false);

        Assert.True(existingGrant.IsAllowed);
        Assert.Equal(PpmExplicitGrantState.Effective, existingGrant.GrantState);
        Assert.False(deletedGrant.IsAllowed);
        Assert.Equal(PpmExplicitGrantState.Absent, deletedGrant.GrantState);
        Assert.Equal(PpmAuthorizationDenial.ExplicitGrantMissing, deletedGrant.Denial);
        Assert.True(existingTransition.ExplicitGrantPreserved);
        Assert.False(existingTransition.CreatesGrant);
        Assert.True(existingTransition.AuditRequired);
        Assert.Equal(PpmEntitlementPermissionPolicy.EntitlementActivatedAuditAction, existingTransition.AuditAction);
        Assert.False(absentTransition.ExplicitGrantPreserved);
        Assert.False(absentTransition.CreatesGrant);
    }

    [Theory]
    [InlineData(true, PpmEntitlementPermissionPolicy.EntitlementActivatedAuditAction)]
    [InlineData(false, PpmEntitlementPermissionPolicy.EntitlementDeactivatedAuditAction)]
    public void Entitlement_transitions_have_exact_audit_actions(bool active, string expected) =>
        Assert.Equal(expected, PpmEntitlementPermissionPolicy.AuditActionForEntitlement(active));

    private static PpmAuthorizationDecision Evaluate(bool entitled, bool granted) =>
        PpmEntitlementPermissionPolicy.Evaluate(
            Tenant, Tenant, "PPM", "ppm.tasks.update", entitled, granted);
}
