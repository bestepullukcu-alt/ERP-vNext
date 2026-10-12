using Diten.BuildingBlocks.Security.Secrets;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
        // MOD-0176 F2 (CT-F2): the ledger's default expiry hook — no hook; AddExpiry replaces it.
        services.TryAddScoped<Application.Features.Expiry.Ports.IExpiryPostingPolicy, Application.Features.Expiry.Ports.AllowAllExpiryPostingPolicy>();
        services.AddScoped<IInventoryReservationRepository, InventoryReservationRepository>();
        services.AddScoped<IReservationExpirySweeper, ReservationExpirySweeper>();
        services.AddScoped<ISkuPositionRebuilder, SkuPositionRebuilder>();
        // WP-0173-PM2 (INVENTORY-BUNDLE 1.1.0 getStockPresence): tenant-wide presence read + its access audit.
        services.AddScoped<IStockPresenceReader, StockPresenceReader>();
        services.AddScoped<IInternalAccessAuditWriter, InternalAccessAuditWriter>();
        services.Configure<ReservationSweeperOptions>(configuration.GetSection(ReservationSweeperOptions.SectionName));
        services.AddSingleton<TransactionRetryCounter>();

        // MOD-0173-FU02: the location master lives in this service. The ledger's ILocationMasterClient is REPLACED by the
        // in-process directory (no HTTP, no second list — §8, ASSUMPTION-LOC-07); the HTTP client registered by
        // AddInfrastructure stays only for the LOCATION contract smoke against Prism.
        services.AddScoped<ILocationRepository, LocationRepository>();
        services.Replace(ServiceDescriptor.Scoped<ILocationMasterClient, InventoryLocationDirectory>());

        // Indexes + SKU-position rebuild happen before the host serves traffic; failure stops the host.
        services.AddHostedService<InventoryIndexInitializerHostedService>();
        services.AddHostedService<ReservationExpirySweeperHostedService>();

        return services;
    }
}
