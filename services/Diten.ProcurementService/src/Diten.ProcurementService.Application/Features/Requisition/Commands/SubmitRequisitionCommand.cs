using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Requisition.Commands;

/// <summary>
/// submitRequisition (contract POST /requisitions/{requisitionId}/submit). Yalnız Draft→Submitted; Draft dışı → 409
/// INVALID_STATE. Submit'te MOD-0023 workflowInstanceId atanır (server-side seam). Idempotency-Key ile idempotent.
/// </summary>
public sealed record SubmitRequisitionCommand(
    string RequisitionId,
    string? IdempotencyKey) : IRequest<Response<RequisitionDto>>;
