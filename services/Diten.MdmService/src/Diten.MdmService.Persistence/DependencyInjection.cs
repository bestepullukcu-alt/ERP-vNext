using Diten.MdmService.Domain.Repositories;
using Diten.MdmService.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Diten.MdmService.Persistence;

public static class DependencyInjection
{
    private static bool _guidSerializerRegistered;

    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        RegisterGuidSerializer();

        var connectionString = configuration["Mongo:ConnectionString"]
            ?? throw new InvalidOperationException("Configuration error: 'Mongo:ConnectionString' is missing.");
        var databaseName = configuration["Mongo:DatabaseName"]
            ?? throw new InvalidOperationException("Configuration error: 'Mongo:DatabaseName' is missing.");

        // GUID consistency (matches Auth/Platform): the registered GuidSerializer(Standard) pins entity
        // serialization to subtype-4, but in driver 2.x (V2 mode) the QUERY-FILTER value is rendered using
        // the client-level GuidRepresentation. Leaving it unset defaults to legacy byte-order, so filters
        // built on TenantId/Id never match the Standard-encoded documents → reads come back empty even
        // though the data exists. Pinning the client to Standard makes write and query paths agree.
        var clientSettings = MongoClientSettings.FromConnectionString(connectionString);
        clientSettings.GuidRepresentation = GuidRepresentation.Standard;
        var client = new MongoClient(clientSettings);

        services.AddSingleton<IMongoClient>(client);
        services.AddScoped<IMongoDatabase>(_ => client.GetDatabase(databaseName));
        services.AddScoped<ILegalEntityRepository, LegalEntityRepository>();

        return services;
    }

    private static void RegisterGuidSerializer()
    {
        if (_guidSerializerRegistered)
        {
            return;
        }

        try
        {
            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        }
        catch (BsonSerializationException)
        {
            // Another service/test host may have already registered the serializer in-process.
        }

        _guidSerializerRegistered = true;
    }
}
