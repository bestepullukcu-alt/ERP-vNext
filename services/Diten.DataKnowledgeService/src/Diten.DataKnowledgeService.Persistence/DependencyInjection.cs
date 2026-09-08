using Diten.DataKnowledgeService.Domain.Repositories;
using Diten.DataKnowledgeService.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Diten.DataKnowledgeService.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        RegisterMongoSerializers();

        var connectionString = configuration["Mongo:ConnectionString"]
            ?? configuration["MongoDbSettings:ConnectionString"]
            ?? "mongodb://localhost:27017";
        var databaseName = configuration["Mongo:DatabaseName"]
            ?? configuration["MongoDbSettings:DatabaseName"]
            ?? "DitenDataKnowledge";

        var mongoClientSettings = MongoClientSettings.FromConnectionString(connectionString);
        // Force standard (subtype 4) GUID representation for Find-filter constants so read
        // filters match the subtype-4 GUIDs written for _id/TenantId. Without this, reads
        // (list/get/exists/evaluate/delete) silently fail to match written records.
        // Mirrors Auth/Platform/HCM persistence configuration.
        mongoClientSettings.GuidRepresentation = GuidRepresentation.Standard;
        var client = new MongoClient(mongoClientSettings);
        var database = client.GetDatabase(databaseName);

        services.AddSingleton<IMongoClient>(client);
        services.AddScoped<IMongoDatabase>(_ => database);
        services.AddScoped<IMetricSemanticRegistryReadinessMetadataRepository, MongoMetricSemanticRegistryReadinessMetadataRepository>();
        services.AddScoped<IDataWarehouseLakehouseReadinessMetadataRepository, MongoDataWarehouseLakehouseReadinessMetadataRepository>();
        services.AddScoped<IEtlEltPipelinesReadinessMetadataRepository, MongoEtlEltPipelinesReadinessMetadataRepository>();
        services.AddScoped<IKpiCatalogReadinessMetadataRepository, MongoKpiCatalogReadinessMetadataRepository>();
        services.AddScoped<IMetricDefinitionsOwnershipReadinessMetadataRepository, MongoMetricDefinitionsOwnershipReadinessMetadataRepository>();
        services.AddScoped<IScorecardsDashboardsReadinessMetadataRepository, MongoScorecardsDashboardsReadinessMetadataRepository>();

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
