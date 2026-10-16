using Diten.BuildingBlocks.Security.Secrets;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Repositories;
using Diten.AuthService.Persistence.Configurations;
using Diten.AuthService.Persistence.Repositories;
using Diten.AuthService.Persistence.Seed;
using Diten.AuthService.Persistence.Serialization;
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
        // WP-INFRA-AUTH-ACCOUNT-KIND-01 (C1/C2) — the registration is process-global and THROWS once a Guid
        // serializer has been registered or merely looked up. In production that never happens (one host, one
        // startup, this line runs first). In a shared test process that hosts the real Api in-process
        // (WebApplicationFactory) after some EARLIER, unrelated Guid-touching operation has already caused the
        // driver's own auto-registration — measured: taking the whole AuthService host down over it broke the
        // Auth suite far beyond this WP's scope. GuidSerializerRegistration.EnsureStandard() distinguishes a
        // second COMPATIBLE registration (silent no-op) from an INCOMPATIBLE one (a loud Console.Error warning,
        // not a throw — see its own remarks for the measured reason).
        GuidSerializerRegistration.EnsureStandard();

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
        // BL-521 FIX6 item 13 (D') — the user repository every handler gets hides platform accounts from a tenant actor
        // (see PlatformAccountScopedUserRepository); the Mongo implementation underneath is unchanged.
        services.AddScoped<UserRepository>();
        services.AddScoped<IUserRepository, PlatformAccountScopedUserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();
        services.AddScoped<IUserListReader, UserListReader>();
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
        services.AddScoped<IESignReauthTicketRepository, ESignReauthTicketRepository>();
        services.AddScoped<IESignReauthWindowRepository, ESignReauthWindowRepository>();

        // Ensure Indexes and Seed Data
        // Note: In a production environment, this might be handled by an initialization service or migration tool.
        // For this project, we execute it during DI registration as requested.
        try
        {
            MongoDbIndexConfigurations.EnsureIndexesAsync(database).GetAwaiter().GetResult();
            // Mock users only when BOTH hold: Development AND the opt-in key (see DataSeeder.MockUsersOptInConfigurationKey).
            var seedMockUsers = environment.IsDevelopment()
                && configuration.GetValue<bool>(DataSeeder.MockUsersOptInConfigurationKey);
            // The dev tenant administrator under the same gate (DataSeeder.TenantAdminOptInConfigurationKey, BL-521 follow-up).
            var seedTenantAdmin = environment.IsDevelopment()
                && configuration.GetValue<bool>(DataSeeder.TenantAdminOptInConfigurationKey);
            DataSeeder.SeedAsync(database, seedMockUsers, seedTenantAdmin).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            // Log or handle initial connection errors
            Console.WriteLine($"MongoDB Initialization Error: {ex.Message}");
        }

        return services;
    }
}
