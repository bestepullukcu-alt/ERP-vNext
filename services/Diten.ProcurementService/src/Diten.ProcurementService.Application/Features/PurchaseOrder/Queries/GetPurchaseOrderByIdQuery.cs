using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.PurchaseOrder.Queries;

/// <summary>getPurchaseOrder (contract GET /purchase-orders/{poId}). GRN 0142 bunu poId/poLineId ile okur; cross-tenant/LE → 404.</summary>
public sealed record GetPurchaseOrderByIdQuery(string PoId) : IRequest<Response<PurchaseOrderDto>>;
