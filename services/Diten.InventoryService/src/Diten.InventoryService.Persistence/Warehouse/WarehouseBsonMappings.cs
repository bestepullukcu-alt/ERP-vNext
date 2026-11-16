using Diten.InventoryService.Domain.Warehouse.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace Diten.InventoryService.Persistence.Warehouse;

/// <summary>
/// The one MOD-0178 addition to the service's Mongo representation: a role SET (<c>List&lt;WarehouseRole&gt;</c>) is stored as role NAMES, like
/// every single Domain enum (<c>InventoryBsonMappings</c>, unchanged) — its enum convention reaches top-level enum members only, so without
/// this a list would be stored as numbers. Registered once per process (a second host in the same test process is a no-op).
/// </summary>
public static class WarehouseBsonMappings
{
    public static void Register()
        => BsonSerializer.TryRegisterSerializer(
            new EnumerableInterfaceImplementerSerializer<List<WarehouseRole>, WarehouseRole>(new EnumSerializer<WarehouseRole>(BsonType.String)));
}
