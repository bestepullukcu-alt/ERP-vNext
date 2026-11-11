using System.Globalization;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.OutboundShipments.Services;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.OutboundShipments.Queries;

/// <summary>The frozen <c>listOutboundShipments</c> (WAREHOUSE-OUTBOUND 1.0.0) — raw values; the controller already checked <c>status</c>.</summary>
public sealed record GetFrozenOutboundListQuery(string? Status, string? WarehouseId, string? Cursor) : IRequest<Response<Dictionary<string, object?>>>;

/// <summary>The frozen <c>getOutboundShipment</c> (WAREHOUSE-OUTBOUND 1.0.0) — the id as sent (a string: not a UUID → 404 UNKNOWN_OUTBOUND).</summary>
public sealed record GetFrozenOutboundQuery(string OutboundId) : IRequest<Response<Dictionary<string, object?>>>;

/// <summary>
/// DL-5 (pack §8.18, §8.21, §8.24; AC-30, AC-33 (v)): the frozen reads, filled. Only the six visible statuses (an internal one is absent / 404) and
/// never an Item-level outbound (MVP-6's reader refuses the whole page for one Item line — §8.24 rule 4). The page: <c>items</c> + <c>contractVersion</c>,
/// <c>nextCursor</c> only when a next page exists (never a JSON null — GAP-0183-03); 200 per page, oldest first (a poll walks forward).
/// </summary>
public sealed class FrozenOutboundQueryHandlers :
    IRequestHandler<GetFrozenOutboundListQuery, Response<Dictionary<string, object?>>>,
    IRequestHandler<GetFrozenOutboundQuery, Response<Dictionary<string, object?>>>
{
    public const int PageSize = 200;

    private readonly IOutboundShipmentRepository _outbounds;
    private readonly OutboundShipmentReads _reads;

    public FrozenOutboundQueryHandlers(IOutboundShipmentRepository outbounds, OutboundShipmentReads reads)
    {
        _outbounds = outbounds;
        _reads = reads;
    }

    public async Task<Response<Dictionary<string, object?>>> Handle(GetFrozenOutboundListQuery request, CancellationToken ct)
    {
        var page = 1;
        if (request.Cursor is not null && (!int.TryParse(request.Cursor, NumberStyles.None, CultureInfo.InvariantCulture, out page) || page < 1))
            return WarehouseSupport.Invalid<Dictionary<string, object?>>("cursor is not a cursor of this list.");

        var statuses = request.Status is null ? OutboundAllocationRules.VisibleStatuses : [Enum.Parse<OutboundStatus>(request.Status)];
        var empty = new Dictionary<string, object?> { ["items"] = Array.Empty<object>(), ["contractVersion"] = WarehouseContract.ContractVersion };
        Guid? warehouse = null;
        if (request.WarehouseId is not null)
        {
            if (!WarehouseSupport.TryParseId(request.WarehouseId, out var w)) return Response<Dictionary<string, object?>>.Success(empty); // no such warehouse id
            warehouse = w;
        }

        var result = await _outbounds.ListAsync(new OutboundShipmentQuery(statuses, warehouse, null, OldestFirst: true, page, PageSize, ExcludeItemLevel: true), ct);
        var ids = result.Items.Select(o => o.Id).ToList();
        var tasks = (await _outbounds.PickTasksOfManyAsync(ids, ct)).ToLookup(t => t.OutboundId!.Value);
        var packages = (await _outbounds.PackagesOfManyAsync(ids, ct)).ToLookup(p => p.OutboundId);
        var body = new Dictionary<string, object?>
        {
            ["items"] = result.Items.Select(o => OutboundFlowReads.Frozen(o, tasks[o.Id].ToList(), packages[o.Id].ToList())).ToList()
        };
        if ((long)page * PageSize < result.Total) body["nextCursor"] = (page + 1).ToString(CultureInfo.InvariantCulture);
        body["contractVersion"] = WarehouseContract.ContractVersion;
        return Response<Dictionary<string, object?>>.Success(body);
    }

    public async Task<Response<Dictionary<string, object?>>> Handle(GetFrozenOutboundQuery request, CancellationToken ct)
    {
        if (!WarehouseSupport.TryParseId(request.OutboundId, out var id)) return OutboundShipmentReads.UnknownOutbound<Dictionary<string, object?>>();
        var outbound = await _reads.FindVisibleAsync(id, ct);
        if (outbound is null || outbound.IsItemLevel()) return OutboundShipmentReads.UnknownOutbound<Dictionary<string, object?>>();
        var tasks = await _outbounds.PickTasksAsync(outbound.Id, ct);
        var packages = await _outbounds.PackagesAsync(outbound.Id, ct);
        return Response<Dictionary<string, object?>>.Success(OutboundFlowReads.Frozen(outbound, tasks, packages));
    }
}
