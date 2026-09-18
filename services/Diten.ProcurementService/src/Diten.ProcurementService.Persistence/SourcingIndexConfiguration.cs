using Diten.ProcurementService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.ProcurementService.Persistence;

/// <summary>
/// Sourcing (rfx_events + bids) collection index tanımları (MOD-0145 §4). Tenant-first indexing
/// (mongo-indexing.md DB-001): her index TenantId (+ LegalEntityId) ile başlar.
/// </summary>
public static class SourcingIndexConfiguration
{
    public const string RfxCollectionName = "rfx_events";
    public const string BidCollectionName = "bids";

    public static IReadOnlyList<CreateIndexModel<RfxEvent>> BuildRfx()
    {
        var b = Builders<RfxEvent>.IndexKeys;

        return new List<CreateIndexModel<RfxEvent>>
        {
            // Compound tenant+LE — her sorgunun giriş filtresi (COLLSCAN önleme).
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId),
                new CreateIndexOptions { Name = "ix_tenant_le" }),

            // RfxId public code — tenant+LE bazında unique.
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.RfxId),
                new CreateIndexOptions { Name = "ux_tenant_le_rfxid", Unique = true }),

            // Status filtresi (contract listRfxEvents status).
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_tenant_le_status" }),

            // Title text index (arama).
            new(
                b.Text(x => x.Title),
                new CreateIndexOptions { Name = "tx_title" })
        };
    }

    public static IReadOnlyList<CreateIndexModel<Bid>> BuildBid()
    {
        var b = Builders<Bid>.IndexKeys;

        return new List<CreateIndexModel<Bid>>
        {
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId),
                new CreateIndexOptions { Name = "ix_tenant_le" }),

            // BidId public code — tenant+LE bazında unique.
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.BidId),
                new CreateIndexOptions { Name = "ux_tenant_le_bidid", Unique = true }),

            // RFx'e göre bid listeleme (contract listBids).
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.RfxId),
                new CreateIndexOptions { Name = "ix_tenant_le_rfxid" })
        };
    }

    public static async Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var rfx = database.GetCollection<RfxEvent>(RfxCollectionName);
        await rfx.Indexes.CreateManyAsync(BuildRfx(), ct);

        var bids = database.GetCollection<Bid>(BidCollectionName);
        await bids.Indexes.CreateManyAsync(BuildBid(), ct);
    }
}
