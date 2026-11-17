using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.WavePlanning;
using Diten.InventoryService.Application.Features.WavePlanning.Services;
using Diten.InventoryService.Application.Features.Waves.Commands;
using Diten.InventoryService.Domain.WavePlanning.Enums;
using Diten.InventoryService.Domain.WavePlanning.Repositories;
using Diten.InventoryService.Domain.WavePlanning.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Waves.Handlers.CommandHandlers;

/// <summary>
/// <c>cancelWave</c> (pack §8.3; AC-8): the reason is required (400 <c>WAVE_REASON_REQUIRED</c>); ONLY a Draft is cancelled (a released wave →
/// 409 <c>WAVE_INVALID_STATE</c>; its way out is the recall, D-9). The wave becomes <c>Cancelled</c> and is KEPT (never deleted — GET still
/// answers it); its Planned memberships become <c>Removed</c> in the same transaction; one <c>Cancelled</c> event carries the reason.
/// </summary>
public sealed class CancelWaveHandler : WaveCommandHandler, IRequestHandler<CancelWaveCommand, Response<WaveDto>>
{
    public CancelWaveHandler(IWaveRepository repository, WaveSupport support) : base(repository, support)
    {
    }

    public async Task<Response<WaveDto>> Handle(CancelWaveCommand request, CancellationToken ct)
    {
        const string op = WaveContract.OpCancelWave;
        if (WaveRules.Reason(request.Reason) is not { } reason) return WaveSupport.ReasonRequired<WaveDto>();
        var fingerprint = WaveSupport.Fingerprint(("op", op), ("waveId", request.WaveId.ToString("D")), ("reason", reason));
        if (await ReplayAsync(request.WaveId, op, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var wave = await Repository.FindAsync(request.WaveId, ct);
        if (wave is null) return WaveSupport.NotFound<WaveDto>();
        if (wave.Version != request.ExpectedVersion) return WaveSupport.Conflict<WaveDto>();
        if (!WaveRules.IsDraft(wave)) return WaveSupport.InvalidState<WaveDto>(wave.Status);

        var now = Support.Now();
        var members = await Repository.ActiveOrdersAsync(wave.Id, ct);
        var copy = Copy(wave);
        copy.Status = WaveStatus.Cancelled;
        copy.CancelledBy = Support.UserId;
        copy.CancelledAt = now;
        copy.CancelReason = reason;
        copy.Version = wave.Version + 1;
        copy.UpdatedAt = now;

        var data = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["previousStatus"] = wave.Status.ToString(), ["reason"] = reason, ["removedOrderCount"] = WaveSupport.Int(members.Count),
            ["removedOutboundNumbers"] = WaveSupport.List(members.Select(m => m.OutboundNumber).OrderBy(x => x, StringComparer.Ordinal))
        };
        var cancelled = Support.Event(copy, WaveEventKind.Cancelled, now, data, request.IdempotencyKey, op, fingerprint);
        var saved = await SaveKeyedAsync(copy, wave.Version, [cancelled],
            async (session, tx) => await session.RemovePlannedOrdersAsync(wave.Id, null, Support.UserId, now, reason, tx), ct);
        if (saved) return Response<WaveDto>.Success(WaveReads.Dto(copy, WaveTotals.Empty));
        return await ReplayAsync(request.WaveId, op, request.IdempotencyKey, fingerprint, ct) ?? WaveSupport.Conflict<WaveDto>();
    }
}
