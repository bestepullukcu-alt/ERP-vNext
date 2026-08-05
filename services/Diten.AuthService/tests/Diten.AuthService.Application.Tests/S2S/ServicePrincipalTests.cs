using Diten.AuthService.Domain.S2S;

namespace Diten.AuthService.Application.Tests.S2S;

public sealed class ServicePrincipalTests
{
    [Fact]
    public void Exact_values_are_case_sensitive_and_not_normalized()
    {
        var principal = Create();

        Assert.True(principal.AllowsAudience("diten-fpa-service"));
        Assert.False(principal.AllowsAudience("DITEN-FPA-SERVICE"));
        Assert.False(principal.AllowsAudience("diten-fpa-service "));
        Assert.True(principal.AllowsProtocolScope(DelegatedActorProofV1.ExactScope));
        Assert.False(principal.AllowsProtocolScope("diten.s2s.*"));
        Assert.Throws<S2SContractException>(() => Create("Diten-Fpa-Producer"));
        Assert.Throws<S2SContractException>(() => Create("diten-fpa-producer "));
        Assert.Throws<S2SContractException>(() => Create("diten-*-producer"));
    }

    [Fact]
    public void Lifecycle_is_fail_closed_and_versions_each_change()
    {
        var principal = Create();
        Assert.Equal(1, principal.PrincipalVersion);
        Assert.Throws<S2SContractException>(() => principal.TransitionTo(ServicePrincipalStatus.Suspended, "operator", DateTimeOffset.UtcNow));

        principal.TransitionTo(ServicePrincipalStatus.Active, "operator", DateTimeOffset.UtcNow);
        principal.TransitionTo(ServicePrincipalStatus.Suspended, "operator", DateTimeOffset.UtcNow);
        principal.TransitionTo(ServicePrincipalStatus.Active, "operator", DateTimeOffset.UtcNow);
        principal.TransitionTo(ServicePrincipalStatus.Revoked, "operator", DateTimeOffset.UtcNow);

        Assert.Equal(5, principal.PrincipalVersion);
        Assert.Throws<S2SContractException>(() => principal.TransitionTo(ServicePrincipalStatus.Active, "operator", DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Principal_is_global_and_contains_no_tenant_grant_or_role_model()
    {
        var names = typeof(ServicePrincipal).GetProperties().Select(x => x.Name).ToArray();
        Assert.DoesNotContain("TenantId", names);
        Assert.DoesNotContain("RoleId", names);
        Assert.DoesNotContain("PermissionId", names);
        Assert.DoesNotContain("Grant", names);
    }

    private static ServicePrincipal Create(string clientId = "diten-fpa-producer") => new(
        Guid.NewGuid(), clientId, "FP&A producer", ["MOD-0136"], ["diten-fpa-service"],
        [DelegatedActorProofV1.ExactScope], DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddDays(1), "test");
}
