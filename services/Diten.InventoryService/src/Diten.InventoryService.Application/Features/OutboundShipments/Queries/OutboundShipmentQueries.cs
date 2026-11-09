using Diten.InventoryService.Application.Common.Models;
using MediatR;

namespace Diten.InventoryService.Application.Features.OutboundShipments.Queries;

/// <summary>WMS draft §11 <c>previewOutboundAllocation</c> (pack §8.17 step 7) — steps 1–4 without a write; not a reservation.</summary>
public sealed record PreviewOutboundAllocationQuery(CreateOutboundShipmentInput Outbound) : IRequest<Response<OutboundAllocationPreviewDto>>;

/// <summary>WMS draft §11 <c>searchOutboundShipments</c> — the screen list (raw query strings; the handler parses them).</summary>
public sealed record GetOutboundShipmentListQuery(IReadOnlyList<string> Statuses, string? WarehouseId, string? Search, string? OrderBy, string? Page, string? PageSize)
    : IRequest<Response<OutboundShipmentPageDto>>;

/// <summary>WMS draft §11 <c>getOutboundShipmentView</c>.</summary>
public sealed record GetOutboundShipmentViewQuery(Guid OutboundId) : IRequest<Response<OutboundShipmentViewDto>>;

/// <summary>WMS draft §11 <c>listOutboundEvents</c>.</summary>
public sealed record GetOutboundEventListQuery(Guid OutboundId, string? Page, string? PageSize) : IRequest<Response<OutboundEventPageDto>>;
