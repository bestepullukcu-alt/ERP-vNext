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

        // HCM feature repositories (PR-A2 group 1 — readiness leaves).
        services.AddScoped<IApplicantIntakeReadinessMetadataRepository, MongoApplicantIntakeReadinessMetadataRepository>();
        services.AddScoped<ICandidatePipelineReadinessMetadataRepository, MongoCandidatePipelineReadinessMetadataRepository>();
        services.AddScoped<ICompensationBenefitsReadinessMetadataRepository, MongoCompensationBenefitsReadinessMetadataRepository>();
        services.AddScoped<ICompetencySkillsReadinessMetadataRepository, MongoCompetencySkillsReadinessMetadataRepository>();
        services.AddScoped<IDevelopmentPlanReadinessMetadataRepository, MongoDevelopmentPlanReadinessMetadataRepository>();
        services.AddScoped<IEmployeeOnboardingReadinessMetadataRepository, MongoEmployeeOnboardingReadinessMetadataRepository>();

        // HCM feature repositories (PR-A2 group 2 — employment / HR ops leaves).
        services.AddScoped<IEmploymentChangeReadinessMetadataRepository, MongoEmploymentChangeReadinessMetadataRepository>();
        services.AddScoped<IHeadcountBudgetReadinessMetadataRepository, MongoHeadcountBudgetReadinessMetadataRepository>();
        services.AddScoped<IHrCaseManagementReadinessMetadataRepository, MongoHrCaseManagementReadinessMetadataRepository>();
        services.AddScoped<IHrComplianceReadinessMetadataRepository, MongoHrComplianceReadinessMetadataRepository>();
        services.AddScoped<IHrDocumentationReadinessMetadataRepository, MongoHrDocumentationReadinessMetadataRepository>();
        services.AddScoped<IHrKpiAnalyticsReadinessMetadataRepository, MongoHrKpiAnalyticsReadinessMetadataRepository>();

        // HCM feature repositories (PR-A2 group 3 — learning/offer/perf/self-service/succession/time/workforce leaves).
        services.AddScoped<ILearningTrainingReadinessMetadataRepository, MongoLearningTrainingReadinessMetadataRepository>();
        services.AddScoped<IOfferReadinessMetadataRepository, MongoOfferReadinessMetadataRepository>();
        services.AddScoped<IPerformanceReviewReadinessMetadataRepository, MongoPerformanceReviewReadinessMetadataRepository>();
        services.AddScoped<ISelfServiceReadinessMetadataRepository, MongoSelfServiceReadinessMetadataRepository>();
        services.AddScoped<ISuccessionReadinessMetadataRepository, MongoSuccessionReadinessMetadataRepository>();
        services.AddScoped<ITimeAttendanceLeaveReadinessMetadataRepository, MongoTimeAttendanceLeaveReadinessMetadataRepository>();
        services.AddScoped<IWorkforcePlanningReadinessMetadataRepository, MongoWorkforcePlanningReadinessMetadataRepository>();

        // HCM feature repositories (PR-A2 group 4 — seam-entangled cluster; local Mongo,
        // fail-closed until Ali wires EmployeeProfileProjection <- main HcmService feed).
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
