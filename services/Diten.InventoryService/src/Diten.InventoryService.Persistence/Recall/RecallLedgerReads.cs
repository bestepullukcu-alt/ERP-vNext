using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Recall;

/// <summary>
/// MOD-0177 R3 — <see cref="IRecallLedgerReader"/> on the ledger's own collections, every read bound to the caller's Tenant + LegalEntity
/// (balances by lot: prefix of <c>ix_tenant_le_lot</c>-style indexes; transactions by lot: <c>ix_tenant_le_lot</c>). Reads only.
/// </summary>
public sealed class RecallLedgerReads : IRecallLedgerReader
{
    private readonly IMongoCollection<InventoryBalance> _balances;
    private readonly IMongoCollection<InventoryTransaction> _transactions;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public RecallLedgerReads(IMongoDatabase database, ITenantContext tenant)
    {
        _balances = database.GetCollection<InventoryBalance>(InventoryCollections.Balances);
        _transactions = database.GetCollection<InventoryTransaction>(InventoryCollections.Transactions);
        _tenantId = tenant.TenantId;
        _legalEntityId = tenant.LegalEntityId;
    }

    public async Task<IReadOnlyList<RecallStockBucket>> StockOfLotsAsync(IReadOnlyCollection<string> lotIds, CancellationToken cancellationToken = default)
    {
        if (lotIds.Count == 0) return [];
        var f = Builders<InventoryBalance>.Filter;
        var rows = await _balances.Find(f.Eq(x => x.TenantId, _tenantId) & f.Eq(x => x.LegalEntityId, _legalEntityId) & f.Eq(x => x.IsDeleted, false)
                                        & f.In(x => x.LotId, lotIds) & f.Gt(x => x.OnHand, 0m)).ToListAsync(cancellationToken);
        return rows.GroupBy(r => (r.LotId!, r.ItemId, r.SkuId, r.SkuLevel, r.WarehouseId, r.LocationId, r.StockStatus, r.BaseUomId))
            .Select(g => new RecallStockBucket(g.Key.Item1, g.Key.ItemId, g.Key.SkuId, g.Key.SkuLevel, g.Key.WarehouseId, g.Key.LocationId, g.Key.StockStatus,
                g.Sum(r => r.OnHand), g.Key.BaseUomId))
            .OrderBy(b => b.LotId, StringComparer.Ordinal).ThenBy(b => b.WarehouseId, StringComparer.Ordinal).ThenBy(b => b.LocationId, StringComparer.Ordinal)
            .ThenBy(b => b.Status).ToList();
    }

    public async Task<IReadOnlyList<RecallLedgerMovement>> CustomerReturnsAsync(IReadOnlyCollection<string> lotIds, CancellationToken cancellationToken = default)
    {
        if (lotIds.Count == 0) return [];
        var f = Builders<InventoryTransaction>.Filter;
        var rows = await _transactions.Find(Company & f.In(x => x.LotId, lotIds) & f.Eq(x => x.MovementType, MovementType.CustomerReturn))
            .Sort(Builders<InventoryTransaction>.Sort.Ascending(x => x.CreatedAt)).ToListAsync(cancellationToken);
        return rows.Select(Movement).ToList();
    }

    public async Task<RecallLedgerMovement?> TransactionAsync(string transactionId, CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParseExact(transactionId, "D", out var id)) return null;
        var row = await _transactions.Find(Company & Builders<InventoryTransaction>.Filter.Eq(x => x.Id, id)).FirstOrDefaultAsync(cancellationToken);
        return row is null ? null : Movement(row);
    }

    private FilterDefinition<InventoryTransaction> Company
        => Builders<InventoryTransaction>.Filter.Eq(x => x.TenantId, (Guid?)_tenantId) & Builders<InventoryTransaction>.Filter.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId);

    private static RecallLedgerMovement Movement(InventoryTransaction t)
        => new(t.Id.ToString("D"), t.TransactionNumber, t.MovementType, t.LotId, t.ItemId, t.SkuId, t.SkuLevel, t.BaseQuantity, t.BaseUomId, t.ToStockStatus,
            t.PostingDate, t.CreatedAt, t.SourceDocumentId);
}
