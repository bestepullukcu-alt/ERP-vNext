using Diten.ProcurementService.Application.Features.Contract.Queries;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Contract.Handlers.QueryHandlers;

/// <summary>getContract (contract GET /api/contracts/{contractId}). Cross-tenant/LE → repository null → 404 NOT_FOUND
/// (sızıntı yok).</summary>
public sealed class GetContractByIdHandler : IRequestHandler<GetContractByIdQuery, Response<ContractDto>>
{
    private readonly IContractingRepository _repository;

    public GetContractByIdHandler(IContractingRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<ContractDto>> Handle(GetContractByIdQuery request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByContractIdAsync(request.ContractId, cancellationToken);
        if (entity is null)
        {
            return Response<ContractDto>.Fail("NOT_FOUND", 404);
        }

        return Response<ContractDto>.Success(ContractMapping.ToDto(entity));
    }
}
