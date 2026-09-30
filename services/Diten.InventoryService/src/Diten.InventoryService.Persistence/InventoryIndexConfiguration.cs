using Diten.InventoryService.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence;

/// <summary>
/// MOD-0173 indexes (pack §4), tenant-first (mongo-indexing.md DB-001). Only this service's five collections — it is
/// not the platform schema. The unique indexes are load-bearing, not performance hints:
/// <list type="bullet">
/// <item><c>ux_tenant_le_idempotencykey</c> — a key commits once per Tenant + LE even under a race (pack §8).</item>
/// <item><c>ux_tenant_le_reversestransactionid</c> — an original can be reversed once, even under a race.</item>
/// <item><c>ux_balance_dimension</c> — exactly one projection row per dimension key.</item>
/// </list>
/// </summary>
public static class InventoryIndexConfiguration
{
    public const string IdempotencyIndex = "ux_tenant_le_idempotencykey";
    public const string TransactionNumberIndex = "ux_tenant_le_transactionnumber";
    public const string ReversalIndex = "ux_tenant_le_reversestransactionid";
    public const string BalanceDimensionIndex = "ux_balance_dimension";

    public static IReadOnlyList<CreateIndexModel<InventoryTransaction>> BuildTransactions()
    {
        var b = Builders<InventoryTransaction>.IndexKeys;
        return
        [
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions { Name = IdempotencyIndex, Unique = true }),
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.TransactionNumber),
                new CreateIndexOptions { Name = TransactionNumberIndex, Unique = true }),
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.ItemId).Ascending(x => x.PostingDate),
                new CreateIndexOptions { Name = "ix_tenant_le_item_postingdate" }),
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.LotId),
                new CreateIndexOptions { Name = "ix_tenant_le_lot" }),
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.ToLocationId),
                new CreateIndexOptions { Name = "ix_tenant_le_tolocation" }),
            // Additive to pack §4: GET /transactions?locationId matches either side, so the source side is indexed too.
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.FromLocationId),
                new CreateIndexOptions { Name = "ix_tenant_le_fromlocation" }),
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.ReversesTransactionId),
                new CreateIndexOptions<InventoryTransaction>
                {
                    Name = ReversalIndex,
                    Unique = true,
                    PartialFilterExpression = new BsonDocument(nameof(InventoryTransaction.ReversesTransactionId),
                        new BsonDocument("$type", "string"))
                })
        ];
    }

    public static IReadOnlyList<CreateIndexModel<InventoryBalance>> BuildBalances()
    {
        var b = Builders<InventoryBalance>.IndexKeys;
        return
        [
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId)
                    .Ascending(x => x.ItemId).Ascending(x => x.SkuId).Ascending(x => x.SkuLevel)
                    .Ascending(x => x.WarehouseId).Ascending(x => x.LocationId).Ascending(x => x.LotId)
                    .Ascending(x => x.StockStatus).Ascending(x => x.OwnerId).Ascending(x => x.ProjectId),
                new CreateIndexOptions { Name = BalanceDimensionIndex, Unique = true }),
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.ItemId).Ascending(x => x.WarehouseId),
                new CreateIndexOptions { Name = "ix_tenant_le_item_warehouse" })
        ];
    }

    public static IReadOnlyList<CreateIndexModel<InventoryAuditRecord>> BuildAudit()
    {
        var b = Builders<InventoryAuditRecord>.IndexKeys;
        return
        [
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.TransactionId),
                new CreateIndexOptions { Name = "ix_tenant_le_transaction" }),
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.CorrelationId),
                new CreateIndexOptions { Name = "ix_tenant_le_correlation" })
        ];
    }

    public static IReadOnlyList<CreateIndexModel<InventoryOutboxMessage>> BuildOutbox()
    {
        var b = Builders<InventoryOutboxMessage>.IndexKeys;
        return
        [
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.Status).Ascending(x => x.OccurredAt),
                new CreateIndexOptions { Name = "ix_tenant_le_status_occurred" }),
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.AggregateId),
                new CreateIndexOptions { Name = "ix_tenant_le_aggregate" })
        ];
    }

    /// <summary>Creates the MOD-0173 indexes (idempotent). Nothing outside these five collections is touched.</summary>
    public static async Task ApplyAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        await database.GetCollection<InventoryTransaction>(InventoryCollections.Transactions).Indexes.CreateManyAsync(BuildTransactions(), ct);
        await database.GetCollection<InventoryBalance>(InventoryCollections.Balances).Indexes.CreateManyAsync(BuildBalances(), ct);
        await database.GetCollection<InventoryAuditRecord>(InventoryCollections.Audit).Indexes.CreateManyAsync(BuildAudit(), ct);
        await database.GetCollection<InventoryOutboxMessage>(InventoryCollections.Outbox).Indexes.CreateManyAsync(BuildOutbox(), ct);
    }
}
