using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.WavePlanning;
using Diten.InventoryService.Application.Features.WavePlanning.Services;
using Diten.InventoryService.Application.Features.Waves.Commands;
using Diten.InventoryService.Domain.WavePlanning.Entities;
using Diten.InventoryService.Domain.WavePlanning.Enums;
using Diten.InventoryService.Domain.WavePlanning.Repositories;
using Diten.InventoryService.Domain.WavePlanning.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Waves.Handlers.CommandHandlers;

/// <summary>
/// <c>removeWaveOrders</c> (pack §8.2 rule 4; draft §3.6): Draft only. A Planned membership of the wave becomes <c>Removed</c>
/// (<c>Active = false</c>, never deleted); a row that is not a member is <c>NotMember</c> — not an error. When no row is a member nothing is
/// written (no event, no version, no central record). The reason is optional. It stays allowed in a warehouse whose wave management was
/// switched off (pack §8.2 rule 1).
/// </summary>
public sealed class RemoveWaveOrdersHandler : WaveCommandHandler, IRequestHandler<RemoveWaveOrdersCommand, Response<WaveOrdersResultDto>>
{
    public RemoveWaveOrdersHandler(IWaveRepository repository, WaveSupport support) : base(repository, support)
    {
    }

    public async Task<Response<WaveOrdersResultDto>> Handle(RemoveWaveOrdersCommand request, CancellationToken ct)
    {
        const string op = WaveContract.OpRemoveWaveOrders;
        var reason = WaveSupport.Trimmed(request.Reason);
        var ids = request.OutboundIds;
        var fingerprint = WaveSupport.Fingerprint(("op", op), ("waveId", request.WaveId.ToString("D")),
            ("outboundIds", WaveSupport.List(ids.Select(x => x.ToString("D")).OrderBy(x => x, StringComparer.Ordinal))), ("reason", reason));
        if (await ReplayAsync(request.WaveId, ids, op, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var wave = await Repository.FindAsync(request.WaveId, ct);
        if (wave is null) return WaveSupport.NotFound<WaveOrdersResultDto>();
        if (wave.Version != request.ExpectedVersion) return WaveSupport.Conflict<WaveOrdersResultDto>();
        if (!WaveRules.IsDraft(wave)) return WaveSupport.InvalidState<WaveOrdersResultDto>(wave.Status);

        var active = await Repository.ActiveOrdersAsync(wave.Id, ct);
        var members = active.Where(m => m.State == WaveOrderState.Planned && ids.Contains(m.OutboundId)).ToList();
        var current = new WaveTotals(active.Count, active.Sum(o => o.LineCount), active.Sum(o => o.PlannedTaskCount));
        if (members.Count == 0)
        {
            return Response<WaveOrdersResultDto>.Success(new WaveOrdersResultDto(WaveReads.Dto(wave, current),
                ids.Select(id => new WaveOrderRowDto(id.ToString("D"), WaveContract.RowNotMember)).ToList()));
        }

        var now = Support.Now();
        var copy = Copy(wave);
        copy.Version = wave.Version + 1;
        copy.UpdatedAt = now;
        var data = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["outboundIds"] = WaveSupport.List(members.Select(o => o.OutboundId.ToString("D"))),
            ["outboundNumbers"] = WaveSupport.List(members.Select(o => o.OutboundNumber)),
            ["reason"] = reason
        };
        var removed = Support.Event(copy, WaveEventKind.OrdersRemoved, now, data, request.IdempotencyKey, op, fingerprint);
        var removedIds = members.Select(m => m.OutboundId).ToList();
        var saved = await SaveKeyedAsync(copy, wave.Version, [removed],
            async (session, tx) => await session.RemovePlannedOrdersAsync(wave.Id, removedIds, Support.UserId, now, reason, tx), ct);
        if (!saved) return await ReplayAsync(request.WaveId, ids, op, request.IdempotencyKey, fingerprint, ct) ?? WaveSupport.Conflict<WaveOrdersResultDto>();

        var totals = new WaveTotals(current.OrderCount - members.Count, current.LineCount - members.Sum(o => o.LineCount),
            current.PlannedTaskCount - members.Sum(o => o.PlannedTaskCount));
        var closed = members.ToDictionary(m => m.OutboundId, m => Closed(m, now, reason));
        return Response<WaveOrdersResultDto>.Success(new WaveOrdersResultDto(WaveReads.Dto(copy, totals), Rows(ids, closed)));
    }

    private WaveOrder Closed(WaveOrder m, DateTimeOffset at, string? reason) => new()
    {
        Id = m.Id, WaveId = m.WaveId, WarehouseId = m.WarehouseId, OutboundId = m.OutboundId, OutboundNumber = m.OutboundNumber, LineCount = m.LineCount,
        AllocationCount = m.AllocationCount, PlannedTaskCount = m.PlannedTaskCount, State = WaveOrderState.Removed, Active = false, AddedBy = m.AddedBy,
        AddedAt = m.AddedAt, AddedVia = m.AddedVia, ClosedBy = Support.UserId, ClosedAt = at, CloseReason = reason, Version = m.Version + 1
    };

    private static List<WaveOrderRowDto> Rows(IReadOnlyList<Guid> ids, IReadOnlyDictionary<Guid, WaveOrder> removed)
        => ids.Select(id => removed.TryGetValue(id, out var order)
            ? new WaveOrderRowDto(id.ToString("D"), WaveContract.RowRemoved, WaveReads.Order(order))
            : new WaveOrderRowDto(id.ToString("D"), WaveContract.RowNotMember)).ToList();

    /// <summary>The replay of a remove: the current wave; a row is Removed when the order's latest membership in the wave is Removed.</summary>
    private async Task<Response<WaveOrdersResultDto>?> ReplayAsync(Guid waveId, IReadOnlyList<Guid> ids, string op, string key, string fingerprint, CancellationToken ct)
    {
        switch (await KeyUsedAsync(waveId, op, key, fingerprint, ct))
        {
            case null:
                return null;
            case false:
                return WaveSupport.KeyReused<WaveOrdersResultDto>();
        }

        if (await CurrentAsync(waveId, ct) is not { } wave) return WaveSupport.NotFound<WaveOrdersResultDto>();
        var removed = (await Repository.MembershipsAsync(waveId, ids.ToList(), ct)).GroupBy(m => m.OutboundId).Select(g => g.First())
            .Where(m => m.State == WaveOrderState.Removed).ToDictionary(m => m.OutboundId);
        return Response<WaveOrdersResultDto>.Success(new WaveOrdersResultDto(wave, Rows(ids, removed)));
    }
}
