using Diten.ManufacturingService.Application.Common;
using Diten.ManufacturingService.Application.Features.Boms.Commands;
using Diten.ManufacturingService.Application.Interfaces;
using Diten.ManufacturingService.Domain.Entities;
using Diten.ManufacturingService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ManufacturingService.Application.Features.Boms.Handlers.CommandHandlers;

/// <summary>Yalnız taslak silinir (soft); yürürlükteki/geçmiş sürüm GxP kaydıdır, silinemez (409).</summary>
public sealed class DeleteBomDraftHandler(
    IBomRepository repository,
    IBomHistoryJournal journal,
    ITenantContext tenant,
    ICurrentUserContext user,
    ICorrelationContext correlation,
    TimeProvider clock) : IRequestHandler<DeleteBomDraftCommand, Response<NoContent>>
{
    public async Task<Response<NoContent>> Handle(DeleteBomDraftCommand request, CancellationToken ct)
    {
        var bom = await repository.GetByIdAsync(tenant.TenantId, tenant.LegalEntityId, request.BomVersionId, ct);
        if (bom is null)
        {
            return Response<NoContent>.Fail(BomErrorCodes.UnknownBom, 404);
        }

        if (bom.Status != BomStatus.Draft)
        {
            return Response<NoContent>.Fail(BomErrorCodes.BomNotDraft, 409);
        }

        if (bom.Version != request.RowVersion)
        {
            return Response<NoContent>.Fail(BomErrorCodes.ConcurrencyConflict, 409);
        }

        var now = clock.UtcNowMs();
        var expected = bom.Version;
        bom.IsDeleted = true;
        bom.DeletedAt = now;
        bom.UpdatedAt = now;
        bom.UpdatedBy = user.UserId;
        bom.Version++;

        var history = BomDraftSupport.History(bom, BomHistoryOperation.Deleted, BomStatus.Draft.ToString(), ["isDeleted"], user, correlation, now);
        var result = await journal.CommitAsync(new BomChangeSet(tenant.TenantId, tenant.LegalEntityId, null, [(bom, expected)], [history]), ct);
        return result == BomCommitResult.Committed
            ? Response<NoContent>.SuccessWithoutData(204)
            : Response<NoContent>.Fail(BomErrorCodes.ConcurrencyConflict, 409);
    }
}
