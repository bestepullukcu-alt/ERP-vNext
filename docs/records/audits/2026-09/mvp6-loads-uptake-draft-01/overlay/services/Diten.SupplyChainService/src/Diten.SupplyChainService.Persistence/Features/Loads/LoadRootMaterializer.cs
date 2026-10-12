using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Diten.SupplyChainService.Application.Features.Loads;
using Diten.SupplyChainService.Domain.Features.Loads;

namespace Diten.SupplyChainService.Persistence.Features.Loads;

public static class LoadRootMaterializer
{
    private const string RootField = nameof(LoadPlan.CorrelationRoot);

    public static LoadReadResult Materialize(BsonDocument raw)
    {
        var state = LoadRootState.Missing;
        Guid? root = null;
        if (raw.TryGetValue(RootField, out var value))
        {
            if (value.IsBsonNull) state = LoadRootState.ExplicitNull;
            // Same lexical rule as the Loads request context: ASCII hex 8-4-4-4-12, either case, no trim.
            else if (value.IsString && LoadWire.Uuid(value.AsString) && Guid.TryParseExact(value.AsString, "D", out var parsed))
            {
                state = LoadRootState.PresentStoredUuid;
                root = parsed;
            }
            else state = LoadRootState.InvalidStoredValue;
        }

        // Deserialize a detached copy without the root field. The raw document is never mutated or
        // written back, and a typed Guid serializer can neither synthesize a nil root for a missing
        // field nor fail the whole read on a null or malformed one.
        var detached = raw.DeepClone().AsBsonDocument;
        detached.Remove(RootField);
        var load = BsonSerializer.Deserialize<LoadPlan>(detached);
        return new LoadReadResult(load, state, root);
    }
}
