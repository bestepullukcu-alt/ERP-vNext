using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Contract.Queries;

/// <summary>getContract (contract GET /api/contracts/{contractId}). Cross-tenant/LE → 404 NOT_FOUND (sızıntı yok).</summary>
public sealed record GetContractByIdQuery(string ContractId) : IRequest<Response<ContractDto>>;
