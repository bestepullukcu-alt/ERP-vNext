using Diten.InventoryService.Domain.Traceability.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Traceability;

/// <summary>
/// MOD-0174 indexes (pack §4), applied by the MOD-0173 startup index gate. The unique indexes are load-bearing:
/// <list type="bullet">
/// <item><c>ux_trace_lot_identity</c> — one lot per <c>LotIdentityPolicy</c> key (Tenant + SKU + LotNumber, K-G), even
/// under a race;</item>
/// <item><c>ux_trace_serial_identity</c> — one serial per Tenant + SKU + serial number (CT-D7);</item>
/// <item>the create / status / genealogy idempotency keys and the genealogy triple commit once;</item>
/// <item><c>ux_trace_movement_link</c> / <c>ux_trace_lot_migration</c> — slice P4 (one link per transaction, one
/// migration per tenant).</item>
/// </list>
/// </summary>
public static class TraceIndexConfiguration
{
    public const string LotIdentityIndex = "ux_trace_lot_identity";
    public const string LotCreateKeyIndex = "ux_trace_lot_create_key";
    public const string StatusChangeKeyIndex = "ux_trace_lot_status_key";
    public const string SerialIdentityIndex = "ux_trace_serial_identity";
    public const string GenealogyLinkIndex = "ux_trace_genealogy_link";
    public const string GenealogyKeyIndex = "ux_trace_genealogy_key";
    public const string MovementLinkIndex = "ux_trace_movement_link";
    public const string MigrationIndex = "ux_trace_lot_migration";

    public static IReadOnlyList<CreateIndexModel<Lot>> BuildLots()
    {
        var b = Builders<Lot>.IndexKeys;
        return
        [
            new(b.Ascending(x => x.TenantId).Ascending(x => x.IdentityKey),
                new CreateIndexOptions { Name = LotIdentityIndex, Unique = true }),
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.CreateIdempotencyKey),
                new CreateIndexOptions<Lot>
                {
                    Name = LotCreateKeyIndex,
                    Unique = true,
                    PartialFilterExpression = new BsonDocument(nameof(Lot.CreateIdempotencyKey), new BsonDocument("$type", "string"))
                }),
            new(b.Ascending(x => x.TenantId).Ascending(x => x.SkuId).Ascending(x => x.SkuLevel).Ascending(x => x.ExpiryDate),
                new CreateIndexOptions { Name = "ix_trace_lot_sku_expiry" }),
            new(b.Ascending(x => x.TenantId).Ascending(x => x.ItemId),
                new CreateIndexOptions { Name = "ix_trace_lot_item" }),
            new(b.Ascending(x => x.TenantId).Ascending(x => x.Status).Ascending(x => x.ExpiryDate),
                new CreateIndexOptions { Name = "ix_trace_lot_status_expiry" }),
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LotNumber),
                new CreateIndexOptions { Name = "ix_trace_lot_number" }),
            // The expiry sweeper reads across tenants (B-3).
            new(b.Ascending(x => x.ExpiryDate).Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_trace_lot_expiry_sweep" })
        ];
    }

    public static IReadOnlyList<CreateIndexModel<LotChangeEntry>> BuildLotHistory()
    {
        var b = Builders<LotChangeEntry>.IndexKeys;
        return
        [
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LotId).Descending(x => x.OccurredAt),
                new CreateIndexOptions { Name = "ix_trace_lot_history" }),
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LotId).Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions<LotChangeEntry>
                {
                    Name = StatusChangeKeyIndex,
                    Unique = true,
                    PartialFilterExpression = new BsonDocument(nameof(LotChangeEntry.IdempotencyKey), new BsonDocument("$type", "string"))
                })
        ];
    }

    public static IReadOnlyList<CreateIndexModel<SerialNumber>> BuildSerials()
    {
        var b = Builders<SerialNumber>.IndexKeys;
        return
        [
            new(b.Ascending(x => x.TenantId).Ascending(x => x.IdentityKey),
                new CreateIndexOptions { Name = SerialIdentityIndex, Unique = true }),
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LotId),
                new CreateIndexOptions { Name = "ix_trace_serial_lot" }),
            new(b.Ascending(x => x.TenantId).Ascending(x => x.SkuId).Ascending(x => x.SkuLevel).Ascending(x => x.Number),
                new CreateIndexOptions { Name = "ix_trace_serial_sku_number" })
        ];
    }

    public static IReadOnlyList<CreateIndexModel<LotGenealogyLink>> BuildGenealogy()
    {
        var b = Builders<LotGenealogyLink>.IndexKeys;
        return
        [
            new(b.Ascending(x => x.TenantId).Ascending(x => x.ParentLotId).Ascending(x => x.ChildLotId).Ascending(x => x.TransformationReference),
                new CreateIndexOptions { Name = GenealogyLinkIndex, Unique = true }),
            new(b.Ascending(x => x.TenantId).Ascending(x => x.ChildLotId),
                new CreateIndexOptions { Name = "ix_trace_genealogy_child" }),
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions<LotGenealogyLink>
                {
                    Name = GenealogyKeyIndex,
                    Unique = true,
                    PartialFilterExpression = new BsonDocument(nameof(LotGenealogyLink.IdempotencyKey), new BsonDocument("$type", "string"))
                })
        ];
    }

    public static IReadOnlyList<CreateIndexModel<MovementLotLink>> BuildMovementLinks()
    {
        var b = Builders<MovementLotLink>.IndexKeys;
        return
        [
            new(b.Ascending(x => x.TenantId).Ascending(x => x.TransactionId),
                new CreateIndexOptions { Name = MovementLinkIndex, Unique = true }),
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LotId),
                new CreateIndexOptions { Name = "ix_trace_movement_link_lot" })
        ];
    }

    public static IReadOnlyList<CreateIndexModel<ManualLotMigration>> BuildMigrations()
        =>
        [
            new(Builders<ManualLotMigration>.IndexKeys.Ascending(x => x.TenantId),
                new CreateIndexOptions { Name = MigrationIndex, Unique = true })
        ];

    public static async Task ApplyAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        await database.GetCollection<Lot>(TraceCollections.Lots).Indexes.CreateManyAsync(BuildLots(), ct);
        await database.GetCollection<LotChangeEntry>(TraceCollections.LotHistory).Indexes.CreateManyAsync(BuildLotHistory(), ct);
        await database.GetCollection<SerialNumber>(TraceCollections.Serials).Indexes.CreateManyAsync(BuildSerials(), ct);
        await database.GetCollection<LotGenealogyLink>(TraceCollections.GenealogyLinks).Indexes.CreateManyAsync(BuildGenealogy(), ct);
        await database.GetCollection<MovementLotLink>(TraceCollections.MovementLotLinks).Indexes.CreateManyAsync(BuildMovementLinks(), ct);
        await database.GetCollection<ManualLotMigration>(TraceCollections.LotMigrations).Indexes.CreateManyAsync(BuildMigrations(), ct);
    }
}
