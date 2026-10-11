using System.Text.Json;
using MongoDB.Bson;
using MongoDB.Driver;
using Diten.SupplyChainService.Domain.Features.Shipments;
using Diten.SupplyChainService.Domain.Features.SourceIntake;
namespace Diten.SupplyChainService.Persistence.Features.Shipments;

public sealed class SourceIntakeStore(IMongoDatabase db) : ISourceIntakeStore
{
    private static BsonDocument Scope(ShipmentScope scope)
    {
        if (scope.TenantId == Guid.Empty || scope.LegalEntityId == Guid.Empty || scope.ActorId == Guid.Empty)
            throw new InvalidOperationException("Validated scope required.");
        return new BsonDocument { { "TenantId", scope.TenantId.ToString() }, { "LegalEntityId", scope.LegalEntityId.ToString() } };
    }
    public async Task<SourceIntent?> FindAsync(ShipmentScope scope, string key, CancellationToken ct)
    {
        var filter = Scope(scope); filter["Key"] = key; filter["IsDeleted"] = false;
        var row = await db.GetCollection<BsonDocument>("sce_shipment_source_intents").Find(filter).FirstOrDefaultAsync(ct);
        return row is null ? null : JsonSerializer.Deserialize<SourceIntent>(row["IntentJson"].AsString)! with { State = row["State"].AsString };
    }
    public async Task<SourceIntent> PrepareAsync(ShipmentScope scope, SourceIntent candidate, CancellationToken ct)
    {
        var filter = Scope(scope); filter["Key"] = candidate.Key; filter["IsDeleted"] = false;
        if (candidate.Shipment.TenantId != scope.TenantId || candidate.Shipment.LegalEntityId != scope.LegalEntityId)
            throw new InvalidOperationException("Source scope mismatch.");
        var collection = db.GetCollection<BsonDocument>("sce_shipment_source_intents").WithWriteConcern(WriteConcern.WMajority);
        var row = filter.DeepClone().AsBsonDocument;
        row["_id"] = candidate.Key; row["IntentJson"] = JsonSerializer.Serialize(candidate); row["State"] = "Pending";
        row["IsDeleted"] = false; row["CreatedAt"] = candidate.Shipment.CreatedAt.UtcDateTime;
        row["CreatedBy"] = scope.ActorId.ToString();
        try { await collection.UpdateOneAsync(filter, new BsonDocument("$setOnInsert", row), new UpdateOptions { IsUpsert = true }, ct); }
        catch (MongoWriteException e) when (e.WriteError.Category == ServerErrorCategory.DuplicateKey) { }
        var stored = await collection.Find(filter).FirstAsync(ct);
        return JsonSerializer.Deserialize<SourceIntent>(stored["IntentJson"].AsString)! with { State = stored["State"].AsString };
    }
    public Task RecordEvidenceAsync(ShipmentScope scope, string key, string outboundId, string state, string evidence, CancellationToken ct)
    {
        var row = Scope(scope); row["_id"] = Guid.NewGuid().ToString(); row["SourceDocumentId"] = outboundId;
        row["Key"] = key;
        row["SourceModule"] = "MOD-0178"; row["SourceType"] = "WAREHOUSE_OUTBOUND"; row["State"] = state;
        row["Evidence"] = evidence; row["ActorId"] = scope.ActorId.ToString(); row["OccurredAt"] = DateTime.UtcNow;
        row["IsDeleted"] = false; row["CreatedAt"] = DateTime.UtcNow; row["CreatedBy"] = scope.ActorId.ToString();
        return db.GetCollection<BsonDocument>("sce_shipment_source_evidence").WithWriteConcern(WriteConcern.WMajority).InsertOneAsync(row, cancellationToken: ct);
    }
}
