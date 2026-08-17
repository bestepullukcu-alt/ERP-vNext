using Diten.BuildingBlocks.Security.Secrets;
using Diten.DevEnablementService.Application.Common;
using Diten.DevEnablementService.Application.Interfaces;
using Diten.DevEnablementService.Domain.Repositories;
using Diten.DevEnablementService.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Diten.DevEnablementService.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.ValidateRequiredSecrets(configuration, environment, "DevEnablement.Persistence", [
            new("Mongo:ConnectionString", "DevEnablement.Persistence", SecretRequirementKind.ConnectionString)
        ]);

        // Guid'leri string olarak sakla (okunabilirlik + cross-platform)
        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));

        var connectionString = configuration["Mongo:ConnectionString"]
            ?? throw new InvalidOperationException("Configuration error: 'Mongo:ConnectionString' is missing in appsettings.json.");
        var databaseName = configuration["Mongo:DatabaseName"]
            ?? throw new InvalidOperationException("Configuration error: 'Mongo:DatabaseName' is missing in appsettings.json.");

        var clientSettings = MongoClientSettings.FromConnectionString(connectionString);
        clientSettings.GuidRepresentation = GuidRepresentation.Standard;
        var client = new MongoClient(clientSettings);
        services.AddSingleton<IMongoClient>(client);
        services.AddScoped<IMongoDatabase>(_ => client.GetDatabase(databaseName));

        services.AddScoped<IGoldenReferenceSlimRepository, GoldenReferenceSlimRepository>();
        services.AddScoped<IGoldenReferenceCompactRepository, GoldenReferenceCompactRepository>();
        services.AddScoped<IModuleSeedDataInitializer, NoOpModuleSeedDataInitializer>();

        return services;
    }
}
