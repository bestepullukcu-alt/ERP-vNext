using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Warehouse.Ports;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Warehouse;

/// <summary>
/// <see cref="IWarehouseStockReader"/> — a READ of the ledger's balance projection (the MOD-0175 <c>QuarantineLedgerReads</c> precedent), bound
/// to the caller's Tenant + company. <c>Find</c> only: MOD-0178 never writes a balance (pack §2 boundary rules 1–2; the architecture test pins
/// that this file only reads). Stock changes go through <see cref="IWarehouseMovementPoster"/>.
/// </summary>
public sealed class WarehouseStockReads : IWarehouseStockReader
{
    private readonly IMongoCollection<InventoryBalance> _balances;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public WarehouseStockReads(IMongoDatabase database, ITenantContext tenant)
    {
        _balances = database.GetCollection<InventoryBalance>(InventoryCollections.Balances);
        _tenantId = tenant.TenantId;
        _legalEntityId = tenant.LegalEntityId;
    }

    private FilterDefinition<InventoryBalance> Scope()
        => Builders<InventoryBalance>.Filter.Eq(x => x.TenantId, _tenantId) & Builders<InventoryBalance>.Filter.Eq(x => x.LegalEntityId, _legalEntityId)
           & Builders<InventoryBalance>.Filter.Gt(x => x.OnHand, 0m);

    public async Task<IReadOnlyList<WarehouseStockRow>> RowsAtAsync(IReadOnlyCollection<Guid> locationIds, CancellationToken cancellationToken = default)
    {
        if (locationIds.Count == 0) return [];
        try
        {
            var rows = await _balances.Find(Scope() & Builders<InventoryBalance>.Filter.In(x => x.LocationId, locationIds.Select(l => l.ToString("D"))))
                .Limit(5000).ToListAsync(cancellationToken);
            return rows.Select(Row).OfType<WarehouseStockRow>().ToList();
        }
        catch (MongoException ex)
        {
            throw new WarehouseInventoryUnavailableException("The stock rows could not be read.", ex);
        }
    }

    public async Task<IReadOnlyList<Guid>> LastStockedLocationsAsync(Guid warehouseId, Guid skuId, SkuLevel skuLevel, string? lotId,
        CancellationToken cancellationToken = default)
    {
        var f = Builders<InventoryBalance>.Filter;
        var filter = Scope() & f.Eq(x => x.WarehouseId, warehouseId.ToString("D")) & f.Eq(x => x.SkuId, skuId) & f.Eq(x => x.SkuLevel, skuLevel);
        if (lotId is not null) filter &= f.Eq(x => x.LotId, lotId);
        try
        {
            var rows = await _balances.Find(filter).SortByDescending(x => x.LastPostingDate).Limit(50).ToListAsync(cancellationToken);
            return rows.Select(r => Guid.TryParse(r.LocationId, out var id) ? id : (Guid?)null).OfType<Guid>().Distinct().ToList();
        }
        catch (MongoException ex)
        {
            throw new WarehouseInventoryUnavailableException("The stocked locations could not be read.", ex);
        }
    }

    public async Task<IReadOnlyList<WarehouseStockRow>> AvailableRowsOfSkuAsync(Guid warehouseId, Guid itemId, Guid skuId, SkuLevel skuLevel,
        CancellationToken cancellationToken = default)
    {
        var f = Builders<InventoryBalance>.Filter;
        var filter = Scope() & f.Eq(x => x.WarehouseId, warehouseId.ToString("D")) & f.Eq(x => x.ItemId, itemId) & f.Eq(x => x.SkuId, skuId)
                     & f.Eq(x => x.SkuLevel, skuLevel) & f.Eq(x => x.StockStatus, StockStatus.Available) & f.Eq(x => x.IsDeleted, false);
        try
        {
            var rows = await _balances.Find(filter).Limit(5000).ToListAsync(cancellationToken);
            return rows.Select(Row).OfType<WarehouseStockRow>().ToList();
        }
        catch (MongoException ex)
        {
            throw new WarehouseInventoryUnavailableException("The stock rows of the SKU could not be read.", ex);
        }
    }

    private static WarehouseStockRow? Row(InventoryBalance b)
        => Guid.TryParse(b.WarehouseId, out var warehouse) && Guid.TryParse(b.LocationId, out var location)
            ? new WarehouseStockRow(b.ItemId, b.SkuId, b.SkuLevel, warehouse, location, b.LotId, b.StockStatus, b.OnHand, b.BaseUomId, b.LastPostingDate)
            : null;
}
