using Diten.DataKnowledgeService.Application.Contracts;
using Diten.DataKnowledgeService.Infrastructure.Tenancy;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.DataKnowledgeService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, HttpTenantContext>();

        return services;
    }
}
