using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.Extensions.Hosting;
namespace Diten.SupplyChainService.Persistence.Features.Carriers;
public sealed class CarrierSchema(IMongoDatabase db) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var hello = await db.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1), cancellationToken: ct);
        if (!hello.Contains("setName") && hello.GetValue("msg", "").AsString != "isdbgrid")
            throw new InvalidOperationException("Carrier persistence requires transactions.");
        foreach (var name in new[] { "carriers", "carrier_idempotency", "carrier_audit" })
            await db.GetCollection<BsonDocument>(name).Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
                new BsonDocument { { "TenantId", 1 }, { "LegalEntityId", 1 } }, new CreateIndexOptions { Name = "scope", Collation = Collation.Simple }), cancellationToken: ct);
        await db.GetCollection<BsonDocument>("carriers").Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            new BsonDocument { { "TenantId", 1 }, { "LegalEntityId", 1 }, { "CarrierCode", 1 } },
            new CreateIndexOptions { Name = "carrier_code", Unique = true, Collation = Collation.Simple }), cancellationToken: ct);
        await db.GetCollection<BsonDocument>("carrier_idempotency").Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(
            new BsonDocument { { "TenantId", 1 }, { "LegalEntityId", 1 }, { "Operation", 1 }, { "TargetId", 1 }, { "IdempotencyKey", 1 } },
            new CreateIndexOptions { Name = "carrier_replay", Unique = true, Collation = Collation.Simple }), cancellationToken: ct);
        using var session = await db.Client.StartSessionAsync(cancellationToken: ct);
        session.StartTransaction();
        try { await db.GetCollection<BsonDocument>("carriers").Find(session, new BsonDocument { { "TenantId", Guid.Empty.ToString() }, { "LegalEntityId", Guid.Empty.ToString() } }).FirstOrDefaultAsync(ct); }
        finally { await session.AbortTransactionAsync(ct); }
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
