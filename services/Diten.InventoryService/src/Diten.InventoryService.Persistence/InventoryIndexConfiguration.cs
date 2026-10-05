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
    public const string ReservationIdempotencyIndex = "ux_tenant_le_reservation_idempotencykey";
    public const string SkuPositionIndex = "ux_sku_position";
    public const string LocationCodeIndex = "ux_location_code";
    public const string LocationCreateKeyIndex = "ux_location_create_key";

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
                new CreateIndexOptions { Name = "ix_tenant_le_item_warehouse" }),
            // MOD-0173-FU02 K-10: stock presence of a location / warehouse is answered from these rows.
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.LocationId),
                new CreateIndexOptions { Name = "ix_tenant_le_location" }),
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.WarehouseId),
                new CreateIndexOptions { Name = "ix_tenant_le_warehouse" })
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

    public static IReadOnlyList<CreateIndexModel<InventoryReservation>> BuildReservations()
    {
        var b = Builders<InventoryReservation>.IndexKeys;
        return
        [
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions { Name = ReservationIdempotencyIndex, Unique = true }),
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.ItemId).Ascending(x => x.SkuId)
                    .Ascending(x => x.SkuLevel).Ascending(x => x.Status).Ascending(x => x.ExpiresAt),
                new CreateIndexOptions { Name = "ix_tenant_le_sku_status_expires" }),
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.SourceModule)
                    .Ascending(x => x.SourceDocumentId).Ascending(x => x.SourceLineId).Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_tenant_le_source_status" }),
            // System index for the cross-tenant expiry sweeper (it only DISCOVERS due groups here; every write is then
            // scoped to one Tenant + LE). Deliberately not tenant-first — a tenant-first index cannot serve that scan.
            new(b.Ascending(x => x.Status).Ascending(x => x.ExpiresAt),
                new CreateIndexOptions { Name = "ix_system_status_expires" })
        ];
    }

    public static IReadOnlyList<CreateIndexModel<InventorySkuPosition>> BuildSkuPositions()
    {
        var b = Builders<InventorySkuPosition>.IndexKeys;
        return
        [
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.ItemId).Ascending(x => x.SkuId)
                    .Ascending(x => x.SkuLevel),
                new CreateIndexOptions { Name = SkuPositionIndex, Unique = true })
        ];
    }

    /// <summary>
    /// MOD-0173-FU02 §10. <c>ux_location_code</c> carries K-02 (code unique per company, case-insensitive, every status);
    /// <c>ux_location_create_key</c> makes a create key commit once even under a race (§8).
    /// </summary>
    public static IReadOnlyList<CreateIndexModel<Location>> BuildLocations()
    {
        var b = Builders<Location>.IndexKeys;
        return
        [
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.CodeNormalized),
                new CreateIndexOptions { Name = LocationCodeIndex, Unique = true }),
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.CreateIdempotencyKey),
                new CreateIndexOptions<Location>
                {
                    Name = LocationCreateKeyIndex,
                    Unique = true,
                    PartialFilterExpression = new BsonDocument(nameof(Location.CreateIdempotencyKey), new BsonDocument("$type", "string"))
                }),
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.ParentId),
                new CreateIndexOptions { Name = "ix_location_parent" }),
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.AncestorIds),
                new CreateIndexOptions { Name = "ix_location_ancestors" }),
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.BulkIdempotencyKey),
                new CreateIndexOptions<Location>
                {
                    Name = "ix_location_bulk_key",
                    PartialFilterExpression = new BsonDocument(nameof(Location.BulkIdempotencyKey), new BsonDocument("$type", "string"))
                })
        ];
    }

    public static IReadOnlyList<CreateIndexModel<LocationChangeEntry>> BuildLocationHistory()
    {
        var b = Builders<LocationChangeEntry>.IndexKeys;
        return
        [
            new(b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.LocationId).Descending(x => x.OccurredAt),
                new CreateIndexOptions { Name = "ix_location_history" })
        ];
    }

    /// <summary>Creates the MOD-0173 indexes (idempotent). Nothing outside the MOD-0173 collections is touched.</summary>
    public static async Task ApplyAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        await database.GetCollection<InventoryReservation>(InventoryCollections.Reservations).Indexes.CreateManyAsync(BuildReservations(), ct);
        await database.GetCollection<InventorySkuPosition>(InventoryCollections.SkuPositions).Indexes.CreateManyAsync(BuildSkuPositions(), ct);
        await database.GetCollection<InventoryReservationAuditRecord>(InventoryCollections.Audit).Indexes.CreateOneAsync(
            new CreateIndexModel<InventoryReservationAuditRecord>(
                Builders<InventoryReservationAuditRecord>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.ReservationId),
                new CreateIndexOptions { Name = "ix_tenant_le_reservation" }),
            cancellationToken: ct);
        await database.GetCollection<InventoryTransaction>(InventoryCollections.Transactions).Indexes.CreateManyAsync(BuildTransactions(), ct);
        await database.GetCollection<InventoryBalance>(InventoryCollections.Balances).Indexes.CreateManyAsync(BuildBalances(), ct);
        await database.GetCollection<InventoryAuditRecord>(InventoryCollections.Audit).Indexes.CreateManyAsync(BuildAudit(), ct);
        await database.GetCollection<InventoryOutboxMessage>(InventoryCollections.Outbox).Indexes.CreateManyAsync(BuildOutbox(), ct);
        await database.GetCollection<Location>(InventoryCollections.Locations).Indexes.CreateManyAsync(BuildLocations(), ct);
        await database.GetCollection<LocationChangeEntry>(InventoryCollections.LocationHistory).Indexes.CreateManyAsync(BuildLocationHistory(), ct);
        // MOD-0174 (lot / serial SoR, same database and startup gate).
        await Traceability.TraceIndexConfiguration.ApplyAsync(database, ct);
    }
}
