using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace Diten.PpmService.Persistence.Mongo;

internal static class PpmBsonConfiguration
{
    private static int _configured;

    public static void Configure()
    {
        if (Interlocked.Exchange(ref _configured, 1) != 0)
            return;

#pragma warning disable CS0618
        BsonDefaults.GuidRepresentationMode = GuidRepresentationMode.V3;
#pragma warning restore CS0618
        TryRegisterGuidSerializer();
        TryRegisterUtcDateTimeSerializer();

        var conventions = new ConventionPack
        {
            new EnumRepresentationConvention(BsonType.String),
            new IgnoreExtraElementsConvention(false)
        };
        ConventionRegistry.Register("Diten.PpmService", conventions, type =>
            type.Namespace?.StartsWith("Diten.PpmService", StringComparison.Ordinal) == true);
    }

    private static void TryRegisterUtcDateTimeSerializer()
    {
        try
        {
            BsonSerializer.RegisterSerializer(new DateTimeSerializer(DateTimeKind.Utc));
        }
        catch (BsonSerializationException)
        {
            var serializer = BsonSerializer.LookupSerializer<DateTime>();
            if (serializer is not DateTimeSerializer dateTimeSerializer ||
                dateTimeSerializer.Kind != DateTimeKind.Utc)
            {
                throw new InvalidOperationException(
                    "PPM requires scalar BSON DateTime values materialized with DateTimeKind.Utc.");
            }
        }
    }

    private static void TryRegisterGuidSerializer()
    {
        try
        {
            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        }
        catch (BsonSerializationException)
        {
            var serializer = BsonSerializer.LookupSerializer<Guid>();
            if (serializer is not GuidSerializer guidSerializer ||
                guidSerializer.GuidRepresentation != GuidRepresentation.Standard)
            {
                throw new InvalidOperationException(
                    "PPM requires BSON GuidRepresentation.Standard (subtype 4).");
            }
        }
    }
}
