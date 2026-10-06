using Diten.ProcurementService.Application.Features.Contract.Commands;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using ContractStatusEnum = Diten.ProcurementService.Domain.Entities.ContractStatus;

namespace Diten.ProcurementService.Application.Features.Contract.Handlers.CommandHandlers;

/// <summary>
/// terminateContract (ASSUMPTION-0144-02 additive). Active → Terminated geçişi (optimistic concurrency). Active olmayan
/// (Draft/InReview/Expired/Terminated) → 409 INVALID_STATE. Bilinmeyen sözleşme / cross-tenant-LE → 404.
/// </summary>
public sealed class TerminateContractHandler : IRequestHandler<TerminateContractCommand, Response<ContractDto>>
{
    private readonly IContractingRepository _repository;

    public TerminateContractHandler(IContractingRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<ContractDto>> Handle(TerminateContractCommand request, CancellationToken cancellationToken)
    {
        var contract = await _repository.GetByContractIdAsync(request.ContractId, cancellationToken);
        if (contract is null)
        {
            return Response<ContractDto>.Fail("NOT_FOUND", 404);
        }

        if (contract.Status != ContractStatusEnum.Active)
        {
            return Response<ContractDto>.Fail("INVALID_STATE", 409);
        }

        var expectedVersion = contract.Version;
        contract.Status = ContractStatusEnum.Terminated;

        var ok = await _repository.UpdateAsync(contract, expectedVersion, cancellationToken);
        if (!ok)
        {
            return Response<ContractDto>.Fail("INVALID_STATE", 409);
        }

        return Response<ContractDto>.Success(ContractMapping.ToDto(contract));
    }
}
