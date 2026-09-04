using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace Diten.PpmService.Persistence.Mongo;

internal static class PpmBsonConfiguration
{
    private static readonly object ConfigurationLock = new();
    private static bool _configured;

    public static void Configure()
    {
        lock (ConfigurationLock)
        {
            if (_configured)
                return;

#pragma warning disable CS0618
            BsonDefaults.GuidRepresentationMode = GuidRepresentationMode.V3;
#pragma warning restore CS0618
            TryRegisterGuidSerializer();
            TryRegisterUtcDateTimeSerializer();
            TryRegisterDateOnlySerializer();

            var conventions = new ConventionPack
            {
                new EnumRepresentationConvention(BsonType.String),
                new IgnoreExtraElementsConvention(false)
            };
            ConventionRegistry.Register("Diten.PpmService", conventions, type =>
                type.Namespace?.StartsWith("Diten.PpmService", StringComparison.Ordinal) == true);

            _configured = true;
        }
    }

    private static void TryRegisterDateOnlySerializer()
    {
        try { BsonSerializer.RegisterSerializer(new DateOnlyIsoSerializer()); }
        catch (BsonSerializationException)
        {
            if (BsonSerializer.LookupSerializer<DateOnly>() is not DateOnlyIsoSerializer)
                throw new InvalidOperationException("PPM requires ISO yyyy-MM-dd BSON string DateOnly values.");
        }
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

internal sealed class DateOnlyIsoSerializer : SerializerBase<DateOnly>
{
    public override DateOnly Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        if (context.Reader.GetCurrentBsonType() != BsonType.String)
            throw new BsonSerializationException("DateOnly must be a BSON string.");
        var value = context.Reader.ReadString();
        return DateOnly.TryParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out var parsed)
            ? parsed
            : throw new BsonSerializationException("DateOnly must use yyyy-MM-dd.");
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, DateOnly value) =>
        context.Writer.WriteString(value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
}
