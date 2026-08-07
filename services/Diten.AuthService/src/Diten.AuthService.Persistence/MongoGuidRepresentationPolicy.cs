using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence;

internal static class MongoGuidRepresentationPolicy
{
    internal const GuidRepresentation Canonical = GuidRepresentation.Standard;

    private static readonly object Gate = new();

    internal static void EnsureGlobalSerializer()
    {
        lock (Gate)
        {
            var serializer = BsonSerializer.LookupSerializer<Guid>();
            if (serializer is GuidSerializer guidSerializer)
            {
                if (guidSerializer.GuidRepresentation != Canonical)
                    throw new AuthUuidPolicyConflictException(guidSerializer.GuidRepresentation);
                return;
            }

            BsonSerializer.RegisterSerializer(new GuidSerializer(Canonical));
        }
    }

    internal static MongoClientSettings CreateClientSettings(string connectionString)
    {
        EnsureGlobalSerializer();
        var settings = MongoClientSettings.FromConnectionString(connectionString);
#pragma warning disable CS0618
        settings.GuidRepresentation = Canonical;
#pragma warning restore CS0618
        return settings;
    }

    internal static BsonBinaryData ToBson(Guid value) => new(value, Canonical);
}

internal sealed class AuthUuidPolicyConflictException(GuidRepresentation actual)
    : InvalidOperationException($"AuthService Mongo Guid policy requires {MongoGuidRepresentationPolicy.Canonical}, but {actual} is already registered.");
