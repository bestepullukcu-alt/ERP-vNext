using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Application.Features.CycleCounts.Commands;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using Diten.InventoryService.Domain.CycleCounting.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.CycleCounts.Handlers.CommandHandlers;

/// <summary>
/// <c>cancelCycleCount</c> (pack §8.1, §8.3, F-1; AC-10): the reason is required (400 <c>COUNT_REASON_REQUIRED</c>). Draft → <c>Cancelled</c> +
/// soft delete (<c>IsDeleted</c>, <c>DeletedAt</c>; GET 404 after); Released / Counting → <c>Cancelled</c>, kept, the count's bin claims
/// released (<c>Open = false</c>) in the same transaction; Submitted and later → 409 <c>COUNT_INVALID_STATE</c>. One <c>Cancelled</c> event
/// (<c>Data.softDeleted</c>), no separate Deleted kind (02/F-4).
/// </summary>
public sealed class CancelCycleCountHandler : CycleCountCommandHandler, IRequestHandler<CancelCycleCountCommand, Response<CycleCountDto>>
{
    public CancelCycleCountHandler(ICycleCountRepository repository, CycleCountSupport support) : base(repository, support)
    {
    }

    public async Task<Response<CycleCountDto>> Handle(CancelCycleCountCommand request, CancellationToken ct)
    {
        const string op = CycleCountContract.OpCancelCycleCount;
        if (CycleCountRules.Reason(request.Reason) is not { } reason) return CycleCountSupport.ReasonRequired<CycleCountDto>();
        var fingerprint = CycleCountSupport.Fingerprint(("op", op), ("countId", request.CountId.ToString("D")), ("reason", reason));
        if (await ReplayAsync(request.CountId, op, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var count = await Repository.FindAsync(request.CountId, ct);
        if (count is null) return CycleCountSupport.NotFound<CycleCountDto>();
        if (count.Version != request.ExpectedVersion) return CycleCountSupport.Conflict<CycleCountDto>();
        if (!CycleCountRules.CanCancel(count)) return CycleCountSupport.InvalidState<CycleCountDto>(count.Status);

        var now = Support.Now();
        var deletes = CycleCountRules.CancelDeletes(count);
        var copy = Copy(count);
        copy.Status = CycleCountStatus.Cancelled;
        copy.CancelledBy = Support.UserId;
        copy.CancelledAt = now;
        copy.CancelReason = reason;
        copy.Version = count.Version + 1;
        copy.UpdatedAt = now;
        if (deletes)
        {
            copy.IsDeleted = true;
            copy.DeletedAt = now;
        }

        var data = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["previousStatus"] = count.Status.ToString(), ["reason"] = reason, ["softDeleted"] = deletes ? "true" : "false",
            ["releasedBinClaims"] = deletes ? "0" : CycleCountSupport.Int(count.ScopeBinIds.Count)
        };
        var cancelled = Support.Event(copy, CycleCountEventKind.Cancelled, now, data, request.IdempotencyKey, op, fingerprint);
        return await SaveKeyedAsync(copy, count.Version, [cancelled], op, request.IdempotencyKey, fingerprint,
            deletes ? null : async (session, tx) => await session.ReleaseBinClaimsAsync(count.Id, now, tx), ct);
    }
}
