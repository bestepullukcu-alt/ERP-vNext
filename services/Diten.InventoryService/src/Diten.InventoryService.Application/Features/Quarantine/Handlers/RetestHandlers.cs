using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.Quarantine.Ports;
using Diten.InventoryService.Application.Features.Quarantine.Queries;
using Diten.InventoryService.Application.Features.Quarantine.Services;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Domain.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Quarantine.Handlers;

/// <summary>
/// §4 listRetest — the retest queue (KQ-H, pack §8; mockup "Tarihi geçmiş ya da 30 gün içinde gelecek lotlar"): the tenant's lots whose
/// retest date is before today + <c>horizonDays</c> (1–90, default 30) with AVAILABLE stock in this company, or with an open Retest
/// hold here. State: an open Retest hold → <c>Quarantined</c>; retest date before today → <c>DueClosed</c> ("Tarihi geçti · kullanıma
/// kapalı"); otherwise <c>Upcoming</c>. <c>buckets</c> = the AVAILABLE rows (what "Karantinaya Al" mode Retest takes); a
/// <c>warehouseId</c> narrows buckets and holds to that warehouse. Ordered by retest date.
/// </summary>
public sealed class ListRetestHandler : IRequestHandler<ListRetestQuery, Response<IReadOnlyList<RetestItemDto>>>
{
    private static readonly string[] States = ["Upcoming", "DueClosed", "Quarantined"];

    private readonly IQuarantineLedgerReader _ledger;
    private readonly IQuarantineRepository _repository;

    public ListRetestHandler(IQuarantineLedgerReader ledger, IQuarantineRepository repository)
    {
        _ledger = ledger;
        _repository = repository;
    }

    public async Task<Response<IReadOnlyList<RetestItemDto>>> Handle(ListRetestQuery request, CancellationToken ct)
    {
        var horizon = 30;
        if (request.HorizonDays is not null && (!int.TryParse(request.HorizonDays, out horizon) || horizon is < 1 or > 90))
        {
            return QuarantineSupport.Invalid<IReadOnlyList<RetestItemDto>>("horizonDays must be an integer 1-90.");
        }

        if (request.State is not null && !States.Contains(request.State, StringComparer.Ordinal))
        {
            return QuarantineSupport.Invalid<IReadOnlyList<RetestItemDto>>("state must be Upcoming, DueClosed or Quarantined.");
        }

        if (request.WarehouseId is not null && !QuarantineSupport.TryParseId(request.WarehouseId, out _))
        {
            return QuarantineSupport.Invalid<IReadOnlyList<RetestItemDto>>("warehouseId must be a canonical UUID.");
        }

        var today = QuarantineSupport.Today();
        var lots = await _ledger.RetestLotsAsync(QuarantineSupport.Midnight(today.AddDays(horizon)), ct);
        if (lots.Count == 0)
        {
            return Response<IReadOnlyList<RetestItemDto>>.Success([]);
        }

        var lotIds = lots.Select(l => l.Id.ToString("D")).ToList();
        var available = (await _ledger.LotBalancesAsync(lotIds, StockStatus.Available, ct))
            .Where(r => request.WarehouseId is null || string.Equals(r.WarehouseId, request.WarehouseId, StringComparison.Ordinal))
            .ToLookup(r => r.LotId!);
        var retestHolds = new List<Domain.Quarantine.Entities.QuarantineHold>();
        for (var page = 1; page <= 20; page++)
        {
            var batch = await _repository.ListHoldsAsync(new HoldQuery(null, request.WarehouseId, null, HoldSource.Retest, null, null, null, null, page, 500), ct);
            retestHolds.AddRange(batch.Items);
            if (batch.Items.Count < 500) break;
        }

        var openHolds = retestHolds.Where(h => h.Status != HoldStatus.Decided && h.LotId is not null).ToLookup(h => h.LotId!);

        var items = new List<RetestItemDto>();
        foreach (var lot in lots)
        {
            var id = lot.Id.ToString("D");
            var rows = available[id].ToList();
            var holds = openHolds[id].ToList();
            if (rows.Count == 0 && holds.Count == 0)
            {
                continue;
            }

            var retestDay = DateOnly.FromDateTime(lot.RetestDate!.Value.UtcDateTime);
            var state = holds.Count > 0 ? "Quarantined" : retestDay < today ? "DueClosed" : "Upcoming";
            if (request.State is not null && request.State != state)
            {
                continue;
            }

            var uom = rows.Select(r => r.BaseUomId).Concat(holds.Select(h => h.BaseUomId)).First();
            items.Add(new RetestItemDto(
                id,
                lot.LotNumber,
                new ItemRefDto(QuarantineMapping.Id(lot.ItemId), QuarantineMapping.Id(lot.SkuId), WireNames.ToWire(lot.SkuLevel)),
                QuarantineSupport.Wire(lot.RetestDate.Value),
                retestDay.DayNumber - today.DayNumber,
                state,
                rows.Sum(r => r.OnHand),
                new UomRefDto(uom),
                rows.GroupBy(r => (r.WarehouseId, r.LocationId))
                    .Select(g => new RetestBucketDto(g.Key.WarehouseId!, g.Key.LocationId!, g.Sum(r => r.OnHand))).ToList(),
                holds.Select(h => QuarantineMapping.Id(h.Id)).ToList()));
        }

        return Response<IReadOnlyList<RetestItemDto>>.Success(items);
    }
}
