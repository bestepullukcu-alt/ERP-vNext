using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;

namespace Diten.InventoryService.Persistence;

/// <summary>
/// Mongo representation of the framework-free Domain (pack §10: Domain carries no Bson attributes). Registered once
/// per process — a second host in the same process (integration tests) must not re-register and throw.
/// <list type="bullet">
/// <item><see cref="Guid"/> → string (readable ids; tenant filters compare exact strings).</item>
/// <item><see cref="decimal"/> → Decimal128 (exact; <c>$inc</c> keeps scale; never double).</item>
/// <item><see cref="DateTimeOffset"/> → BSON date (UTC, range-queryable; every ledger time is UTC — pack §12).</item>
/// <item>Domain enums → their names as strings.</item>
/// </list>
/// </summary>
public static class InventoryBsonMappings
{
    private static readonly object Gate = new();
    private static bool _registered;

    public static void Register()
    {
        lock (Gate)
        {
            if (_registered)
            {
                return;
            }

            BsonSerializer.TryRegisterSerializer(new GuidSerializer(BsonType.String));
            BsonSerializer.TryRegisterSerializer(new DecimalSerializer(BsonType.Decimal128));
            BsonSerializer.TryRegisterSerializer(new DateTimeOffsetSerializer(BsonType.DateTime));

            ConventionRegistry.Register(
                "diten-inventory-domain",
                new ConventionPack
                {
                    new EnumRepresentationConvention(BsonType.String),
                    new IgnoreExtraElementsConvention(true)
                },
                type => type.Namespace?.StartsWith("Diten.InventoryService.Domain", StringComparison.Ordinal) == true);

            _registered = true;
        }
    }
}
