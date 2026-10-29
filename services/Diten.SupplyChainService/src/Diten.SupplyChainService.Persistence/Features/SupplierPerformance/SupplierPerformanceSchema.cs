using MongoDB.Driver;
using MongoDB.Bson;
using Microsoft.Extensions.Hosting;
namespace Diten.SupplyChainService.Persistence.Features.SupplierPerformance;
/// <summary>
/// MOD-0147 DL-1 collections and their tenant-first indexes. Two unique indexes carry the §0.5 / §0.2 guarantees: the receipt
/// tuple (concurrent identical creates: one result, the rest replay) and one active evaluation per Tenant + LE + canonical
/// supplier + period (partial on IsActivePeriod, so Status is not in the key and DRAFT and PUBLISHED cannot coexist).
/// </summary>
public sealed class SupplierPerformanceSchema(IMongoDatabase db) : IHostedService
{
    public const string Evaluations = "supplier_evaluations";
    public const string Receipts = "supplier_performance_receipts";
    public const string ActivePeriodIndex = "supplier_evaluation_active_period";
    public const string ReceiptIndex = "supplier_performance_receipt";

    public async Task StartAsync(CancellationToken ct)
    {
        var hello = await db.RunCommandAsync<BsonDocument>(new BsonDocument("hello", 1), cancellationToken: ct);
        if (!hello.Contains("setName") && hello.GetValue("msg", "").AsString != "isdbgrid") throw new InvalidOperationException("Supplier performance requires replica-set transactions.");
        foreach (var n in new[] { Evaluations, Receipts }) await Index(n, "scope", false, null, ct);
        await Index(Evaluations, ActivePeriodIndex, true, new BsonDocument("IsActivePeriod", true), ct, "SupplierId", "PeriodStart", "PeriodEnd");
        await Index(Evaluations, "supplier_scorecard", false, null, ct, "ScorecardId");
        await Index(Receipts, ReceiptIndex, true, null, ct, "Operation", "TargetId", "IdempotencyKey");
        // Create both collections outside a transaction before the first command's transaction would create them implicitly.
        using var session = await db.Client.StartSessionAsync(cancellationToken: ct); session.StartTransaction();
        try { await db.GetCollection<BsonDocument>(Evaluations).Find(session, new BsonDocument { { "TenantId", Guid.Empty.ToString() }, { "LegalEntityId", Guid.Empty.ToString() } }).FirstOrDefaultAsync(ct); }
        finally { await session.AbortTransactionAsync(ct); }
    }

    private Task<string> Index(string collection, string name, bool unique, BsonDocument? partial, CancellationToken ct, params string[] tail)
    {
        var keys = new BsonDocument { { "TenantId", 1 }, { "LegalEntityId", 1 } };
        foreach (var field in tail) keys.Add(field, 1);
        var options = new CreateIndexOptions<BsonDocument> { Name = name, Unique = unique, Collation = Collation.Simple };
        if (partial is not null) options.PartialFilterExpression = partial;
        return db.GetCollection<BsonDocument>(collection).Indexes.CreateOneAsync(new CreateIndexModel<BsonDocument>(keys, options), cancellationToken: ct);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
