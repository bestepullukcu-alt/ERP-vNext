using Diten.AuthService.Application.S2S;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Diten.AuthService.Application.Tests.S2S;

public sealed class S2SCompositionTests
{
    [Fact]
    public void Application_composition_resolves_contract_validator_without_runtime_activation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<DelegatedActorProofV1ContractValidator>());
        Assert.DoesNotContain(services, x => x.ServiceType == typeof(IHostedService) &&
            (x.ImplementationType?.Namespace?.Contains("S2S", StringComparison.Ordinal) ?? false));
    }
}
