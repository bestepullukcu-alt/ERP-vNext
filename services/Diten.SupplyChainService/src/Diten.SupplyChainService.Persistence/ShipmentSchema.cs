using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.Extensions.Hosting;
namespace Diten.SupplyChainService.Persistence;
public sealed class ShipmentSchema(IMongoDatabase db) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var hello = await db.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1), cancellationToken: ct);
        if (!hello.Contains("setName") && hello.GetValue("msg", "").AsString != "isdbgrid")
            throw new InvalidOperationException("MOD-0183 requires Mongo transaction support (replica set or mongos).");
        foreach (var name in new[] { "sce_shipments", "sce_shipment_receipts", "sce_shipment_history", "sce_shipment_audit", "sce_shipment_outbox", "sce_shipment_source_links", "sce_shipment_source_intents", "sce_shipment_source_evidence" })
        {
            var collection = db.GetCollection<BsonDocument>(name);
            await collection.Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
             new BsonDocument { { "TenantId", 1 }, { "LegalEntityId", 1 } }, new CreateIndexOptions { Name = "scope" }), cancellationToken: ct);
        }
        foreach (var name in new[] { "sce_shipment_source_links", "sce_shipment_source_intents" })
            await Index(name, new BsonDocument { { "TenantId", 1 }, { "LegalEntityId", 1 }, { "Key", 1 } }, "source_identity", ct);
        await db.GetCollection<BsonDocument>("sce_shipment_source_evidence").Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            new BsonDocument { { "TenantId", 1 }, { "LegalEntityId", 1 }, { "Key", 1 }, { "OccurredAt", -1 } },
            new CreateIndexOptions { Name = "source_evidence" }), cancellationToken: ct);
        await Index("sce_shipments", new BsonDocument { { "TenantId", 1 }, { "LegalEntityId", 1 }, { "ShipmentNumber", 1 } }, "shipment_number", ct);
        await Index("sce_shipment_receipts", new BsonDocument { { "TenantId", 1 }, { "LegalEntityId", 1 }, { "Operation", 1 }, { "Key", 1 } }, "replay", ct);
        await Index("sce_shipment_history", new BsonDocument { { "TenantId", 1 }, { "LegalEntityId", 1 }, { "ShipmentId", 1 }, { "Sequence", 1 } }, "history_sequence", ct);
        await db.GetCollection<BsonDocument>("sce_shipments").Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
         new BsonDocument { { "TenantId", 1 }, { "LegalEntityId", 1 }, { "Status", 1 }, { "PlannedDeliverAt", 1 } }), cancellationToken: ct);
        // Verify transactions really execute before accepting any traffic.
        using var session = await db.Client.StartSessionAsync(cancellationToken: ct);
        session.StartTransaction();
        try { await db.GetCollection<BsonDocument>("sce_shipments").Find(session, new BsonDocument("_id", "startup-transaction-probe")).FirstOrDefaultAsync(ct); }
        finally { await session.AbortTransactionAsync(ct); }
    }
    private Task Index(string name, BsonDocument keys, string label, CancellationToken ct) =>
     db.GetCollection<BsonDocument>(name).Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(keys, new CreateIndexOptions { Unique = true, Name = label }), cancellationToken: ct);
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
