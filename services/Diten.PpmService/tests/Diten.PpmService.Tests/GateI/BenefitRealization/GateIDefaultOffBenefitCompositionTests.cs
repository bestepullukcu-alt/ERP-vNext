using Diten.PpmService.Application.Features.BenefitCommitments.GateI.BenefitRealization;
using Diten.PpmService.Infrastructure;
using Diten.PpmService.Infrastructure.GateI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.PpmService.Tests.GateI.BenefitRealization;

public sealed class GateIDefaultOffBenefitCompositionTests
{
    [Fact]
    public async Task Benefit_authority_port_is_default_off_and_never_fabricates_not_found()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [GateICompositionGate.CommonFlag] = "false",
            [GateICompositionGate.BenefitRealizationFlag] = "false"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure(configuration);
        using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<BenefitCommitmentOutcomeReferenceValidator>());
        var result = await provider.GetRequiredService<IOutcomeReferenceAuthorityPort>()
            .ValidateAsync(null!, CancellationToken.None);
        Assert.Equal(OutcomeReferenceAuthorityDisposition.Unavailable, result.Disposition);
    }
}
