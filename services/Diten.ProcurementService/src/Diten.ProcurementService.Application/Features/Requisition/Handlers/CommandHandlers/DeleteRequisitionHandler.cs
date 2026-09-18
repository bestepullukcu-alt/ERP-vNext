using Diten.ProcurementService.Application.Features.Requisition.Commands;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using RequisitionStatusEnum = Diten.ProcurementService.Domain.Entities.RequisitionStatus;

namespace Diten.ProcurementService.Application.Features.Requisition.Handlers.CommandHandlers;

/// <summary>
/// Soft delete tek requisition. Yalnız Draft silinebilir (Draft dışı → 409 INVALID_STATE). Cross-tenant/LE → 404
/// NOT_FOUND. Hard delete YOK.
/// </summary>
public sealed class DeleteRequisitionHandler : IRequestHandler<DeleteRequisitionCommand, Response<bool>>
{
    private readonly IRequisitionRepository _repository;

    public DeleteRequisitionHandler(IRequisitionRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(DeleteRequisitionCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByRequisitionIdAsync(request.RequisitionId, cancellationToken);
        if (entity is null)
        {
            return Response<bool>.Fail("NOT_FOUND", 404);
        }

        // Yalnız Draft requisition soft-delete edilebilir (MOD-0141 §13).
        if (entity.Status != RequisitionStatusEnum.Draft)
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
