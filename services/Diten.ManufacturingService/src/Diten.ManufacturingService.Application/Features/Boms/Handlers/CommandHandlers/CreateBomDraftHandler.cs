using Diten.ManufacturingService.Application.Common;
using Diten.ManufacturingService.Application.Features.Boms.Commands;
using Diten.ManufacturingService.Application.Interfaces;
using Diten.ManufacturingService.Domain.Entities;
using Diten.ManufacturingService.Domain.Repositories;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ManufacturingService.Application.Features.Boms.Handlers.CommandHandlers;

/// <summary>
/// Yeni BOM taslağı. Revizyon no item başına max+1; taslak ve <c>Created</c> geçmiş kaydı tek işlemde yazılır
/// (<see cref="IBomHistoryJournal.CommitAsync"/>, AUD-001 yol c). Aynı item için eşzamanlı iki taslak aynı revizyon
/// numarasını alırsa eşsiz index birini reddeder → bir kez yeniden denenir.
/// </summary>
public sealed class CreateBomDraftHandler(
    IBomRepository repository,
    IBomHistoryJournal journal,
    IProductReferenceValidator products,
    ITenantContext tenant,
    ICurrentUserContext user,
    ICorrelationContext correlation,
    TimeProvider clock) : IRequestHandler<CreateBomDraftCommand, Response<BomView>>
{
    public async Task<Response<BomView>> Handle(CreateBomDraftCommand request, CancellationToken ct)
    {
        var body = request.Body;
        var lines = BomDraftSupport.ToLines(body.Components!);
        var referenceError = await BomDraftSupport.CheckReferencesAsync(body.ItemId, lines, products, ct);
        if (referenceError is not null)
        {
            return Response<BomView>.Fail(referenceError, 422);
        }

        for (var attempt = 0; attempt < 2; attempt++)
        {
            var now = clock.UtcNowMs();
            var bom = new BomVersion
            {
                TenantId = tenant.TenantId,
                LegalEntityId = tenant.LegalEntityId,
                ItemId = body.ItemId,
                RevisionNo = await repository.GetNextRevisionNoAsync(tenant.TenantId, tenant.LegalEntityId, body.ItemId, ct),
                Description = string.IsNullOrWhiteSpace(body.Description) ? null : body.Description.Trim(),
                Status = BomStatus.Draft,
                Components = lines,
                Routing = BomDraftSupport.ToRouting(body.Routing, null),
                CreatedAt = now,
                CreatedBy = user.UserId,
                Version = 1
            };

            var history = BomDraftSupport.History(bom, BomHistoryOperation.Created, null, null, user, correlation, now);
            var result = await journal.CommitAsync(new BomChangeSet(tenant.TenantId, tenant.LegalEntityId, bom, [], [history]), ct);
            if (result == BomCommitResult.Committed)
            {
                return Response<BomView>.Success(bom.ToView(), 201);
            }
        }

        return Response<BomView>.Fail(BomErrorCodes.ConcurrencyConflict, 409);
    }
}
