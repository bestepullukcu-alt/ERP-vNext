using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Contract.Commands;

/// <summary>Toplu soft delete (public ContractId listesi; pack §14 procurement.contracts.bulk-delete;
/// ASSUMPTION-0144-01 additive). Yalnız Draft olanlar silinir; silinen adedini döner. Hard delete YOK.</summary>
public sealed record BulkDeleteContractCommand(List<string> ContractIds) : IRequest<Response<int>>;
