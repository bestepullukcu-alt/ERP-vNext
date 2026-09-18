using Diten.ProcurementService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.ProcurementService.Persistence;

/// <summary>
/// Goods Receipt (goods_receipts) collection index tanımları (MOD-0142 §4). Tenant-first indexing
/// (mongo-indexing.md DB-001): her index TenantId (+ LegalEntityId) ile başlar. GrnId ve IdempotencyKey tenant+LE
/// bazında unique (replay-safe create).
/// </summary>
public static class GrnIndexConfiguration
{
    public const string GrnCollectionName = "goods_receipts";

    public static IReadOnlyList<CreateIndexModel<GoodsReceipt>> BuildGrn()
    {
        var b = Builders<GoodsReceipt>.IndexKeys;

        return new List<CreateIndexModel<GoodsReceipt>>
        {
            // Compound tenant+LE — her sorgunun giriş filtresi (COLLSCAN önleme).
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId),
                new CreateIndexOptions { Name = "ix_tenant_le" }),

            // GrnId public code — tenant+LE bazında unique.
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.GrnId),
                new CreateIndexOptions { Name = "ux_tenant_le_grnid", Unique = true }),

            // Idempotency-Key — tenant+LE bazında unique (replay-safe; mükerrer INVENTORY post önlenir). Sparse:
            // yalnız key taşıyan dokümanlar unique kısıtına girer (null'lar çakışmaz).
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.IdempotencyKey),
                new CreateIndexOptions { Name = "ux_tenant_le_idempotencykey", Unique = true, Sparse = true }),

            // poId filtresi (GetGrnList poId).
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.PoId),
                new CreateIndexOptions { Name = "ix_tenant_le_poid" }),

            // Status filtresi (GetGrnList status).
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_tenant_le_status" })
        };
    }

    public static async Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var goodsReceipts = database.GetCollection<GoodsReceipt>(GrnCollectionName);
        await goodsReceipts.Indexes.CreateManyAsync(BuildGrn(), ct);
    }
}
