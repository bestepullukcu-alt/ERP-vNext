using Diten.ProcurementService.Application.Features.Contract.Commands;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using ContractStatusEnum = Diten.ProcurementService.Domain.Entities.ContractStatus;

namespace Diten.ProcurementService.Application.Features.Contract.Handlers.CommandHandlers;

/// <summary>
/// Soft delete tek sözleşme (ASSUMPTION-0144-01 additive). Yalnız Draft silinebilir; aktive edilmiş/onaya gönderilmiş
/// sözleşme (InReview/Active/Expired/Terminated) silinemez (→ 409 INVALID_STATE) — approval trail + aktive kayıt
/// kalıcıdır. Cross-tenant/LE → 404 NOT_FOUND. Hard delete YOK.
/// </summary>
public sealed class DeleteContractHandler : IRequestHandler<DeleteContractCommand, Response<bool>>
{
    private readonly IContractingRepository _repository;

    public DeleteContractHandler(IContractingRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(DeleteContractCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByContractIdAsync(request.ContractId, cancellationToken);
        if (entity is null)
        {
            return Response<bool>.Fail("NOT_FOUND", 404);
        }

        if (entity.Status != ContractStatusEnum.Draft)
        {
            return Response<bool>.Fail("INVALID_STATE", 409);
        }

        var ok = await _repository.DeleteAsync(entity.Id, cancellationToken);
        if (!ok)
        {
            return Response<bool>.Fail("NOT_FOUND", 404);
        }

        return Response<bool>.Success(true);
    }
}
