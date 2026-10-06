using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Contract.Commands;

/// <summary>Soft delete tek sözleşme (public ContractId; pack §14 procurement.contracts.delete; ASSUMPTION-0144-01
/// additive). Yalnız Draft silinebilir; InReview/Active/Expired/Terminated silinemez (→ 409 INVALID_STATE) —
/// aktive edilmiş sözleşme + approval trail kalıcıdır. Hard delete YOK.</summary>
public sealed record DeleteContractCommand(string ContractId) : IRequest<Response<bool>>;
