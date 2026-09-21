using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Contract.Commands;

/// <summary>
/// terminateContract (ASSUMPTION-0144-02 additive; procurement.contracts.update altında). Active → Terminated geçişi.
/// Active olmayan (Draft/InReview/Expired/Terminated) → 409 INVALID_STATE. Bilinmeyen sözleşme / cross-tenant-LE → 404.
/// </summary>
public sealed record TerminateContractCommand(string ContractId) : IRequest<Response<ContractDto>>;
