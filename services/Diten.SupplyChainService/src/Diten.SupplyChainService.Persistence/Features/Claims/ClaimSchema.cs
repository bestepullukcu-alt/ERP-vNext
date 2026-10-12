using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.SupplyChainService.Persistence.Features.Claims;

public sealed class ClaimSchema(IMongoDatabase database) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var hello = await database.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1), cancellationToken: cancellationToken);
        if (!hello.Contains("setName") && hello.GetValue("msg", "").AsString != "isdbgrid")
            throw new InvalidOperationException("Claims require replica-set transactions.");
        foreach (var name in new[] { "claims", "claims_receipts", "claims_audit", "claims_outbox" })
            await Index(name, "claim_scope", false, cancellationToken);
        await Index("claims", "claim_number", true, cancellationToken, "ClaimNumber");
        await Index("claims_receipts", "claim_receipt", true, cancellationToken, "Operation", "TargetId", "IdempotencyKey");
        await Index("claims_outbox", "claim_event", true, cancellationToken, "EventId");
        using var session = await database.Client.StartSessionAsync(cancellationToken: cancellationToken);
        session.StartTransaction();
        try
        {
            await database.GetCollection<BsonDocument>("claims").Find(session, new BsonDocument("TenantId", Guid.Empty.ToString())).FirstOrDefaultAsync(cancellationToken);
        }
        finally { await session.AbortTransactionAsync(cancellationToken); }
    }

    private Task<string> Index(string collection, string name, bool unique, CancellationToken cancellationToken, params string[] tail)
    {
        var keys = new BsonDocument { { "TenantId", 1 }, { "LegalEntityId", 1 } };
        foreach (var field in tail) keys.Add(field, 1);
        return database.GetCollection<BsonDocument>(collection).Indexes.CreateOneAsync(
            new CreateIndexModel<BsonDocument>(keys, new CreateIndexOptions { Name = name, Unique = unique, Collation = Collation.Simple }), cancellationToken: cancellationToken);
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
