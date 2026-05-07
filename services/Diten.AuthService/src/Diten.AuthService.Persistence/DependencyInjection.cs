using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Persistence.Configurations;
using Diten.AuthService.Persistence.Repositories;
using Diten.AuthService.Persistence.Seed;
using Diten.AuthService.Persistence.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        // MongoDB Serializers
        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

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
