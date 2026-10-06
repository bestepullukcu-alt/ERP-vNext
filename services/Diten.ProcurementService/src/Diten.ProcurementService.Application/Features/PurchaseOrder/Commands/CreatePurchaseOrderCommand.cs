using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.PurchaseOrder.Commands;

/// <summary>
/// createPurchaseOrder (contract POST /purchase-orders). Onaylı requisition'dan veya doğrudan. Tenant/LE
/// server-resolved — payload'da YOK. Idempotency-Key ile idempotent. supplierId MOD-0140'ta (fail-closed → 404),
/// line.itemId MOD-0290'da (seam, fail-closed → 404) doğrulanır. lineAmount/totalAmount SERVER-COMPUTED (Decimal).
/// requisitionId verilirse mevcut + Approved olmalı (aksi 422). ≥1 satır + Decimal/float 422 VALIDATION_FAILED.
/// </summary>
public sealed record CreatePurchaseOrderCommand(
    string SupplierId,
    string? RequisitionId,
    string Currency,
    List<PoLineInput>? Lines,
    string? SourceSystem,
    string? ExternalRef,
    string? IdempotencyKey) : IRequest<Response<PurchaseOrderDto>>;
