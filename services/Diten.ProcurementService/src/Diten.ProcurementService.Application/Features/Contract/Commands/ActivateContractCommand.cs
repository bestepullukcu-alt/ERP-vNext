using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Contract.Commands;

/// <summary>
/// activateContract (contract POST /api/contracts/{contractId}/activate). Draft/InReview → Active geçişi + approval
/// trail (MOD-0023; workflowInstanceId set — ASSUMPTION-0144-05). Idempotency-Key ile idempotent (replay → ikinci
/// geçiş YOK). Zaten Active/Expired/Terminated → 409 INVALID_STATE (sessiz overwrite YOK). Bilinmeyen sözleşme /
/// cross-tenant-LE → 404 NOT_FOUND.
/// </summary>
public sealed record ActivateContractCommand(
    string ContractId,
    string? IdempotencyKey) : IRequest<Response<ContractDto>>;
