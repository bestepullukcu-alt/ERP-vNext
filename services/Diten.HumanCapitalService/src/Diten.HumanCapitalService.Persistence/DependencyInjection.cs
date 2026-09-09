using Diten.HumanCapitalService.Domain.Repositories;
using Diten.HumanCapitalService.Persistence.Migrations;
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
        // Force standard (subtype 4) GUID representation for Find-filter constants so read
        // filters match the subtype-4 GUIDs written for _id/TenantId. Without this, reads
        // (list/get/exists/evaluate/delete) silently fail to match written records.
        // Mirrors Auth/Platform/DevEnablement persistence configuration.
        mongoClientSettings.GuidRepresentation = GuidRepresentation.Standard;
        var mongoClient = new MongoClient(mongoClientSettings);
        var database = mongoClient.GetDatabase(mongoSettings.DatabaseName);

        services.AddSingleton(mongoSettings);
        services.AddSingleton<IMongoClient>(mongoClient);
        services.AddScoped<IMongoDatabase>(_ => database);
        services.AddScoped<IEmployeeProjectionRepository, MongoEmployeeProjectionRepository>();
        services.AddScoped<IPositionAssignmentOverlayRepository, MongoPositionAssignmentOverlayRepository>();
        services.AddScoped<IOffboardingCaseRepository, MongoOffboardingCaseRepository>();
        services.AddScoped<IApplicantIntakeReadinessMetadataRepository, MongoApplicantIntakeReadinessMetadataRepository>();
        services.AddScoped<ICandidatePipelineReadinessMetadataRepository, MongoCandidatePipelineReadinessMetadataRepository>();
        services.AddScoped<IOfferReadinessMetadataRepository, MongoOfferReadinessMetadataRepository>();
        services.AddScoped<IEmployeeOnboardingReadinessMetadataRepository, MongoEmployeeOnboardingReadinessMetadataRepository>();
        services.AddScoped<IEmploymentChangeReadinessMetadataRepository, MongoEmploymentChangeReadinessMetadataRepository>();
        services.AddScoped<IPerformanceReviewReadinessMetadataRepository, MongoPerformanceReviewReadinessMetadataRepository>();
        services.AddScoped<ICompetencySkillsReadinessMetadataRepository, MongoCompetencySkillsReadinessMetadataRepository>();
        services.AddScoped<IDevelopmentPlanReadinessMetadataRepository, MongoDevelopmentPlanReadinessMetadataRepository>();
        services.AddScoped<ILearningTrainingReadinessMetadataRepository, MongoLearningTrainingReadinessMetadataRepository>();
        services.AddScoped<ISuccessionReadinessMetadataRepository, MongoSuccessionReadinessMetadataRepository>();
        services.AddScoped<IWorkforcePlanningReadinessMetadataRepository, MongoWorkforcePlanningReadinessMetadataRepository>();
        services.AddScoped<IHeadcountBudgetReadinessMetadataRepository, MongoHeadcountBudgetReadinessMetadataRepository>();
        services.AddScoped<IHrKpiAnalyticsReadinessMetadataRepository, MongoHrKpiAnalyticsReadinessMetadataRepository>();
        services.AddScoped<IHrDocumentationReadinessMetadataRepository, MongoHrDocumentationReadinessMetadataRepository>();
        services.AddScoped<ITimeAttendanceLeaveReadinessMetadataRepository, MongoTimeAttendanceLeaveReadinessMetadataRepository>();
        services.AddScoped<ICompensationBenefitsReadinessMetadataRepository, MongoCompensationBenefitsReadinessMetadataRepository>();
        services.AddScoped<ISelfServiceReadinessMetadataRepository, MongoSelfServiceReadinessMetadataRepository>();
        services.AddScoped<IHrCaseManagementReadinessMetadataRepository, MongoHrCaseManagementReadinessMetadataRepository>();
        services.AddScoped<IHrComplianceReadinessMetadataRepository, MongoHrComplianceReadinessMetadataRepository>();

        // Legal-entity scoping rollout: one-shot startup backfill + unique-index reconciliation across
        // every scoped HCM collection.
        services.AddHostedService<HcmLegalEntityBackfillService>();

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
