using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.PurchaseOrder.Commands;

/// <summary>
/// approvePurchaseOrder (contract POST /purchase-orders/{poId}/approve). Yalnız Draft→Approved; Draft dışı → 409
/// INVALID_STATE. Approve'da MOD-0023 workflowInstanceId atanır (server-side seam). Idempotency-Key ile idempotent.
/// Approved PO, G2A golden flow'un upstream belgesidir (GRN 0142 / Invoice-Match 0143 okur).
/// </summary>
public sealed record ApprovePurchaseOrderCommand(
    string PoId,
    string? IdempotencyKey) : IRequest<Response<PurchaseOrderDto>>;
