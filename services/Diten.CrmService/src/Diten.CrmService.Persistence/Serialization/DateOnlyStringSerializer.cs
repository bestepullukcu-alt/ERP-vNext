using System.Globalization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Diten.CrmService.Persistence.Serialization;

/// <summary>
/// Serializes a <see cref="DateOnly"/> as an ISO <c>"yyyy-MM-dd"</c> BSON string. The MongoDB C# driver 2.27 has no
/// built-in DateOnly serializer, and this is exactly the representation MOD-0155 FU01 needs for
/// <c>PlannedVisit.PlannedDate</c>: a plain, lexicographically-sortable scalar that can be indexed and range-filtered
/// without the DateTimeOffset "cannot sort with keys that are parallel arrays" 500 the whole DateOnly choice exists to
/// avoid. A null/absent element deserializes to <see cref="DateOnly.MinValue"/>.
/// </summary>
public sealed class DateOnlyStringSerializer : SerializerBase<DateOnly>
{
    private const string Format = "yyyy-MM-dd";

    public override DateOnly Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var reader = context.Reader;
        switch (reader.CurrentBsonType)
        {
            case BsonType.String:
            {
                var value = reader.ReadString();
                return DateOnly.TryParseExact(value, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                    ? date
                    : DateOnly.Parse(value, CultureInfo.InvariantCulture);
            }
            case BsonType.Null:
                reader.ReadNull();
                return DateOnly.MinValue;
            case BsonType.DateTime:
            {
                // Tolerate a legacy row written as a BSON DateTime (UTC ms since epoch).
                var ms = reader.ReadDateTime();
                var dt = DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
                return DateOnly.FromDateTime(dt);
            }
            default:
                throw new FormatException(
                    $"Cannot deserialize a DateOnly from BSON type {reader.CurrentBsonType}.");
        }
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, DateOnly value)
        => context.Writer.WriteString(value.ToString(Format, CultureInfo.InvariantCulture));
}
