using Diten.ProcurementService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.ProcurementService.Persistence;

/// <summary>
/// Requisition (requisitions) + Purchase Order (purchase_orders) collection index tanımları (MOD-0141 §4).
/// Tenant-first indexing (mongo-indexing.md DB-001): her index TenantId (+ LegalEntityId) ile başlar.
/// </summary>
public static class RequisitionPoIndexConfiguration
{
    public const string RequisitionCollectionName = "requisitions";
    public const string PurchaseOrderCollectionName = "purchase_orders";

    public static IReadOnlyList<CreateIndexModel<Requisition>> BuildRequisition()
    {
        var b = Builders<Requisition>.IndexKeys;

        return new List<CreateIndexModel<Requisition>>
        {
            // Compound tenant+LE — her sorgunun giriş filtresi (COLLSCAN önleme).
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId),
                new CreateIndexOptions { Name = "ix_tenant_le" }),

            // RequisitionId public code — tenant+LE bazında unique.
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.RequisitionId),
                new CreateIndexOptions { Name = "ux_tenant_le_requisitionid", Unique = true }),

            // Status filtresi (contract listRequisitions status).
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_tenant_le_status" })
        };
    }

    public static IReadOnlyList<CreateIndexModel<PurchaseOrder>> BuildPurchaseOrder()
    {
        var b = Builders<PurchaseOrder>.IndexKeys;

        return new List<CreateIndexModel<PurchaseOrder>>
        {
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId),
                new CreateIndexOptions { Name = "ix_tenant_le" }),

            // PoId public code — tenant+LE bazında unique.
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.PoId),
                new CreateIndexOptions { Name = "ux_tenant_le_poid", Unique = true }),

            // supplierId filtresi (contract listPurchaseOrders supplierId).
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.SupplierId),
                new CreateIndexOptions { Name = "ix_tenant_le_supplierid" }),

            // Status filtresi (contract listPurchaseOrders status).
            new(
                b.Ascending(x => x.TenantId).Ascending(x => x.LegalEntityId).Ascending(x => x.Status),
                new CreateIndexOptions { Name = "ix_tenant_le_status" })
        };
    }

    public static async Task EnsureIndexesAsync(IMongoDatabase database, CancellationToken ct = default)
    {
        var requisitions = database.GetCollection<Requisition>(RequisitionCollectionName);
        await requisitions.Indexes.CreateManyAsync(BuildRequisition(), ct);

        var purchaseOrders = database.GetCollection<PurchaseOrder>(PurchaseOrderCollectionName);
        await purchaseOrders.Indexes.CreateManyAsync(BuildPurchaseOrder(), ct);
    }
}
