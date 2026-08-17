using Diten.HumanCapitalService.Domain.Repositories;
using Diten.HumanCapitalService.Persistence.Repositories;
using Diten.HumanCapitalService.Persistence.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Diten.HumanCapitalService.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        RegisterMongoSerializers();

        var mongoSettings = new MongoSettings
        {
            ConnectionString = configuration["Mongo:ConnectionString"]
                ?? configuration["MongoDbSettings:ConnectionString"]
                ?? throw new InvalidOperationException("Configuration error: Mongo connection string is missing."),
            DatabaseName = configuration["Mongo:DatabaseName"]
                ?? configuration["MongoDbSettings:DatabaseName"]
                ?? throw new InvalidOperationException("Configuration error: Mongo database name is missing.")
        };

        var mongoClientSettings = MongoClientSettings.FromConnectionString(mongoSettings.ConnectionString);
        var mongoClient = new MongoClient(mongoClientSettings);
        var database = mongoClient.GetDatabase(mongoSettings.DatabaseName);

        services.AddSingleton(mongoSettings);
        services.AddSingleton<IMongoClient>(mongoClient);
        services.AddScoped<IMongoDatabase>(_ => database);
        services.AddScoped<IEmployeeProjectionRepository, MongoEmployeeProjectionRepository>();
        services.AddScoped<IPositionAssignmentOverlayRepository, MongoPositionAssignmentOverlayRepository>();
        services.AddScoped<IOffboardingCaseRepository, MongoOffboardingCaseRepository>();

        return services;
    }

    private static void RegisterMongoSerializers()
    {
        TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        TryRegisterSerializer(new DateTimeOffsetSerializer(BsonType.Document));
    }

    private static void TryRegisterSerializer<T>(IBsonSerializer<T> serializer)
    {
        try
        {
            BsonSerializer.RegisterSerializer(serializer);
        }
        catch (BsonSerializationException)
        {
            // Serializer registration is process-wide; duplicate registration is harmless.
        }
    }
}
