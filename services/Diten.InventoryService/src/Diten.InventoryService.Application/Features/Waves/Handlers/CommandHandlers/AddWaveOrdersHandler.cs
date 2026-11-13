using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.WavePlanning;
using Diten.InventoryService.Application.Features.WavePlanning.Ports;
using Diten.InventoryService.Application.Features.WavePlanning.Services;
using Diten.InventoryService.Application.Features.Waves.Commands;
using Diten.InventoryService.Domain.WavePlanning.Entities;
using Diten.InventoryService.Domain.WavePlanning.Enums;
using Diten.InventoryService.Domain.WavePlanning.Repositories;
using Diten.InventoryService.Domain.WavePlanning.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Waves.Handlers.CommandHandlers;

/// <summary>
/// <c>addWaveOrders</c> (pack §8.2 rules 1–3, §8.5; AC-11, AC-13; draft §3.6). Order: the key → If-Match → Draft → the warehouse still wave
/// managed → every row eligible (ALL or NOTHING: one refused row refuses the request, every refused row is in <c>details.orders[]</c>; 409 when
/// all of them are in another wave, otherwise 422) → the technical ceiling (422 <c>WAVE_TOO_LARGE</c>) → ONE transaction: the gate write on the
/// warehouse setting (a warehouse switched off meanwhile → 422, nothing written), the wave (version + 1), the memberships and the
/// <c>OrdersAdded</c> event. A business limit that is exceeded is WRITTEN — the answer's wave carries <c>limitWarnings</c> (D-7). The partial
/// unique index refuses the second of two concurrent adds of one order (D-8).
/// </summary>
public sealed class AddWaveOrdersHandler : WaveCommandHandler, IRequestHandler<AddWaveOrdersCommand, Response<WaveOrdersResultDto>>
{
    private readonly WaveChecks _checks;
    private readonly IWaveInventoryReader _inventory;

    public AddWaveOrdersHandler(IWaveRepository repository, WaveSupport support, WaveChecks checks, IWaveInventoryReader inventory) : base(repository, support)
    {
        _checks = checks;
        _inventory = inventory;
    }

    public async Task<Response<WaveOrdersResultDto>> Handle(AddWaveOrdersCommand request, CancellationToken ct)
    {
        const string op = WaveContract.OpAddWaveOrders;
        var via = Enum.Parse<WaveOrderAddedVia>(request.Via ?? nameof(WaveOrderAddedVia.Manual), ignoreCase: false);
        var reason = WaveSupport.Trimmed(request.Reason);
        var ids = request.OutboundIds;
        var fingerprint = WaveSupport.Fingerprint(("op", op), ("waveId", request.WaveId.ToString("D")),
            ("outboundIds", WaveSupport.List(ids.Select(x => x.ToString("D")).OrderBy(x => x, StringComparer.Ordinal))), ("via", via.ToString()), ("reason", reason));
        if (await ReplayAsync(request.WaveId, ids, op, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var wave = await Repository.FindAsync(request.WaveId, ct);
        if (wave is null) return WaveSupport.NotFound<WaveOrdersResultDto>();
        if (wave.Version != request.ExpectedVersion) return WaveSupport.Conflict<WaveOrdersResultDto>();
        if (!WaveRules.IsDraft(wave)) return WaveSupport.InvalidState<WaveOrdersResultDto>(wave.Status);
        var (_, settingsRefusal) = await _checks.ManagedTimeZoneAsync<WaveOrdersResultDto>(wave.WarehouseId, ct);
        if (settingsRefusal is not null) return settingsRefusal;

        Dictionary<Guid, WaveOutbound> outbounds;
        try
        {
            outbounds = (await _inventory.OutboundsAsync(ids.ToList(), ct)).ToDictionary(o => o.Id);
        }
        catch (WaveInventoryUnavailableException)
        {
            return WaveSupport.InventoryUnavailable<WaveOrdersResultDto>();
        }

        var memberships = (await Repository.ActiveMembershipsAsync(ids.ToList(), ct)).ToDictionary(m => m.OutboundId);
        var refused = new List<(Guid OutboundId, string Code)>();
        foreach (var id in ids)
        {
            if (RowRefusal(id, wave, outbounds.GetValueOrDefault(id), memberships.GetValueOrDefault(id)) is { } code) refused.Add((id, code));
        }

        if (refused.Count > 0) return Refused(refused);

        var now = Support.Now();
        var added = ids.Select(id => outbounds[id]).Select(o => new WaveOrder
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            Version = 1,
            WaveId = wave.Id,
            WarehouseId = wave.WarehouseId,
            OutboundId = o.Id,
            OutboundNumber = o.Number,
            LineCount = o.LineCount,
            AllocationCount = o.AllocationCount,
            PlannedTaskCount = o.PlannedTaskCount,
            State = WaveOrderState.Planned,
            Active = true,
            AddedBy = Support.UserId,
            AddedAt = now,
            AddedVia = via
        }).ToList();

        var current = await TotalsAsync(wave.Id, ct);
        var totals = new WaveTotals(current.OrderCount + added.Count, current.LineCount + added.Sum(o => o.LineCount),
            current.PlannedTaskCount + added.Sum(o => o.PlannedTaskCount));
        if (WaveRules.ExceedsTechnicalLimit(totals))
        {
            return WaveSupport.Fail<WaveOrdersResultDto>(WaveErrorCodes.TooLarge,
                $"A wave holds at most {WaveRules.TechnicalMaxOrders} orders and {WaveRules.TechnicalMaxPlannedTasks} planned pick tasks.", 422,
                new Dictionary<string, object?> { ["orderCount"] = totals.OrderCount, ["plannedTaskCount"] = totals.PlannedTaskCount });
        }

        var copy = Copy(wave);
        copy.Version = wave.Version + 1;
        copy.UpdatedAt = now;
        var warnings = WaveRules.LimitWarnings(copy.MaxOrders, copy.MaxLines, totals);
        var data = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["outboundIds"] = WaveSupport.List(added.Select(o => o.OutboundId.ToString("D"))),
            ["outboundNumbers"] = WaveSupport.List(added.Select(o => o.OutboundNumber)),
            ["addedVia"] = via.ToString(),
            ["orderCount"] = WaveSupport.Int(totals.OrderCount),
            ["lineCount"] = WaveSupport.Int(totals.LineCount),
            ["limitWarnings"] = warnings.Count == 0 ? null : WaveSupport.List(warnings.Select(w => $"{w.Kind}:{w.Actual}/{w.Limit}")),
            ["reason"] = reason
        };
        var ordersAdded = Support.Event(copy, WaveEventKind.OrdersAdded, now, data, request.IdempotencyKey, op, fingerprint);

        var lostToAnotherWave = false;
        try
        {
            var saved = await SaveKeyedAsync(copy, wave.Version, [ordersAdded], async (session, tx) =>
            {
                // pack §8.19 "Sıralama": the warehouse must STILL be wave managed inside this transaction
                await session.PassGateAsync(wave.WarehouseId, tx);
                await session.InsertOrdersAsync(added, tx);
            }, ct);
            if (saved)
            {
                return Response<WaveOrdersResultDto>.Success(new WaveOrdersResultDto(WaveReads.Dto(copy, totals),
                    added.Select(o => new WaveOrderRowDto(o.OutboundId.ToString("D"), WaveContract.RowAdded, WaveReads.Order(o))).ToList()));
            }
        }
        catch (WaveGateClosedException)
        {
            return WaveSupport.SettingsRequired<WaveOrdersResultDto>();
        }
        catch (WaveDuplicateException ex) when (ex.Index == WaveUniqueIndex.ActiveOrder)
        {
            // D-8: a concurrent add took one of these orders — nothing of this request was written
            lostToAnotherWave = true;
        }

        if (await ReplayAsync(request.WaveId, ids, op, request.IdempotencyKey, fingerprint, ct) is { } twin) return twin;
        return lostToAnotherWave
            ? WaveSupport.Fail<WaveOrdersResultDto>(WaveErrorCodes.OrderInOtherWave, "An order was added to another wave meanwhile; nothing was added.", 409)
            : WaveSupport.Conflict<WaveOrdersResultDto>();
    }

    /// <summary>Pack §8.2 rule 2 for one row: null = eligible; otherwise the row code of draft §3.6.</summary>
    private static string? RowRefusal(Guid outboundId, Wave wave, WaveOutbound? outbound, WaveOrder? membership)
    {
        if (outbound is null) return WaveContract.OrderNotFound;
        if (outbound.WarehouseId != wave.WarehouseId) return WaveContract.OrderOtherWarehouse;
        if (membership is not null) return membership.WaveId == wave.Id ? WaveContract.OrderAlreadyInWave : WaveContract.OrderInOtherWave;
        if (outbound.Status != WaveRules.EligibleOutboundStatus) return WaveContract.OrderNotAllocated;
        return outbound.PickIssuanceState != WaveRules.DeferredPickIssuance ? WaveContract.OrderNotDeferred : null;
    }

    /// <summary>Draft §3.6: 409 only when EVERY refused row is in another wave; otherwise 422 — both with every refused row.</summary>
    private static Response<WaveOrdersResultDto> Refused(List<(Guid OutboundId, string Code)> refused)
    {
        var details = new Dictionary<string, object?>
        {
            ["orders"] = refused.Select(r => new Dictionary<string, object?> { ["outboundId"] = r.OutboundId.ToString("D"), ["code"] = r.Code }).ToList()
        };
        return refused.All(r => r.Code == WaveContract.OrderInOtherWave)
            ? WaveSupport.Fail<WaveOrdersResultDto>(WaveErrorCodes.OrderInOtherWave, "The order is already in another active wave; nothing was added.", 409, details)
            : WaveSupport.Fail<WaveOrdersResultDto>(WaveErrorCodes.OrderNotEligible, "Not every order can enter the wave; nothing was added.", 422, details);
    }

    /// <summary>The replay of an add: the current wave and the memberships this request's orders have in it.</summary>
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
        var latest = (await Repository.MembershipsAsync(waveId, ids.ToList(), ct)).GroupBy(m => m.OutboundId).ToDictionary(g => g.Key, g => g.First());
        return Response<WaveOrdersResultDto>.Success(new WaveOrdersResultDto(wave,
            ids.Where(latest.ContainsKey).Select(id => new WaveOrderRowDto(id.ToString("D"), WaveContract.RowAdded, WaveReads.Order(latest[id]))).ToList()));
    }
}
