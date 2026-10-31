using Microsoft.Extensions.DependencyInjection;
using Diten.SupplyChainService.Application.Features.SupplierPerformance;
using Diten.SupplyChainService.Domain.Features.SupplierPerformance;
namespace Diten.SupplyChainService.Persistence.Features.SupplierPerformance;
public static class SupplierPerformancePersistenceRegistration
{
    public static IServiceCollection AddSupplierPerformancePersistence(this IServiceCollection s)
    {
        s.AddScoped<SupplierPerformanceRequestContext>(); s.AddScoped<IEvaluationRepository, EvaluationRepository>();
        s.AddSingleton<ISupplierPerformanceCommitProbe, NoOpSupplierPerformanceCommitProbe>(); s.AddHostedService<SupplierPerformanceSchema>();
        return s;
    }
}
