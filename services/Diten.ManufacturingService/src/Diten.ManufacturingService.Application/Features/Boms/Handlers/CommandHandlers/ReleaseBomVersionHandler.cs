using Diten.ManufacturingService.Application.Common;
using Diten.ManufacturingService.Application.Features.Boms.Commands;
using Diten.ManufacturingService.Application.Interfaces;
using Diten.ManufacturingService.Domain.Entities;
using Diten.ManufacturingService.Domain.Repositories;
using Diten.ManufacturingService.Domain.Rules;
using Diten.Shared.Core;
using MediatR;

namespace Diten.ManufacturingService.Application.Features.Boms.Handlers.CommandHandlers;

/// <summary>
/// Taslağı yürürlüğe alır (ASSUMPTION-BOM-01: hemen, <c>effectiveFrom = now</c>). Aynı item'ın o anki Effective
/// sürümü AYNI işlemde Superseded olur ve <c>effectiveTo</c> = yeni <c>effectiveFrom</c>. Her iki değişikliğin geçmişi
/// de aynı işlemde yazılır. Değişiklik kontrolü referansı MOD-0209 seam'inden geçmeli; yürürlükteki yapıda döngü
/// oluşturacak sürüm reddedilir (409 <c>BOM_CYCLE</c>).
/// </summary>
public sealed class ReleaseBomVersionHandler(
    IBomRepository repository,
    IBomHistoryJournal journal,
    IChangeControlGate changeControl,
    ITenantContext tenant,
    ICurrentUserContext user,
    ICorrelationContext correlation,
    TimeProvider clock) : IRequestHandler<ReleaseBomVersionCommand, Response<BomView>>
{
    public async Task<Response<BomView>> Handle(ReleaseBomVersionCommand request, CancellationToken ct)
    {
        var tenantId = tenant.TenantId;
        var legalEntityId = tenant.LegalEntityId;
        var bom = await repository.GetByIdAsync(tenantId, legalEntityId, request.BomVersionId, ct);
        if (bom is null)
        {
            return Response<BomView>.Fail(BomErrorCodes.UnknownBom, 404);
        }

        if (bom.Status != BomStatus.Draft)
        {
            return Response<BomView>.Fail(BomErrorCodes.BomNotDraft, 409);
        }

        if (bom.Version != request.Body.RowVersion)
        {
            return Response<BomView>.Fail(BomErrorCodes.ConcurrencyConflict, 409);
        }

        var changeControlRef = request.Body.ChangeControlRef!;
        if (!await changeControl.IsAcceptedAsync(tenantId, legalEntityId, changeControlRef, ct))
        {
            return Response<BomView>.Fail(BomErrorCodes.ChangeControlRejected, 422);
        }

        var cycle = await BomRules.WouldCreateCycleAsync(
            bom.ItemId,
            bom.Components.Select(c => c.ComponentItemId),
            async items =>
            {
                var effective = await repository.GetCurrentEffectiveForItemsAsync(tenantId, legalEntityId, items, ct);
                return effective.ToDictionary(b => b.ItemId, b => (IReadOnlyList<Guid>)b.Components.Select(c => c.ComponentItemId).ToList());
            });
        if (cycle)
        {
            return Response<BomView>.Fail(BomErrorCodes.BomCycle, 409);
        }

        var now = clock.UtcNowMs();
        var updates = new List<(BomVersion, int)>();
        var history = new List<BomHistoryEntry>();

        var previous = await repository.GetCurrentEffectiveAsync(tenantId, legalEntityId, bom.ItemId, ct);
        if (previous is not null)
        {
            var previousExpected = previous.Version;
            previous.Status = BomStatus.Superseded;
            previous.EffectiveTo = now;
            previous.UpdatedAt = now;
            previous.UpdatedBy = user.UserId;
            previous.Version++;
            updates.Add((previous, previousExpected));
            history.Add(BomDraftSupport.History(previous, BomHistoryOperation.Superseded, BomStatus.Effective.ToString(), ["status", "effectiveTo"], user, correlation, now));
        }

        var expected = bom.Version;
        bom.Status = BomStatus.Effective;
        bom.EffectiveFrom = now;
        bom.EffectiveTo = null;
        bom.ChangeControlRef = changeControlRef;
        bom.ReleasedBy = user.UserId;
        bom.UpdatedAt = now;
        bom.UpdatedBy = user.UserId;
        bom.Version++;
        // Order matters: the previous version leaves Effective BEFORE this one enters it — the single-Effective unique
        // index sees each write inside the transaction and would refuse the reverse order.
        updates.Add((bom, expected));
        history.Insert(0, BomDraftSupport.History(bom, BomHistoryOperation.Released, BomStatus.Draft.ToString(), ["status", "effectiveFrom", "changeControlRef"], user, correlation, now));

        var result = await journal.CommitAsync(new BomChangeSet(tenantId, legalEntityId, null, updates, history), ct);
        return result == BomCommitResult.Committed
            ? Response<BomView>.Success(bom.ToView())
            : Response<BomView>.Fail(BomErrorCodes.ConcurrencyConflict, 409);
    }
}
