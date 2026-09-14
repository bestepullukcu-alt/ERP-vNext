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

        // TEP feature repositories (PR-B2 group 1 — coupled cluster).
        services.AddScoped<ITepConsentVisibilityPolicyRepository, MongoTepConsentVisibilityPolicyRepository>();
        services.AddScoped<ITepAssociationMembershipRegistryRepository, MongoTepAssociationMembershipRegistryRepository>();
        services.AddScoped<ITepVerifiedParticipantAccessRepository, MongoTepVerifiedParticipantAccessRepository>();
        services.AddScoped<ITepReviewBoardCaseMetadataRepository, MongoTepReviewBoardCaseMetadataRepository>();
        services.AddScoped<ITepTrustLevelPolicyMetadataRepository, MongoTepTrustLevelPolicyMetadataRepository>();
        services.AddScoped<ITepCandidateProfileMetadataRepository, MongoTepCandidateProfileMetadataRepository>();

        // TEP feature repositories (PR-B2 group 2 — ExitReferenceRecords + leaves).
        services.AddScoped<ITepExitReferenceRecordMetadataRepository, MongoTepExitReferenceRecordMetadataRepository>();
        services.AddScoped<IAssociationOperationsReadinessMetadataRepository, MongoAssociationOperationsReadinessMetadataRepository>();
        services.AddScoped<ICandidateCareerPassportReadinessMetadataRepository, MongoCandidateCareerPassportReadinessMetadataRepository>();
        services.AddScoped<ITepCandidateDisputeReadinessMetadataRepository, MongoTepCandidateDisputeReadinessMetadataRepository>();
        services.AddScoped<IEarlyWarningSignalsReadinessMetadataRepository, MongoEarlyWarningSignalsReadinessMetadataRepository>();
        services.AddScoped<IHiringRiskIndicatorsReadinessMetadataRepository, MongoHiringRiskIndicatorsReadinessMetadataRepository>();

        // TEP feature repositories (PR-B2 group 3 — industry/mentorship/pay leaves).
        services.AddScoped<IIndustryKnowledgeNetworkReadinessMetadataRepository, MongoIndustryKnowledgeNetworkReadinessMetadataRepository>();
        services.AddScoped<IIndustrySkillPassportReadinessMetadataRepository, MongoIndustrySkillPassportReadinessMetadataRepository>();
        services.AddScoped<IIndustrySuccessionPoolReadinessMetadataRepository, MongoIndustrySuccessionPoolReadinessMetadataRepository>();
        services.AddScoped<IIndustryTalentPoolReadinessMetadataRepository, MongoIndustryTalentPoolReadinessMetadataRepository>();
        services.AddScoped<IMentorshipRecommendationNetworkReadinessMetadataRepository, MongoMentorshipRecommendationNetworkReadinessMetadataRepository>();
        services.AddScoped<IPayBenchmarkingReadinessMetadataRepository, MongoPayBenchmarkingReadinessMetadataRepository>();

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
