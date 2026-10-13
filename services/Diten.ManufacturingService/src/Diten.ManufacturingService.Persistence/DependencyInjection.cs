using Diten.BuildingBlocks.Security.Secrets;
using Diten.ManufacturingService.Domain.Repositories;
using Diten.ManufacturingService.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Diten.ManufacturingService.Persistence;

public static class DependencyInjection
{
    private static readonly object SerializerLock = new();
    private static bool _serializerRegistered;

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.ValidateRequiredSecrets(configuration, environment, "Manufacturing.Persistence", [
            new("Mongo:ConnectionString", "Manufacturing.Persistence", SecretRequirementKind.ConnectionString)
        ]);

        // V3 GUID subtype-4 — registered once per process (tests start several hosts).
        lock (SerializerLock)
        {
            if (!_serializerRegistered)
            {
                BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
                _serializerRegistered = true;
            }
        }

        var connectionString = configuration["Mongo:ConnectionString"]
            ?? throw new InvalidOperationException("Configuration error: 'Mongo:ConnectionString' is missing.");
        var databaseName = configuration["Mongo:DatabaseName"]
            ?? throw new InvalidOperationException("Configuration error: 'Mongo:DatabaseName' is missing.");

        var settings = MongoClientSettings.FromConnectionString(connectionString);
        var client = new MongoClient(settings);
        services.AddSingleton<IMongoClient>(client);
        services.AddScoped<IMongoDatabase>(_ => client.GetDatabase(databaseName));

        services.AddScoped<IBomRepository, BomRepository>();
        services.AddScoped<IBomHistoryJournal, BomHistoryJournal>();

        services.AddHostedService<MongoIndexInitializerHostedService>();
        return services;
    }
}
