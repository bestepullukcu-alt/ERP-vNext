using Diten.ManagementGovernanceService.Persistence.Modules.Dws;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Diten.ManagementGovernanceService.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddDwsPersistence(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }

    public static IServiceCollection AddDwsLocalTestPersistence(
        this IServiceCollection services,
        string mongoUri,
        string databaseName)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(mongoUri) || string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException("dws_transaction_unavailable");

        services.AddSingleton(new DwsMongoContext(new MongoClient(mongoUri), databaseName));
        services.AddSingleton<DwsMongoIndexInitializer>();
        services.AddSingleton<DwsMongoAtomicWriter>();
        return services;
    }
}
