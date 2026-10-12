using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Diten.BuildingBlocks.Eventing;
using Diten.SupplyChainService.Domain.Features.Shipments;
using Diten.SupplyChainService.Persistence.Features.Shipments;
namespace Diten.SupplyChainService.Persistence;
public static class DependencyInjection
{
    private static readonly object Sync = new();
    private static bool _configured;
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        lock (Sync)
        {
            if (!_configured)
            {
                BsonSerializer.RegisterSerializer(new GuidSerializer(BsonType.String));
                BsonSerializer.RegisterSerializer(new DateTimeOffsetSerializer(BsonType.String));
                ConventionRegistry.Register("shipment-enums", new ConventionPack { new EnumRepresentationConvention(BsonType.String) }, t => t.Namespace?.StartsWith("Diten.SupplyChainService") == true);
                _configured = true;
            }
        }
        var connection = configuration["Mongo:ConnectionString"] ?? throw new InvalidOperationException("Mongo:ConnectionString is required.");
        var database = configuration["Mongo:DatabaseName"] ?? throw new InvalidOperationException("Mongo:DatabaseName is required.");
        services.AddSingleton<IMongoClient>(_ => new MongoClient(connection));
        services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(database));
        services.AddSingleton<IShipmentCommitProbe, NoOpShipmentCommitProbe>();
        services.AddScoped<Diten.SupplyChainService.Domain.Features.SourceIntake.ISourceIntakeStore, SourceIntakeStore>();
        services.AddScoped<IShipmentRepository, ShipmentRepository>(); services.AddSingleton<IEventOutboxStore, ShipmentOutboxStore>();
        services.AddHostedService<ShipmentSchema>(); return services;
    }
}
