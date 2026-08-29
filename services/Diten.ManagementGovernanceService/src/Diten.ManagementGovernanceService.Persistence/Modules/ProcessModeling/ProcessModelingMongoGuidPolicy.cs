using MongoDB.Bson;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Diten.ManagementGovernanceService.Persistence.Modules.ProcessModeling;

public static class ProcessModelingMongoGuidPolicy
{
    public const GuidRepresentation Canonical = GuidRepresentation.Standard;
    public static GuidSerializer Serializer { get; } = new(Canonical);
    public static MongoClientSettings ClientSettings(string connectionString)
    {
        var settings=MongoClientSettings.FromConnectionString(connectionString);
#pragma warning disable CS0618 // Driver 2.27 client-level policy is mandated by MOD-0355 §19.
        settings.GuidRepresentation=Canonical;
#pragma warning restore CS0618
        return settings;
    }

    public static bool IsCanonical(IMongoClient client)
    {
#pragma warning disable CS0618 // Driver 2.27 exposes the effective client policy only through this property.
        return client.Settings.GuidRepresentation==Canonical;
#pragma warning restore CS0618
    }

    public static BsonBinaryData Write(Guid value) => new(value, Canonical);
    public static Guid Read(BsonValue value) => value.AsBsonBinaryData.ToGuid(Canonical);
}
