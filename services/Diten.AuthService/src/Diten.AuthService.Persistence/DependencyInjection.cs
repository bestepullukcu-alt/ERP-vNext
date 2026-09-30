using Diten.BuildingBlocks.Security.Secrets;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Repositories;
using Diten.AuthService.Persistence.Configurations;
using Diten.AuthService.Persistence.Repositories;
using Diten.AuthService.Persistence.Seed;
using Diten.AuthService.Persistence.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddEntitlementReconciliationPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration.GetSection("MongoDbSettings").Get<MongoDbSettings>()
            ?? throw new InvalidOperationException("MONGO_SETTINGS_REQUIRED");
        if (string.IsNullOrWhiteSpace(settings.ConnectionString) || string.IsNullOrWhiteSpace(settings.DatabaseName))
            throw new InvalidOperationException("MONGO_SETTINGS_REQUIRED");
        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        var conventions = new ConventionPack { new IgnoreExtraElementsConvention(true) };
        ConventionRegistry.Register("EntitlementOperationalIgnoreExtraElements", conventions,
            type => type.Namespace?.StartsWith("Diten.AuthService.Domain", StringComparison.Ordinal) == true);
        var clientSettings = MongoClientSettings.FromConnectionString(settings.ConnectionString);
        clientSettings.GuidRepresentation = GuidRepresentation.Standard;
        clientSettings.ApplicationName = "Diten.AuthService.EntitlementReconciliation";
        clientSettings.RetryWrites = false;
        clientSettings.ReadPreference = ReadPreference.Primary;
        clientSettings.ReadConcern = ReadConcern.Majority;
        clientSettings.WriteConcern = WriteConcern.WMajority;
        var client = new MongoClient(clientSettings);
        services.AddSingleton<IMongoDatabase>(client.GetDatabase(settings.DatabaseName));
        services.AddSingleton<IEntitlementReconciliationOperationStore, EntitlementReconciliationOperationStore>();
        return services;
    }

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.ValidateRequiredSecrets(configuration, environment, "AuthService.Persistence", [
            new("MongoDbSettings:ConnectionString", "AuthService.Persistence", SecretRequirementKind.ConnectionString)
        ]);

        // MongoDB Serializers
        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

        // MongoDB Conventions - Globally ignore extra elements to prevent deserialization errors on schema changes
        var conventionPack = new ConventionPack
        {
            new IgnoreExtraElementsConvention(true)
        };
        ConventionRegistry.Register("IgnoreExtraElementsConvention", conventionPack, type => true);

        // Settings
        var mongoSection = configuration.GetSection("MongoDbSettings");
        if (!mongoSection.Exists())
            throw new InvalidOperationException("Configuration error: 'MongoDbSettings' section is missing in appsettings.json.");

        var mongoSettings = new MongoDbSettings();
        mongoSection.Bind(mongoSettings);

        if (string.IsNullOrWhiteSpace(mongoSettings.ConnectionString))
            throw new InvalidOperationException("Configuration error: 'MongoDbSettings:ConnectionString' is missing.");

        if (string.IsNullOrWhiteSpace(mongoSettings.DatabaseName))
            throw new InvalidOperationException("Configuration error: 'MongoDbSettings:DatabaseName' is missing.");

        services.AddSingleton(mongoSettings);

        // Drivers
        var clientSettings = MongoClientSettings.FromConnectionString(mongoSettings.ConnectionString);
        clientSettings.GuidRepresentation = GuidRepresentation.Standard;
        var client = new MongoClient(clientSettings);
        var database = client.GetDatabase(mongoSettings.DatabaseName);

        services.AddSingleton<IMongoClient>(client);
        services.AddSingleton<IMongoDatabase>(database);

        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();
        services.AddScoped<IRolePermissionRepository, RolePermissionRepository>();
        services.AddScoped<IRoleAssignmentVersionService, RoleAssignmentVersionRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<IPlatformUserRepository, PlatformUserRepository>();
        services.AddScoped<ITenantUserMembershipRepository, TenantUserMembershipRepository>();
        services.AddScoped<IIntegrationEventInboxRepository, IntegrationEventInboxRepository>();
        services.AddScoped<IAuthAuditService, AuthAuditService>();
        services.AddScoped<IMfaChallengeRepository, MfaChallengeRepository>();
        services.AddScoped<IServiceClientIdentityRepository, ServiceClientIdentityRepository>();
        services.AddScoped<IServiceClientTenantGrantRepository, ServiceClientTenantGrantRepository>();

        // Ensure Indexes and Seed Data
        // Note: In a production environment, this might be handled by an initialization service or migration tool.
        // For this project, we execute it during DI registration as requested.
        try
        {
            MongoDbIndexConfigurations.EnsureIndexesAsync(database).GetAwaiter().GetResult();
            DataSeeder.SeedAsync(database).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            // Log or handle initial connection errors
            Console.WriteLine($"MongoDB Initialization Error: {ex.Message}");
        }

        return services;
    }
}
