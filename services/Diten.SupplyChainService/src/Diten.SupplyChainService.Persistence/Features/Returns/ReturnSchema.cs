using MongoDB.Bson;
using MongoDB.Driver;
using Microsoft.Extensions.Hosting;
namespace Diten.SupplyChainService.Persistence.Features.Returns;
public sealed class ReturnSchema(IMongoDatabase database) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        var hello = await database.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1), cancellationToken: ct);
        if (!hello.Contains("setName") && hello.GetValue("msg", "").AsString != "isdbgrid")
            throw new InvalidOperationException("Returns require replica-set transactions.");
        foreach (var name in new[] { "returns", "return_entitlements", "returns_receipts", "returns_audit", "returns_outbox" })
            await Index(name, "return_scope", false, ct);
        await Index("returns", "return_number", true, ct, "RmaNumber");
        await Index("return_entitlements", "return_entitlement", true, ct, "ShipmentId", "LineNumber");
        await Index("returns_receipts", "return_receipt", true, ct, "Operation", "TargetId", "IdempotencyKey");
        await Index("returns_outbox", "return_event", true, ct, "EventId");
        using var session = await database.Client.StartSessionAsync(cancellationToken: ct);
        session.StartTransaction();
        try
        {
            await database.GetCollection<BsonDocument>("returns").Find(session,
                new BsonDocument { { "TenantId", Guid.Empty.ToString() }, { "LegalEntityId", Guid.Empty.ToString() } }).FirstOrDefaultAsync(ct);
        }
        finally { await session.AbortTransactionAsync(ct); }
    }
    private Task<string> Index(string collection, string name, bool unique, CancellationToken ct, params string[] tail)
    {
        var keys = new BsonDocument { { "TenantId", 1 }, { "LegalEntityId", 1 } };
        foreach (var field in tail) keys.Add(field, 1);
        return database.GetCollection<BsonDocument>(collection).Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(keys, new CreateIndexOptions
            { Name = name, Unique = unique, Collation = Collation.Simple }), cancellationToken: ct);
    }
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
