using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Repositories;
using Diten.AuthService.Persistence.Repositories;
using Diten.AuthService.Persistence.Settings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence;

/// <summary>Isolated operational composition: no normal persistence registration, schema or seed execution.</summary>
public static class ServiceClientOperationalPersistenceRegistration
{
    public static IServiceCollection AddServiceClientOperationalPersistence(
        this IServiceCollection services, IConfiguration configuration)
    {
        var settings = new MongoDbSettings();
        configuration.GetSection("MongoDbSettings").Bind(settings);
        if (string.IsNullOrWhiteSpace(settings.ConnectionString) || string.IsNullOrWhiteSpace(settings.DatabaseName))
            throw new InvalidOperationException("Explicit operational persistence configuration is required.");

        BsonSerializer.TryRegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        ConventionRegistry.Register("ServiceClientOperationalIgnoreExtraElements",
            new ConventionPack { new IgnoreExtraElementsConvention(true) },
            type => type.Namespace == "Diten.AuthService.Domain.Entities");
        services.AddSingleton(settings);
        // Registration itself performs no database operations; only the isolated provider resolves this client.
        services.AddSingleton<IMongoClient>(_ =>
        {
            var clientSettings = MongoClientSettings.FromConnectionString(settings.ConnectionString);
            clientSettings.GuidRepresentation = GuidRepresentation.Standard;
            clientSettings.ReadPreference = ReadPreference.Primary;
            clientSettings.WriteConcern = WriteConcern.WMajority;
            clientSettings.RetryWrites = false;
            return new MongoClient(clientSettings);
        });
        services.AddSingleton<IMongoDatabase>(provider =>
            provider.GetRequiredService<IMongoClient>().GetDatabase(settings.DatabaseName));
        services.AddScoped<ITenantContext>(_ =>
        {
            var context = new TenantContext();
            context.SetPlatformContext(Guid.Parse("00000000-0000-0000-0000-000000000001"));
            return context;
        });
        // These existing repository constructors only obtain collection handles; authorization invokes reads only.
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IRolePermissionRepository, RolePermissionRepository>();
        services.AddScoped<IPermissionRepository, PermissionRepository>();
        services.AddScoped<IServiceClientIdentityRepository, ServiceClientIdentityRepository>();
        services.AddScoped<IServiceClientTenantGrantRepository, ServiceClientTenantGrantRepository>();
        services.AddScoped<IServiceClientOperationalProvisioningOperationRepository,
            ServiceClientOperationalProvisioningOperationRepository>();
        return services;
    }
}
