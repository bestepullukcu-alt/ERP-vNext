using Diten.ManufacturingService.Application.Common;
using Diten.ManufacturingService.Application.Features.Boms.Commands;
using Diten.ManufacturingService.Application.Interfaces;
using Diten.ManufacturingService.Domain.Entities;
using Diten.ManufacturingService.Domain.Repositories;
using Diten.ManufacturingService.Domain.Rules;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ManufacturingService.Application.Features.Boms.Handlers.CommandHandlers;

/// <summary>Yalnız taslak düzenlenir; geçmişe değişen alanların ADLARI yazılır. Eski <c>rowVersion</c> → 409.</summary>
public sealed class UpdateBomDraftHandler(
    IBomRepository repository,
    IBomHistoryJournal journal,
    IProductReferenceValidator products,
    ITenantContext tenant,
    ICurrentUserContext user,
    ICorrelationContext correlation,
    TimeProvider clock) : IRequestHandler<UpdateBomDraftCommand, Response<BomView>>
{
    public async Task<Response<BomView>> Handle(UpdateBomDraftCommand request, CancellationToken ct)
    {
        var bom = await repository.GetByIdAsync(tenant.TenantId, tenant.LegalEntityId, request.BomVersionId, ct);
        if (bom is null)
        {
            return Response<BomView>.Fail(BomErrorCodes.UnknownBom, 404);
        }

        if (bom.Status != BomStatus.Draft)
        {
            return Response<BomView>.Fail(BomErrorCodes.BomNotDraft, 409);
        }

        var body = request.Body;
        if (bom.Version != body.RowVersion)
        {
            return Response<BomView>.Fail(BomErrorCodes.ConcurrencyConflict, 409);
        }

        var lines = BomDraftSupport.ToLines(body.Components!);
        var referenceError = await BomDraftSupport.CheckReferencesAsync(bom.ItemId, lines, products, ct);
        if (referenceError is not null)
        {
            return Response<BomView>.Fail(referenceError, 422);
        }

        var description = string.IsNullOrWhiteSpace(body.Description) ? null : body.Description.Trim();
        var routing = BomDraftSupport.ToRouting(body.Routing, bom.Routing?.RoutingId);
        var changed = BomRules.ChangedFields(bom, description, lines, routing);
        if (changed.Count == 0)
        {
            return Response<BomView>.Success(bom.ToView());
        }

        var now = clock.UtcNowMs();
        var expected = bom.Version;
        bom.Description = description;
        bom.Components = lines;
        bom.Routing = routing;
        bom.UpdatedAt = now;
        bom.UpdatedBy = user.UserId;
        bom.Version++;

        var history = BomDraftSupport.History(bom, BomHistoryOperation.Updated, BomStatus.Draft.ToString(), changed, user, correlation, now);
        var result = await journal.CommitAsync(new BomChangeSet(tenant.TenantId, tenant.LegalEntityId, null, [(bom, expected)], [history]), ct);
        return result == BomCommitResult.Committed
            ? Response<BomView>.Success(bom.ToView())
            : Response<BomView>.Fail(BomErrorCodes.ConcurrencyConflict, 409);
    }
}
