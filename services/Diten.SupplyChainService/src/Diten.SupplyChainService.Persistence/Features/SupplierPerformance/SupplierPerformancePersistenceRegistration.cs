using Diten.BuildingBlocks.Eventing;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Diten.SupplyChainService.Application.Features.SupplierPerformance;
using Diten.SupplyChainService.Domain.Features.SupplierPerformance;
namespace Diten.SupplyChainService.Persistence.Features.SupplierPerformance;
public static class SupplierPerformancePersistenceRegistration
{
    public static IServiceCollection AddSupplierPerformancePersistence(this IServiceCollection s)
    {
        s.AddScoped<SupplierPerformanceRequestContext>(); s.AddScoped<IEvaluationRepository, EvaluationRepository>();
        s.AddSingleton<ISupplierPerformanceCommitProbe, NoOpSupplierPerformanceCommitProbe>(); s.AddHostedService<SupplierPerformanceSchema>();
        s.AddKeyedSingleton<IEventOutboxStore>(SupplierPerformanceOutboxStore.ServiceKey, (p, _) => new SupplierPerformanceOutboxStore(p.GetRequiredService<IMongoDatabase>()));
        s.AddHostedService<SupplierPerformanceOutboxWorker>();
        return s;
    }
}
