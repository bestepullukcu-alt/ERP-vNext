using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Diten.SupplyChainService.Domain.Features.Shipments;

namespace Diten.SupplyChainService.Persistence.Features.Shipments;

public static class ShipmentDetailMaterializer
{
    private const string RootField = "LifecycleCorrelationId";

    public static ShipmentDetailReadResult Materialize(BsonDocument raw)
    {
        var state = RawRootState.Missing;
        Guid? root = null;
        if (raw.TryGetValue(RootField, out var value))
        {
            if (value.IsBsonNull) state = RawRootState.ExplicitNull;
            else if (value.IsString && Guid.TryParse(value.AsString, out var parsed))
            {
                state = RawRootState.PresentStoredUuid;
                root = parsed;
            }
            else state = RawRootState.InvalidStoredValue;
        }

        // Deserialize the detached copy without the optional field. This preserves
        // legacy/malformed root bytes and prevents a typed Guid serializer from
        // converting an invalid value into a default or writing it back.
        var detached = raw.DeepClone().AsBsonDocument;
        detached.Remove(RootField);
        var shipment = BsonSerializer.Deserialize<Shipment>(detached);
        return new ShipmentDetailReadResult(shipment, state, root);
    }
}
