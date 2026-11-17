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
/// <c>updateWaveWarehouseSettings</c> (pack §4.5, §8.13, §12; AC-15 a…e; Q-6 = B — its own Tier-3 key). The reason is required (400
/// <c>WAVE_REASON_REQUIRED</c>); another company's warehouse does not exist (404). The first write creates the record with
/// <c>If-Match: W/"0"</c>; a later one is a compare-and-set on the setting's version. <c>false → true</c> and a threshold change are always
/// accepted. The write and its <c>SettingsUpdated</c> event (stream Settings — before / after and the reason) are one transaction. DL-1: no
/// deferred outbound can exist, so <c>true → false</c> only writes the setting — the pending-order round is DL-2 (pack §8.19).
/// </summary>
public sealed class UpdateWaveWarehouseSettingsHandler : WaveCommandHandler,
    IRequestHandler<UpdateWaveWarehouseSettingsCommand, Response<WaveWarehouseSettingsDto>>
{
    private readonly WaveChecks _checks;
    private readonly IWaveInventoryReader _inventory;

    public UpdateWaveWarehouseSettingsHandler(IWaveRepository repository, WaveSupport support, WaveChecks checks, IWaveInventoryReader inventory)
        : base(repository, support)
    {
        _checks = checks;
        _inventory = inventory;
    }

    public async Task<Response<WaveWarehouseSettingsDto>> Handle(UpdateWaveWarehouseSettingsCommand request, CancellationToken ct)
    {
        const string op = WaveContract.OpUpdateWaveWarehouseSettings;
        if (WaveRules.Reason(request.Reason) is not { } reason) return WaveSupport.ReasonRequired<WaveWarehouseSettingsDto>();
        var input = request.Settings;
        var fingerprint = WaveSupport.Fingerprint(("op", op), ("warehouseId", request.WarehouseId.ToString("D")),
            ("waveManaged", input.WaveManaged ? "true" : "false"), ("timeZoneId", input.TimeZoneId),
            ("riskMinutesBeforeCutoff", WaveSupport.Int(input.RiskMinutesBeforeCutoff)), ("riskProgressPercent", WaveSupport.Int(input.RiskProgressPercent)),
            ("pendingTaskMinutes", WaveSupport.Int(input.PendingTaskMinutes)), ("reason", reason));

        var (_, warehouseRefusal) = await _checks.WarehouseAsync<WaveWarehouseSettingsDto>(request.WarehouseId, 404, ct);
        if (warehouseRefusal is not null) return warehouseRefusal;

        var stored = await Repository.FindSettingsAsync(request.WarehouseId, ct);
        if (stored is not null && await ReplayAsync(stored, op, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;
        if ((stored?.Version ?? 0) != request.ExpectedVersion) return WaveSupport.Conflict<WaveWarehouseSettingsDto>();

        long deferred;
        long releasing;
        try
        {
            // read BEFORE the write: a read that cannot be done refuses the request with nothing written
            deferred = await _inventory.DeferredCountAsync(request.WarehouseId, ct);
            releasing = await Repository.ReleasingOrderCountAsync(request.WarehouseId, ct);
        }
        catch (WaveInventoryUnavailableException)
        {
            return WaveSupport.InventoryUnavailable<WaveWarehouseSettingsDto>();
        }

        var now = Support.Now();
        var next = new WaveWarehouseSettings
        {
            Id = stored?.Id ?? Guid.NewGuid(),
            CreatedAt = stored?.CreatedAt ?? now,
            UpdatedAt = stored is null ? null : now,
            Version = (stored?.Version ?? 0) + 1,
            WarehouseId = request.WarehouseId,
            WaveManaged = input.WaveManaged,
            TimeZoneId = WaveSupport.Trimmed(input.TimeZoneId),
            RiskMinutesBeforeCutoff = input.RiskMinutesBeforeCutoff,
            RiskProgressPercent = input.RiskProgressPercent,
            PendingTaskMinutes = input.PendingTaskMinutes,
            GateSeq = stored?.GateSeq ?? 0,
            EventSeq = stored?.EventSeq ?? 0
        };

        var data = new Dictionary<string, string?>(StringComparer.Ordinal) { ["reason"] = reason, ["warehouseId"] = request.WarehouseId.ToString("D") };
        var after = Values(next);
        var before = stored is null ? null : Values(stored);
        foreach (var (field, value) in after)
        {
            if (before is not null && string.Equals(before[field], value, StringComparison.Ordinal)) continue;
            if (before is not null) data["before." + field] = before[field];
            data["after." + field] = value;
        }

        var updated = Support.Event(next, WaveEventKind.SettingsUpdated, now, data, request.IdempotencyKey, op, fingerprint);
        var written = false;
        try
        {
            var outcome = await Repository.InTransactionAsync(async (session, tx) =>
            {
                if (stored is null) await session.InsertSettingsAsync(next, tx);
                else if (!await session.UpdateSettingsAsync(next, stored.Version, tx)) return new WaveWriteOutcome(0, next.Id);
                await session.InsertEventsAsync([updated], tx);
                return new WaveWriteOutcome(1, next.Id);
            }, ct);
            written = outcome.Affected == 1;
        }
        catch (WaveDuplicateException ex) when (ex.Index is WaveUniqueIndex.Settings or WaveUniqueIndex.Event)
        {
            // a concurrent first write created the record, or a concurrent twin committed its key event
        }

        if (written) return Response<WaveWarehouseSettingsDto>.Success(WaveReads.Settings(request.WarehouseId, next, deferred, releasing));

        var winner = await Repository.FindSettingsAsync(request.WarehouseId, ct);
        if (winner is not null && await ReplayAsync(winner, op, request.IdempotencyKey, fingerprint, ct) is { } twin) return twin;
        return WaveSupport.Conflict<WaveWarehouseSettingsDto>();
    }

    private static Dictionary<string, string?> Values(WaveWarehouseSettings s) => new(StringComparer.Ordinal)
    {
        ["waveManaged"] = s.WaveManaged ? "true" : "false", ["timeZoneId"] = s.TimeZoneId,
        ["riskMinutesBeforeCutoff"] = WaveSupport.Int(s.RiskMinutesBeforeCutoff), ["riskProgressPercent"] = WaveSupport.Int(s.RiskProgressPercent),
        ["pendingTaskMinutes"] = WaveSupport.Int(s.PendingTaskMinutes)
    };

    /// <summary>null = not a replay; otherwise 200 + the setting as it is stored now, or 400 for a reused key.</summary>
    private async Task<Response<WaveWarehouseSettingsDto>?> ReplayAsync(WaveWarehouseSettings stored, string op, string key, string fingerprint, CancellationToken ct)
    {
        switch (await KeyUsedAsync(stored.Id, op, key, fingerprint, ct))
        {
            case null:
                return null;
            case false:
                return WaveSupport.KeyReused<WaveWarehouseSettingsDto>();
        }

        try
        {
            return Response<WaveWarehouseSettingsDto>.Success(WaveReads.Settings(stored.WarehouseId, stored,
                await _inventory.DeferredCountAsync(stored.WarehouseId, ct), await Repository.ReleasingOrderCountAsync(stored.WarehouseId, ct)));
        }
        catch (WaveInventoryUnavailableException)
        {
            return WaveSupport.InventoryUnavailable<WaveWarehouseSettingsDto>();
        }
    }
}
