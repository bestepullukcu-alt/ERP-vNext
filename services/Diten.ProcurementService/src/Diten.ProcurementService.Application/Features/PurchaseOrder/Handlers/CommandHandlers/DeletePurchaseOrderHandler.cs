using Diten.ProcurementService.Application.Features.PurchaseOrder.Commands;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using PoStatusEnum = Diten.ProcurementService.Domain.Entities.PoStatus;

namespace Diten.ProcurementService.Application.Features.PurchaseOrder.Handlers.CommandHandlers;

/// <summary>
/// Soft delete tek PO. Yalnız Draft silinebilir (Draft dışı → 409 INVALID_STATE). Cross-tenant/LE → 404 NOT_FOUND.
/// Hard delete YOK.
/// </summary>
public sealed class DeletePurchaseOrderHandler : IRequestHandler<DeletePurchaseOrderCommand, Response<bool>>
{
    private readonly IPurchaseOrderRepository _repository;

    public DeletePurchaseOrderHandler(IPurchaseOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(DeletePurchaseOrderCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByPoIdAsync(request.PoId, cancellationToken);
        if (entity is null)
        {
            return Response<bool>.Fail("NOT_FOUND", 404);
        }

        // Yalnız Draft PO soft-delete edilebilir (MOD-0141 §13).
        if (entity.Status != PoStatusEnum.Draft)
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
