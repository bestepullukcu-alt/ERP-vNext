using Diten.PpmService.Application.Features.InvestmentCases.GateI.FundingScenario;
using Diten.PpmService.Infrastructure;
using Diten.PpmService.Infrastructure.GateI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.PpmService.Tests.GateI.FundingScenario;

public sealed class GateIDefaultOffFundingScenarioCompositionTests
{
    [Fact]
    public async Task Atomic_budget_and_scenario_ports_are_both_default_off()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [GateICompositionGate.CommonFlag] = "true",
            [GateICompositionGate.FundingScenarioFlag] = "false"
        }).Build();
        var gate = new GateICompositionGate(configuration);
        Assert.False(gate.IsEnabled(GateICompositionLane.FundingScenario));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<FundingScenarioContractValidator>());
        var budget = await provider.GetRequiredService<IBudgetVersionReferenceValidationPort>()
            .ValidateAsync(null!, null!, CancellationToken.None);
        var scenario = await provider.GetRequiredService<IScenarioPlanningReferenceValidationPort>()
            .ValidateAsync(null!, null!, CancellationToken.None);
        Assert.Equal(ProducerReferenceState.Unavailable, budget.State);
        Assert.Equal(ProducerReferenceState.Unavailable, scenario.State);
    }
}
