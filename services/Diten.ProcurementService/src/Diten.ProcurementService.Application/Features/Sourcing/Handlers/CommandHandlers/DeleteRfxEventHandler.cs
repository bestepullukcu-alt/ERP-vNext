using Diten.ProcurementService.Application.Features.Sourcing.Commands;
using Diten.ProcurementService.Domain.Entities;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ProcurementService.Application.Features.Sourcing.Handlers.CommandHandlers;

/// <summary>
/// Soft delete tek RFx. Yalnız Draft silinebilir (Draft dışı → 409 INVALID_STATE). Cross-tenant/LE → 404 NOT_FOUND.
/// Hard delete YOK.
/// </summary>
public sealed class DeleteRfxEventHandler : IRequestHandler<DeleteRfxEventCommand, Response<bool>>
{
    private readonly IRfxRepository _repository;

    public DeleteRfxEventHandler(IRfxRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(DeleteRfxEventCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByRfxIdAsync(request.RfxId, cancellationToken);
        if (entity is null)
        {
            return Response<bool>.Fail("NOT_FOUND", 404);
        }

        // Yalnız Draft RFx soft-delete edilebilir (MOD-0145 §13).
        if (entity.Status != RfxStatus.Draft)
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
