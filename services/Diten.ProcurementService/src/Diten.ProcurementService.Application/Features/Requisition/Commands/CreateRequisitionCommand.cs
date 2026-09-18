using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Requisition.Commands;

/// <summary>
/// createRequisition (contract POST /requisitions). Tenant/LE server-resolved — payload'da YOK. Idempotency-Key ile
/// idempotent. line.itemId MOD-0290'da doğrulanır (fail-closed → 404 UNKNOWN_REFERENCE). ≥1 satır + Decimal/float
/// kuralı 422 VALIDATION_FAILED.
/// </summary>
public sealed record CreateRequisitionCommand(
    List<RequisitionLineInput>? Lines,
    string? Justification,
    string? IdempotencyKey) : IRequest<Response<RequisitionDto>>;
