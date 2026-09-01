using Diten.ManagementGovernanceService.Application.Features.Dws;
using Diten.ManagementGovernanceService.Infrastructure.Modules.Dws;
using Diten.ManagementGovernanceService.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Diten.ManagementGovernanceService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddDwsInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        // Production adapters are intentionally absent. Local-test composition is explicit and separate.
        return services;
    }

    public static IServiceCollection AddDwsLocalTestInfrastructure(
        this IServiceCollection services,
        string mongoUri,
        string databaseName)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(mongoUri) || string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException("dws_transaction_unavailable");

        services.AddDwsLocalTestPersistence(mongoUri, databaseName);
        services.AddSingleton<IMod0117ContextValidationAdapter, LocalTestMod0117ContextAdapter>();
        services.AddSingleton<IFu16DwsAuthorizationAdapter, LocalTestFu16AuthorizationAdapter>();
        services.AddSingleton<IDwsAuditSimulator, LocalTestDwsAuditSimulator>();
        services.AddScoped<IDwsLocalActionExecutor, DwsMongoLocalActionExecutor>();
        services.AddSingleton<DwsLocalMod0117Fixture>();
        services.AddSingleton<IMod0117DwsContextValidator, LocalTestMod0117FunctionalContextValidator>();
        services.AddSingleton<DwsLocalFu16Fixture>();
        services.AddSingleton<IFu16DwsFunctionalAuthorization, LocalTestFu16FunctionalAuthorization>();
        services.AddScoped<IDwsLocalAuditObserver, LocalTestDwsAuditObserver>();
        return services;
    }
}
