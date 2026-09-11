using Diten.BuildingBlocks.Security.Secrets;
using Diten.AuthService.Application.Common.Interfaces;
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
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.ValidateRequiredSecrets(configuration, environment, "AuthService.Persistence", [
            new("MongoDbSettings:ConnectionString", "AuthService.Persistence", SecretRequirementKind.ConnectionString)
        ]);

        // MongoDB Serializers
        // WP-INFRA-AUTH-ACCOUNT-KIND-01 — the registration is process-global and THROWS once a Guid serializer has been
        // registered or merely looked up. In production that never happens (one host, one startup, this line runs
        // first). In a test process that hosts the real Api in-process (WebApplicationFactory) AFTER another test has
        // already touched Guid serialization, the throw took the whole AuthService host down at Program.cs:73 —
        // measured: 20 acceptance tests red in the full run, green in isolation. (TryRegisterSerializer is not enough:
        // it still throws when the cached serializer is a DIFFERENT instance, which the driver's own default is.)
        // A fresh process keeps the exact production registration; a warm one keeps the serializer it already has —
        // equivalent on the wire here, because in the driver's V2 mode the representation is decided by the writer.
        try
        {
            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        }
        catch (BsonSerializationException)
        {
            // Already registered/looked up earlier in this process (in-process test host); the first registration stands.
        }

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
