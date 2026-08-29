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
            try
            {
                BsonSerializer.RegisterSerializer(new GuidSerializer(Canonical));
                return;
            }
            catch (BsonSerializationException)
            {
                var serializer = BsonSerializer.LookupSerializer<Guid>();
                if (serializer is GuidSerializer guidSerializer &&
                    guidSerializer.GuidRepresentation is Canonical or GuidRepresentation.Unspecified)
                    return;

                throw new AuthUuidPolicyConflictException(
                    serializer is GuidSerializer existing
                        ? existing.GuidRepresentation
                        : GuidRepresentation.Unspecified);
            }
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
}

internal sealed class AuthUuidPolicyConflictException(GuidRepresentation actual)
    : InvalidOperationException(
        $"AuthService Mongo Guid policy requires {MongoGuidRepresentationPolicy.Canonical}, but {actual} is already registered.");
