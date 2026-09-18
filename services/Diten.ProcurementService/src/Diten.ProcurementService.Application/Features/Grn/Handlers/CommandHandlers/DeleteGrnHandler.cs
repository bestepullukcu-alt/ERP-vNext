using Diten.ProcurementService.Application.Features.Grn.Commands;
using Diten.ProcurementService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;
using GrnStatusEnum = Diten.ProcurementService.Domain.Entities.GrnStatus;

namespace Diten.ProcurementService.Application.Features.Grn.Handlers.CommandHandlers;

/// <summary>
/// Soft delete tek GRN (ASSUMPTION-GRN-03). Yalnız Draft silinebilir; Posted GRN silinemez (→ 409 INVALID_STATE) —
/// düzeltme reverse ile yapılır (append-only). Cross-tenant/LE → 404 NOT_FOUND. Hard delete YOK.
/// </summary>
public sealed class DeleteGrnHandler : IRequestHandler<DeleteGrnCommand, Response<bool>>
{
    private readonly IGrnRepository _repository;

    public DeleteGrnHandler(IGrnRepository repository)
    {
        _repository = repository;
    }

    public async Task<Response<bool>> Handle(DeleteGrnCommand request, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByGrnIdAsync(request.GrnId, cancellationToken);
        if (entity is null)
        {
            return Response<bool>.Fail("NOT_FOUND", 404);
        }

        // Posted/Reversed GRN silinemez (INVENTORY hareketi var; düzeltme reverse ile). Yalnız Draft.
        if (entity.Status != GrnStatusEnum.Draft)
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
