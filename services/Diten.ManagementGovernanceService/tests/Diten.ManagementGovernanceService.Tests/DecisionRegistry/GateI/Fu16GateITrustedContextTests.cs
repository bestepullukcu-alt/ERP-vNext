using Diten.ManagementGovernanceService.Api.DecisionRegistry;
using Diten.ManagementGovernanceService.Application.DecisionRegistry.GateI;
using Diten.Platform.Common.Authorization.Fu16;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.ManagementGovernanceService.Tests.DecisionRegistry.GateI;

public sealed class Fu16GateITrustedContextTests
{
    [Fact]
    public void Exact_shared_context_projects_without_payload_or_identity_substitution()
    {
        var expected = Shared();
        var result = new Fu16GateITrustedContextAccessor(new SharedAccessor(expected)).Current;

        Assert.True(result.Authenticated);
        Assert.Equal(expected.TenantId, result.TenantId);
        Assert.Equal(expected.EffectiveActorId, result.EffectiveActorId);
        Assert.Equal(expected.RequestHash, result.RequestHash);
        Assert.Equal(DecisionReferenceValidationProfile.Operation, result.Operation);
        Assert.Equal(DecisionReferenceValidationProfile.Permission, result.Permission);
    }

    [Fact]
    public void Missing_or_non_exact_profile_is_fail_closed()
    {
        Assert.False(new Fu16GateITrustedContextAccessor(new SharedAccessor(null)).Current.Authenticated);

        var aliases = new[]
        {
            Shared() with { ClientId = "DITEN.MANAGEMENT-GOVERNANCE" },
            Shared() with { Audience = "alias" },
            Shared() with { ProtocolScope = "alias" },
            Shared() with { OperationId = "alias" },
            Shared() with { Permissions = [DecisionReferenceValidationProfile.Permission, "extra"] },
            Shared() with { Permissions = ["alias"] }
        };
        Assert.All(aliases, value =>
            Assert.False(new Fu16GateITrustedContextAccessor(new SharedAccessor(value)).Current.Authenticated));
    }

    [Fact]
    public void Legacy_local_registration_remains_disabled_and_unavailable()
    {
        var services = new ServiceCollection().AddDecisionRegistryGateILocalProvider();
        using var provider = services.BuildServiceProvider();
        Assert.False(provider.GetRequiredService<DecisionReferenceProviderOptions>().Enabled);
        Assert.IsType<UnavailableGateITrustedContextAccessor>(provider.GetRequiredService<IGateITrustedContextAccessor>());
    }

    private static Fu16TrustedRequestContext Shared() => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Guid.Parse("33333333-3333-3333-3333-333333333333"),
        Guid.Parse("44444444-4444-4444-4444-444444444444"),
        Guid.Parse("55555555-5555-5555-5555-555555555555"),
        Guid.Parse("66666666-6666-6666-6666-666666666666"),
        DecisionReferenceValidationProfile.ClientId,
        "https://auth.test",
        DecisionReferenceValidationProfile.Audience,
        "diten-delegated-actor-proof+jwt",
        DecisionReferenceValidationProfile.Scope,
        DecisionReferenceValidationProfile.Operation,
        [DecisionReferenceValidationProfile.Permission],
        new string('a', 64), 7, 8, 9,
        DateTimeOffset.Parse("2026-08-27T10:00:00Z"),
        DateTimeOffset.Parse("2026-08-27T10:00:00Z"),
        DateTimeOffset.Parse("2026-08-27T10:05:00Z"));

    private sealed class SharedAccessor(Fu16TrustedRequestContext? current) : IFu16TrustedRequestContextAccessor
    {
        public Fu16TrustedRequestContext? Current { get; } = current;
        public void Publish(Fu16TrustedRequestContext context) => throw new NotSupportedException();
    }
}
