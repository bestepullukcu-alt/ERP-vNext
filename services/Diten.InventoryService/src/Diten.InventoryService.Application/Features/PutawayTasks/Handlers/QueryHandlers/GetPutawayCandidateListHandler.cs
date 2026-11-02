using Diten.InventoryService.Application.Common;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Common.Upstream;
using Diten.InventoryService.Application.Features.PutawayTasks.Queries;
using Diten.InventoryService.Application.Features.PutawayTasks.Services;
using Diten.InventoryService.Application.Features.Recall.Ports;
using Diten.InventoryService.Application.Features.Warehouse.Ports;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Entities;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Repositories;
using Diten.InventoryService.Domain.Recall.Repositories;
using Diten.InventoryService.Domain.Repositories;
using Diten.InventoryService.Domain.Rules;
using Diten.InventoryService.Domain.Traceability.Enums;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.PutawayTasks.Handlers.QueryHandlers;

/// <summary>
/// <c>listPutawayCandidates</c> (pack §8.2, §8.15; AC-2, AC-7, AC-41): the company's INVENTORY rows (OnHand &gt; 0, every status) in the subtrees
/// of its Receiving nodes, each with its open quantity (OnHand − the open tasks' remaining on the same bucket, read now — never stored) and
/// whether it can be put away. A row that cannot is NOT hidden: <c>eligible: false</c>, the reason and the blocking document's number — the
/// 0175 hold / blocked record, the recall case, the lot status.
/// <para>DL-3 CT F-2 (§11.3a, draft §3.3): each row of the PAGE carries its product's <c>storageConditions</c> from PRODUCT-MASTER, read once per
/// SKU of the page. A SKU that does not state them stays without the member (UNKNOWN — the screen's badge). The read is display only: the draft
/// gives this operation no 503, so PRODUCT-MASTER down leaves the member out too and the list still answers; the create / confirm rule
/// (§8.12) keeps its own fail-closed 503.</para>
/// </summary>
public sealed class GetPutawayCandidateListHandler : IRequestHandler<GetPutawayCandidateListQuery, Response<PutawayCandidatePageDto>>
{
    private readonly IWarehouseLocationRoleRepository _roles;
    private readonly ILocationRepository _locations;
    private readonly IWarehouseStockReader _stock;
    private readonly IWarehouseTaskRepository _tasks;
    private readonly IWarehouseLotReader _lots;
    private readonly IRecallLotStatusReader _recall;
    private readonly IRecallRepository _recallCases;
    private readonly IQuarantineRepository _quarantine;
    private readonly IProductMasterClient _products;

    public GetPutawayCandidateListHandler(IWarehouseLocationRoleRepository roles, ILocationRepository locations, IWarehouseStockReader stock,
        IWarehouseTaskRepository tasks, IWarehouseLotReader lots, IRecallLotStatusReader recall, IRecallRepository recallCases, IQuarantineRepository quarantine,
        IProductMasterClient products)
    {
        _products = products;
        _roles = roles;
        _locations = locations;
        _stock = stock;
        _tasks = tasks;
        _lots = lots;
        _recall = recall;
        _recallCases = recallCases;
        _quarantine = quarantine;
    }

    public async Task<Response<PutawayCandidatePageDto>> Handle(GetPutawayCandidateListQuery request, CancellationToken ct)
    {
        Guid? warehouse = null, sku = null;
        if (request.WarehouseId is not null)
        {
            if (!WarehouseSupport.TryParseId(request.WarehouseId, out var id)) return WarehouseSupport.Invalid<PutawayCandidatePageDto>("warehouseId must be a UUID.");
            warehouse = id;
        }

        if (request.SkuId is not null)
        {
            if (!WarehouseSupport.TryParseId(request.SkuId, out var id)) return WarehouseSupport.Invalid<PutawayCandidatePageDto>("skuId must be a UUID.");
            sku = id;
        }

        bool? eligible = request.Eligible switch { null => null, "true" => true, "false" => false, _ => (bool?)null };
        if (request.Eligible is not (null or "true" or "false")) return WarehouseSupport.Invalid<PutawayCandidatePageDto>("eligible must be true or false.");
        if (WarehousePaging.Parse(request.Page, request.PageSize) is not { } paging) return WarehouseSupport.Invalid<PutawayCandidatePageDto>("page must be ≥ 1 and pageSize 1-200.");

        // the Receiving nodes and their subtrees (the role is inherited down, F-13)
        var receiving = await _roles.FindWithRoleAsync(WarehouseRole.Receiving, ct);
        var places = new Dictionary<Guid, Location>();
        var queue = new Queue<Guid>(receiving.Select(r => r.LocationId));
        foreach (var node in await _locations.FindManyAsync(queue.ToList(), ct)) places[node.Id] = node;
        while (queue.Count > 0)
        {
            var parent = queue.Dequeue();
            foreach (var child in await _locations.ListAsync(null, parent, ct))
            {
                if (places.TryAdd(child.Id, child)) queue.Enqueue(child.Id);
            }
        }

        IReadOnlyList<WarehouseStockRow> rows;
        try
        {
            rows = await _stock.RowsAtAsync(places.Keys.ToList(), ct);
        }
        catch (WarehouseInventoryUnavailableException)
        {
            return PutawayTaskSupport.Fail<PutawayCandidatePageDto>(InventoryErrorCodes.InventoryUnavailable, "The stock could not be read.", 503);
        }

        rows = rows.Where(r => (warehouse is null || r.WarehouseId == warehouse) && (sku is null || r.SkuId == sku)
                               && (request.LotId is null || string.Equals(r.LotId, request.LotId, StringComparison.Ordinal))).ToList();
        var open = await _tasks.OpenTasksFromAsync(rows.Select(r => r.LocationId).Distinct().ToList(), ct);
        var lotIds = rows.Select(r => r.LotId).OfType<string>().Distinct().ToList();
        var lots = await _lots.LotsAsync(lotIds, ct);
        IReadOnlySet<string> recalled;
        try
        {
            recalled = lotIds.Count == 0 ? new HashSet<string>() : await _recall.UnderRecallAsync(lotIds, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return PutawayTaskSupport.Fail<PutawayCandidatePageDto>(InventoryErrorCodes.InventoryUnavailable, "The recall status could not be read.", 503);
        }

        var candidates = new List<PutawayCandidateDto>();
        foreach (var r in rows)
        {
            var hold = open.Where(t => t.WarehouseId == r.WarehouseId && t.FromLocationId == r.LocationId && t.SkuId == r.SkuId && t.SkuLevel == r.SkuLevel
                                       && string.Equals(t.LotId, r.LotId, StringComparison.Ordinal) && t.StockStatus == r.StockStatus)
                .Sum(PutawayTaskRules.OpenHold);
            var lot = r.LotId is null ? null : lots.GetValueOrDefault(r.LotId);
            var (reason, document) = await BlockAsync(r, lot, recalled, ct);
            var dto = new PutawayCandidateDto(
                string.Join('|', r.WarehouseId.ToString("D"), r.LocationId.ToString("D"), r.SkuId.ToString("D"), WireNames.ToWire(r.SkuLevel), r.LotId ?? string.Empty,
                    WireNames.ToWire(r.StockStatus)),
                r.WarehouseId.ToString("D"), r.LocationId.ToString("D"), places.GetValueOrDefault(r.LocationId)?.Code,
                new TaskItemDto(r.ItemId.ToString("D"), r.SkuId.ToString("D"), WireNames.ToWire(r.SkuLevel)),
                WireNames.ToWire(r.StockStatus), PutawayTaskSupport.Q(r.OnHand), PutawayTaskSupport.Q(hold), PutawayTaskSupport.Q(Math.Max(0m, r.OnHand - hold)),
                r.BaseUomId, reason is null, r.LotId, lot?.LotNumber ?? r.LotId, lot?.Status.ToString(), reason, document, PutawayTaskSupport.Wire(r.LastPostingDate));
            candidates.Add(dto);
        }

        var search = WarehouseSupport.Trimmed(request.Search);
        var filtered = candidates.Where(c => (eligible is null || c.Eligible == eligible)
                                             && (search is null || (c.LotNumber?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                                                 || (c.LocationCode?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)))
            .OrderBy(c => c.LocationCode, StringComparer.Ordinal).ThenBy(c => c.LotNumber, StringComparer.Ordinal).ToList();
        var page = filtered.Skip((paging.Page - 1) * paging.PageSize).Take(paging.PageSize).ToList();
        var conditions = await StorageConditionsAsync(page, ct);
        return Response<PutawayCandidatePageDto>.Success(new PutawayCandidatePageDto(
            page.Select(c => c with { StorageConditions = conditions.GetValueOrDefault((c.Item.SkuId, c.Item.SkuLevel)) }).ToList(),
            filtered.Count, paging.Page, paging.PageSize));
    }

    /// <summary>The product's stated conditions per SKU of the page; a SKU that states none, is not found, or PRODUCT-MASTER down → no entry (UNKNOWN).</summary>
    private async Task<Dictionary<(string SkuId, string SkuLevel), IReadOnlyList<string>>> StorageConditionsAsync(IReadOnlyList<PutawayCandidateDto> page,
        CancellationToken ct)
    {
        var found = new Dictionary<(string, string), IReadOnlyList<string>>();
        foreach (var (skuId, level) in page.Select(c => (c.Item.SkuId, c.Item.SkuLevel)).Distinct())
        {
            if (!Guid.TryParse(skuId, out var id) || !WireNames.TryParseSkuLevel(level, out var skuLevel)) continue;
            try
            {
                if ((await _products.GetSkuAsync(id, skuLevel, ct))?.StorageConditions is { } stated) found[(skuId, level)] = stated;
            }
            catch (ProductMasterUnavailableException)
            {
                return found; // display only — the rows of this page stay UNKNOWN; create / confirm answer 503 themselves (§8.12)
            }
        }

        return found;
    }

    /// <summary>Why a row cannot be put away and the document that blocks it (§8.15 "Seçilemeyen stok gizlenmez"); null = eligible.</summary>
    private async Task<(string? Reason, BlockingDocumentDto? Document)> BlockAsync(WarehouseStockRow r, WarehouseLotInfo? lot, IReadOnlySet<string> recalled,
        CancellationToken ct)
    {
        if (r.LotId is not null && recalled.Contains(r.LotId))
        {
            var block = (await _recallCases.ActiveLotBlocksAsync(r.LotId, ct)).FirstOrDefault();
            return ("LotUnderRecall", block is null ? null : new BlockingDocumentDto("RecallCase", block.CaseId.ToString("D"), block.CaseNumber));
        }

        if (r.StockStatus != StockStatus.Available)
        {
            var warehouse = r.WarehouseId.ToString("D");
            var location = r.LocationId.ToString("D");
            var holds = await _quarantine.ListHoldsAsync(new HoldQuery(null, warehouse, null, null, null, null, r.LotId, null, 1, 200), ct);
            var hold = holds.Items.FirstOrDefault(h => h.Status != HoldStatus.Decided && h.LocationId == location && h.SkuId == r.SkuId);
            if (hold is not null) return ("StockHeld", new BlockingDocumentDto("QuarantineHold", hold.Id.ToString("D"), hold.HoldNumber));
            var blocked = await _quarantine.ListBlockedAsync(new BlockedQuery(null, null, false, null, BlockedStatus.Open, warehouse, 1, 200), ct);
            var record = blocked.Items.FirstOrDefault(b => b.LocationId == location && b.SkuId == r.SkuId && string.Equals(b.LotId, r.LotId, StringComparison.Ordinal));
            return ("StockHeld", record is null ? null : new BlockingDocumentDto("BlockedStock", record.Id.ToString("D"), record.BlockedNumber));
        }

        return lot?.Status switch
        {
            LotStatus.Blocked => ("LotBlocked", new BlockingDocumentDto("LotStatus", lot.LotId, lot.LotNumber)),
            LotStatus.Expired => ("LotExpired", new BlockingDocumentDto("LotStatus", lot.LotId, lot.LotNumber)),
            _ => (null, null)
        };
    }
}
