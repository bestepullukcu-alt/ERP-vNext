using Diten.BuildingBlocks.Security.Secrets;
using Diten.ProcurementService.Application.Common;
using Diten.ProcurementService.Application.Interfaces;
using Diten.ProcurementService.Domain.Repositories;
using Diten.ProcurementService.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Diten.ProcurementService.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.ValidateRequiredSecrets(configuration, environment, "Procurement.Persistence", [
            new("Mongo:ConnectionString", "Procurement.Persistence", SecretRequirementKind.ConnectionString)
        ]);

        // V3 GUID subtype-4 (MOD-0140 §8) — Guid'ler standart (subtype 4) UUID olarak temsil edilir.
        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

        var connectionString = configuration["Mongo:ConnectionString"]
            ?? throw new InvalidOperationException("Configuration error: 'Mongo:ConnectionString' is missing in appsettings.json.");
        var databaseName = configuration["Mongo:DatabaseName"]
            ?? throw new InvalidOperationException("Configuration error: 'Mongo:DatabaseName' is missing in appsettings.json.");

        var clientSettings = MongoClientSettings.FromConnectionString(connectionString);
        clientSettings.GuidRepresentation = GuidRepresentation.Standard;
        var client = new MongoClient(clientSettings);
        services.AddSingleton<IMongoClient>(client);
        services.AddScoped<IMongoDatabase>(_ => client.GetDatabase(databaseName));

        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IModuleSeedDataInitializer, NoOpModuleSeedDataInitializer>();

        // Best-effort index bootstrap (idempotent; Mongo down ise startup'ı çökertmez).
        services.AddHostedService<MongoIndexInitializerHostedService>();

        return services;
    }
}
