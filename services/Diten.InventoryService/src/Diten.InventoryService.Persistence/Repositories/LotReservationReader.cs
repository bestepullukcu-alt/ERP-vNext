using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Features.Inventory.Ports;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Persistence.Traceability;
using MongoDB.Driver;

namespace Diten.InventoryService.Persistence.Repositories;

/// <summary>
/// <see cref="ILotReservationReader"/> on <c>inventory_reservations</c>, scoped to the resolved Tenant + LE (the scope index of §4.5). It reads
/// the reservation documents, not the <c>L:</c> position: the position keeps a due reservation until <c>ExpireDueAsync</c> closes it, and the
/// FEFO re-check runs before that (§8.5 "Vade kuralı") — so the read rule is <see cref="ReservationActiveFilter"/> at the server's UTC now.
/// The session is the ambient <see cref="LedgerTransactionBinding.Session"/> (bound by the ledger for each transaction attempt): bound → the
/// read sees the posting transaction; unbound (the pre-check) → outside any transaction, as <c>InventoryLotDirectory</c> does.
/// </summary>
public sealed class LotReservationReader : ILotReservationReader
{
    private readonly IMongoCollection<InventoryReservation> _reservations;
    private readonly LedgerTransactionBinding _binding;
    private readonly Guid _tenantId;
    private readonly Guid _legalEntityId;

    public LotReservationReader(IMongoDatabase database, ITenantContext tenantContext, LedgerTransactionBinding binding)
    {
        _reservations = database.GetCollection<InventoryReservation>(InventoryCollections.Reservations);
        _binding = binding;
        _tenantId = tenantContext.TenantId;
        _legalEntityId = tenantContext.LegalEntityId;
    }

    public async Task<IReadOnlyDictionary<Guid, decimal>> ReservedByLotAsync(
        Guid itemId, Guid skuId, SkuLevel skuLevel, string warehouseId, CancellationToken cancellationToken = default)
    {
        var f = Builders<InventoryReservation>.Filter;
        var filter = f.Eq(x => x.TenantId, (Guid?)_tenantId) & f.Eq(x => x.LegalEntityId, (Guid?)_legalEntityId) & f.Eq(x => x.IsDeleted, false)
                     & ReservationActiveFilter.At(DateTimeOffset.UtcNow)
                     & f.Eq(x => x.ItemId, itemId) & f.Eq(x => x.SkuId, skuId) & f.Eq(x => x.SkuLevel, skuLevel)
                     & f.Eq(x => x.WarehouseId, warehouseId) & f.Ne(x => x.LotId, null);
        var rows = _binding.Session is { } session
            ? await _reservations.Find(session, filter).ToListAsync(cancellationToken)
            : await _reservations.Find(filter).ToListAsync(cancellationToken);
        return rows.GroupBy(r => r.LotId!.Value).ToDictionary(g => g.Key, g => g.Sum(r => r.RemainingQuantity));
    }
}
