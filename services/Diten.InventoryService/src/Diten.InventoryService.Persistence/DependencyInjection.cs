using Diten.BuildingBlocks.Security.Secrets;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.ValidateRequiredSecrets(configuration, environment, "Inventory.Persistence", [
            new("Mongo:ConnectionString", "Inventory.Persistence", SecretRequirementKind.ConnectionString)
        ]);

        InventoryBsonMappings.Register();

        var connectionString = configuration["Mongo:ConnectionString"]
            ?? throw new InvalidOperationException("Configuration error: 'Mongo:ConnectionString' is missing.");
        var databaseName = configuration["Mongo:DatabaseName"]
            ?? throw new InvalidOperationException("Configuration error: 'Mongo:DatabaseName' is missing.");

        var client = new MongoClient(MongoClientSettings.FromConnectionString(connectionString));
        services.AddSingleton<IMongoClient>(client);
        services.AddScoped<IMongoDatabase>(_ => client.GetDatabase(databaseName));

        services.AddScoped<IInventoryLedgerRepository, InventoryLedgerRepository>();
        services.AddScoped<IInventoryReservationRepository, InventoryReservationRepository>();
        services.AddScoped<IReservationExpirySweeper, ReservationExpirySweeper>();
        services.AddScoped<ISkuPositionRebuilder, SkuPositionRebuilder>();
        services.Configure<ReservationSweeperOptions>(configuration.GetSection(ReservationSweeperOptions.SectionName));

        // Indexes + SKU-position rebuild happen before the host serves traffic; failure stops the host.
        services.AddHostedService<InventoryIndexInitializerHostedService>();
        services.AddHostedService<ReservationExpirySweeperHostedService>();

        return services;
    }
}
