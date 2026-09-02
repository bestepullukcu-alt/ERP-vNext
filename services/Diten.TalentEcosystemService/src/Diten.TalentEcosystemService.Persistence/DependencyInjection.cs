using Diten.TalentEcosystemService.Domain.Repositories;
using Diten.TalentEcosystemService.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence;

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
            ?? "DitenTalentEcosystem";

        var client = new MongoClient(MongoClientSettings.FromConnectionString(connectionString));
        var database = client.GetDatabase(databaseName);

        services.AddSingleton<IMongoClient>(client);
        services.AddScoped<IMongoDatabase>(_ => database);
        services.AddScoped<ITepShellMetadataRepository, MongoTepShellMetadataRepository>();
        services.AddScoped<ITepConsentVisibilityPolicyRepository, MongoTepConsentVisibilityPolicyRepository>();
        services.AddScoped<ITepAssociationMembershipRegistryRepository, MongoTepAssociationMembershipRegistryRepository>();
        services.AddScoped<ITepVerifiedParticipantAccessRepository, MongoTepVerifiedParticipantAccessRepository>();
        services.AddScoped<ITepReviewBoardCaseMetadataRepository, MongoTepReviewBoardCaseMetadataRepository>();
        services.AddScoped<ITepTrustLevelPolicyMetadataRepository, MongoTepTrustLevelPolicyMetadataRepository>();
        services.AddScoped<ITepCandidateProfileMetadataRepository, MongoTepCandidateProfileMetadataRepository>();
        services.AddScoped<ITepExitReferenceRecordMetadataRepository, MongoTepExitReferenceRecordMetadataRepository>();
        services.AddScoped<ITepReferenceExchangeMarketplaceReadinessMetadataRepository, MongoTepReferenceExchangeMarketplaceReadinessMetadataRepository>();
        services.AddScoped<ITepRehireRecommendationReadinessMetadataRepository, MongoTepRehireRecommendationReadinessMetadataRepository>();
        services.AddScoped<ITepCandidateDisputeReadinessMetadataRepository, MongoTepCandidateDisputeReadinessMetadataRepository>();

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
