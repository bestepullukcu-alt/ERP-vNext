using Diten.ProcurementService.Domain.Entities;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.PurchaseOrder.Queries;

/// <summary>listPurchaseOrders (contract GET /purchase-orders). Tenant+LE filtreli; opsiyonel supplierId + status + cursor.</summary>
public sealed record GetPurchaseOrderListQuery(
    string? SupplierId = null,
    PoStatus? Status = null,
    string? Cursor = null) : IRequest<Response<PurchaseOrderListResultDto>>;
