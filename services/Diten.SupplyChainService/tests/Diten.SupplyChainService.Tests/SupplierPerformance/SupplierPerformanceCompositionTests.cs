using Diten.SupplyChainService.Api.ModuleRegistration;
using Diten.SupplyChainService.Application.Features.SupplierPerformance;
using Diten.SupplyChainService.Domain.Features.SupplierPerformance;
using Diten.SupplyChainService.Infrastructure.Features.SupplierPerformance;
using Diten.SupplyChainService.Persistence.Features.SupplierPerformance;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace Diten.SupplyChainService.Tests.SupplierPerformance;

/// <summary>
/// MOD-0147 DL-1 in the production container (CompositionRootGuardTests' host): the host tests replace Procurement and the
/// commit probe, so this proves production wires the real ones; and that the merge switches nothing on (pack §0.0, F-1):
/// no MOD-0147 manifest provider, so no catalog permission can reach Auth.
/// </summary>
[Collection(Diten.SupplyChainService.Tests.CompositionRootGuardCollection.Name)]
public sealed class SupplierPerformanceCompositionTests(Diten.SupplyChainService.Tests.CompositionRootGuardTests.Host host)
    : IClassFixture<Diten.SupplyChainService.Tests.CompositionRootGuardTests.Host>
{
    [Fact]
    public void ProductionContainer_ShouldWireTheRealEligibilityReaderRepositoryAndProbe()
    {
        using var scope = host.Services.CreateScope();
        Assert.IsType<SupplierEligibilityReader>(scope.ServiceProvider.GetRequiredService<ISupplierEligibilityReader>());
        Assert.IsType<EvaluationRepository>(scope.ServiceProvider.GetRequiredService<IEvaluationRepository>());
        Assert.IsType<NoOpSupplierPerformanceCommitProbe>(scope.ServiceProvider.GetRequiredService<ISupplierPerformanceCommitProbe>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<SupplierPerformanceRequestContext>());
    }

    [Fact]
    public void ProductionHost_ShouldMapTheSixOperationsAndRegisterNoManifestForMod0147()
    {
        var routes = host.Services.GetServices<EndpointDataSource>().SelectMany(d => d.Endpoints).OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText).Where(r => r is not null && r.StartsWith("api/supplier-performance", StringComparison.Ordinal)).Distinct().Order().ToArray();
        Assert.Equal(["api/supplier-performance/evaluations", "api/supplier-performance/evaluations/{evaluationId}", "api/supplier-performance/evaluations/{evaluationId}/submit", "api/supplier-performance/scorecards",
            "api/supplier-performance/scorecards/{scorecardId}"], routes);
        Assert.DoesNotContain(host.Services.GetServices<IModuleManifestProvider>(), p => p.GetManifest().ModuleCode == "supplier-performance-risk");
        Assert.DoesNotContain(typeof(IModuleManifestProvider).Assembly.GetTypes(), t => typeof(IModuleManifestProvider).IsAssignableFrom(t) && t.Name.Contains("SupplierPerformance", StringComparison.Ordinal));
    }
}
