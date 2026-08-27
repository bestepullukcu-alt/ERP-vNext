using Diten.FpaService.Api.Modules.Budgeting;
using Diten.FpaService.Application.Modules.Budgeting.GateI;
using Diten.Platform.Common.Authorization.Fu16;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.FpaService.UnitTests.Modules.Budgeting.GateI;

public sealed class Fu16BudgetGateITrustedContextTests
{
    [Fact]
    public void Exact_budgeting_profile_projects_trusted_identity()
    {
        var expected = Shared();
        var result = new Fu16BudgetGateITrustedContextAccessor(new SharedAccessor(expected)).Current;
        Assert.True(result.Authenticated);
        Assert.Equal(expected.TenantId, result.TenantId);
        Assert.Equal(expected.EffectiveActorId, result.ActorId);
        Assert.Equal(expected.RequestHash, result.RequestHash);
    }

    [Fact]
    public void Missing_alias_and_scenario_cross_profile_are_fail_closed()
    {
        Assert.False(new Fu16BudgetGateITrustedContextAccessor(new SharedAccessor(null)).Current.Authenticated);
        var invalid = new[]
        {
            Shared() with { ClientId = "DITEN.FPA" },
            Shared() with { Audience = "alias" },
            Shared() with { ProtocolScope = "alias" },
            Shared() with { OperationId = "scenario-planning.scenario-references.validate" },
            Shared() with { Permissions = ["scenario-planning.scenario-references.validate"] },
            Shared() with { Permissions = [BudgetGateIProfile.Permission, "extra"] }
        };
        Assert.All(invalid, value => Assert.False(
            new Fu16BudgetGateITrustedContextAccessor(new SharedAccessor(value)).Current.Authenticated));
    }

    [Fact]
    public void Contract_discovery_registration_remains_disabled()
    {
        var services = new ServiceCollection().AddBudgetingModuleForContractDiscovery();
        using var provider = services.BuildServiceProvider();
        Assert.False(provider.GetRequiredService<BudgetProviderOptions>().Enabled);
        Assert.IsType<UnavailableBudgetGateITrustedContextAccessor>(provider.GetRequiredService<IBudgetGateITrustedContextAccessor>());
    }

    private static Fu16TrustedRequestContext Shared() => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222"),
        Guid.Parse("33333333-3333-3333-3333-333333333333"), Guid.Parse("44444444-4444-4444-4444-444444444444"),
        Guid.Parse("55555555-5555-5555-5555-555555555555"), Guid.Parse("66666666-6666-6666-6666-666666666666"),
        BudgetGateIProfile.ClientId, "https://auth.test", BudgetGateIProfile.Audience,
        "diten-delegated-actor-proof+jwt", BudgetGateIProfile.Scope, BudgetGateIProfile.Operation,
        [BudgetGateIProfile.Permission], new string('a', 64), 1, 2, 3,
        DateTimeOffset.Parse("2026-08-27T10:00:00Z"), DateTimeOffset.Parse("2026-08-27T10:00:00Z"),
        DateTimeOffset.Parse("2026-08-27T10:05:00Z"));

    private sealed class SharedAccessor(Fu16TrustedRequestContext? current) : IFu16TrustedRequestContextAccessor
    {
        public Fu16TrustedRequestContext? Current { get; } = current;
        public void Publish(Fu16TrustedRequestContext context) => throw new NotSupportedException();
    }
}
