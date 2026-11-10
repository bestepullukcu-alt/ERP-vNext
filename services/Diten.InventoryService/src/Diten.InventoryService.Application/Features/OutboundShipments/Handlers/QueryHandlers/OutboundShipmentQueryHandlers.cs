using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.OutboundShipments.Queries;
using Diten.InventoryService.Application.Features.OutboundShipments.Services;
using Diten.InventoryService.Application.Features.Warehouse;
using Diten.InventoryService.Application.Features.Warehouse.Services;
using Diten.InventoryService.Domain.Warehouse.Enums;
using Diten.InventoryService.Domain.Warehouse.Repositories;
using Diten.InventoryService.Domain.Warehouse.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.OutboundShipments.Handlers.QueryHandlers;

/// <summary>
/// <c>previewOutboundAllocation</c> (pack §8.17 step 7): steps 1–4 without a write and without a reservation; a line the stock cannot cover is
/// shown with its failure code, a lot override's FEFO deviation is shown with the company's mode (the create request decides it).
/// </summary>
public sealed class PreviewOutboundAllocationHandler : IRequestHandler<PreviewOutboundAllocationQuery, Response<OutboundAllocationPreviewDto>>
{
    private readonly OutboundAllocationPlanner _planner;

    public PreviewOutboundAllocationHandler(OutboundAllocationPlanner planner) => _planner = planner;

    public async Task<Response<OutboundAllocationPreviewDto>> Handle(PreviewOutboundAllocationQuery request, CancellationToken ct)
    {
        if (OutboundAllocationPlanner.ShapeError(request.Outbound, requireShipTo: false) is { } shape) return WarehouseSupport.Invalid<OutboundAllocationPreviewDto>(shape);
        var (staging, refusal) = await _planner.StagingAsync(request.Outbound.WarehouseId, request.Outbound.StagingLocationId, ct);
        if (refusal is not null) return refusal.As<OutboundAllocationPreviewDto>();
        var plan = await _planner.PlanAsync(request.Outbound, Guid.Empty, staging!, preview: true, ct);
        if (plan.Refusal is not null) return plan.Refusal.As<OutboundAllocationPreviewDto>();
        return Response<OutboundAllocationPreviewDto>.Success(new OutboundAllocationPreviewDto(staging!.Warehouse.Id.ToString("D"), staging.Staging.Id.ToString("D"),
            plan.Mode.ToString(), plan.Preview, WarehouseContract.ContractVersion));
    }
}

/// <summary><c>searchOutboundShipments</c> — the six visible statuses only (an internal state is never listed), newest first by default.</summary>
public sealed class GetOutboundShipmentListHandler : IRequestHandler<GetOutboundShipmentListQuery, Response<OutboundShipmentPageDto>>
{
    private readonly IOutboundShipmentRepository _outbounds;

    public GetOutboundShipmentListHandler(IOutboundShipmentRepository outbounds) => _outbounds = outbounds;

    public async Task<Response<OutboundShipmentPageDto>> Handle(GetOutboundShipmentListQuery request, CancellationToken ct)
    {
        var statuses = new List<OutboundStatus>();
        foreach (var raw in request.Statuses)
        {
            if (!Enum.TryParse<OutboundStatus>(raw, false, out var status) || status.ToString() != raw || !OutboundAllocationRules.IsVisible(status))
                return WarehouseSupport.Invalid<OutboundShipmentPageDto>("status must be one of " + string.Join(", ", OutboundAllocationRules.VisibleStatuses) + ".");
            statuses.Add(status);
        }

        Guid? warehouse = null;
        if (request.WarehouseId is not null)
        {
            if (!WarehouseSupport.TryParseId(request.WarehouseId, out var w)) return WarehouseSupport.Invalid<OutboundShipmentPageDto>("warehouseId must be a canonical UUID.");
            warehouse = w;
        }

        if (request.OrderBy is not (null or "createdAt" or "-createdAt")) return WarehouseSupport.Invalid<OutboundShipmentPageDto>("orderBy must be createdAt or -createdAt.");
        if (WarehousePaging.Parse(request.Page, request.PageSize) is not { } paging) return WarehouseSupport.Invalid<OutboundShipmentPageDto>("page / pageSize out of range.");
        var search = WarehouseSupport.Trimmed(request.Search);
        if (search is { Length: > 100 }) return WarehouseSupport.Invalid<OutboundShipmentPageDto>("q must be at most 100 characters.");

        var page = await _outbounds.ListAsync(new OutboundShipmentQuery(statuses.Count == 0 ? OutboundAllocationRules.VisibleStatuses : statuses, warehouse, search,
            request.OrderBy == "createdAt", paging.Page, paging.PageSize), ct);
        return Response<OutboundShipmentPageDto>.Success(new OutboundShipmentPageDto(page.Items.Select(OutboundShipmentReads.Summary).ToList(), page.Total,
            paging.Page, paging.PageSize, WarehouseContract.ContractVersion));
    }
}

/// <summary><c>getOutboundShipmentView</c> — 404 <c>UNKNOWN_OUTBOUND</c> for another company / tenant and for an internal state.</summary>
public sealed class GetOutboundShipmentViewHandler : IRequestHandler<GetOutboundShipmentViewQuery, Response<OutboundShipmentViewDto>>
{
    private readonly OutboundShipmentReads _reads;

    public GetOutboundShipmentViewHandler(OutboundShipmentReads reads) => _reads = reads;

    public async Task<Response<OutboundShipmentViewDto>> Handle(GetOutboundShipmentViewQuery request, CancellationToken ct)
        => await _reads.FindVisibleAsync(request.OutboundId, ct) is { } outbound
            ? Response<OutboundShipmentViewDto>.Success(await _reads.ViewAsync(outbound, ct))
            : OutboundShipmentReads.UnknownOutbound<OutboundShipmentViewDto>();
}

/// <summary><c>listOutboundEvents</c> — the insert-only timeline in <c>Seq</c> order (no ship-to value in any row).</summary>
public sealed class GetOutboundEventListHandler : IRequestHandler<GetOutboundEventListQuery, Response<OutboundEventPageDto>>
{
    private readonly IOutboundShipmentRepository _outbounds;
    private readonly OutboundShipmentReads _reads;

    public GetOutboundEventListHandler(IOutboundShipmentRepository outbounds, OutboundShipmentReads reads)
    {
        _outbounds = outbounds;
        _reads = reads;
    }

    public async Task<Response<OutboundEventPageDto>> Handle(GetOutboundEventListQuery request, CancellationToken ct)
    {
        if (WarehousePaging.Parse(request.Page, request.PageSize) is not { } paging) return WarehouseSupport.Invalid<OutboundEventPageDto>("page / pageSize out of range.");
        if (await _reads.FindVisibleAsync(request.OutboundId, ct) is null) return OutboundShipmentReads.UnknownOutbound<OutboundEventPageDto>();
        var page = await _outbounds.EventsAsync(request.OutboundId, paging.Page, paging.PageSize, ct);
        return Response<OutboundEventPageDto>.Success(new OutboundEventPageDto(page.Items.Select(OutboundShipmentReads.Event).ToList(), page.Total, paging.Page,
            paging.PageSize, WarehouseContract.ContractVersion));
    }
}
