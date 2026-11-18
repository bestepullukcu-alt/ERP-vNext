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
/// <c>updateWave</c> (pack §8.3 Draft only; AC-6, AC-7): replay first, then 404 / If-Match (409 <c>WAVE_CONFLICT</c>) / state (409
/// <c>WAVE_INVALID_STATE</c>) / the warehouse never changes (400 <c>WAVE_WAREHOUSE_IMMUTABLE</c>) / the cutoff in the WAVE's own time zone / the
/// responsible user; the change is one compare-and-set write with its <c>Updated</c> event (before / after of the changed fields).
/// </summary>
public sealed class UpdateWaveHandler : WaveCommandHandler, IRequestHandler<UpdateWaveCommand, Response<WaveDto>>
{
    private readonly WaveChecks _checks;

    public UpdateWaveHandler(IWaveRepository repository, WaveSupport support, WaveChecks checks) : base(repository, support) => _checks = checks;

    public async Task<Response<WaveDto>> Handle(UpdateWaveCommand request, CancellationToken ct)
    {
        const string op = WaveContract.OpUpdateWave;
        var form = WaveChecks.Normalize(request.Wave);
        var reason = WaveSupport.Trimmed(request.Reason);
        var fingerprint = WaveSupport.Fingerprint([("op", op), ("waveId", request.WaveId.ToString("D")), .. form.Fields(), ("reason", reason)]);
        if (await ReplayAsync(request.WaveId, op, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var wave = await Repository.FindAsync(request.WaveId, ct);
        if (wave is null) return WaveSupport.NotFound<WaveDto>();
        if (wave.Version != request.ExpectedVersion) return WaveSupport.Conflict<WaveDto>();
        if (!WaveRules.IsDraft(wave)) return WaveSupport.InvalidState<WaveDto>(wave.Status);
        if (form.WarehouseId != wave.WarehouseId)
            return WaveSupport.Fail<WaveDto>(WaveErrorCodes.WarehouseImmutable, "A wave's warehouse cannot be changed.", 400);
        var (cutoff, cutoffRefusal) = _checks.Cutoff<WaveDto>(form.CutoffLocal, wave.TimeZoneId);
        if (cutoffRefusal is not null) return cutoffRefusal;
        if (await _checks.ResponsibleAsync<WaveDto>(form.ResponsibleUserId, ct) is { } responsibleRefusal) return responsibleRefusal;

        var before = CreateWaveHandler.After(wave);
        var copy = Copy(wave);
        copy.WaveType = form.WaveType;
        copy.CutoffLocal = form.CutoffLocal;
        copy.CutoffAtUtc = cutoff!.AtUtc;
        copy.CutoffAmbiguousResolved = cutoff.AmbiguousResolved;
        copy.ShipToFilter = form.ShipToFilter;
        copy.MaxOrders = form.MaxOrders;
        copy.MaxLines = form.MaxLines;
        copy.ResponsibleUserId = form.ResponsibleUserId;
        copy.Note = form.Note;
        var after = CreateWaveHandler.After(copy);

        var now = Support.Now();
        copy.Version = wave.Version + 1;
        copy.UpdatedAt = now;
        var changes = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (field, value) in after.Where(a => !string.Equals(a.Value, before[a.Key], StringComparison.Ordinal)))
        {
            changes["before." + field] = before[field];
            changes["after." + field] = value;
        }

        if (reason is not null) changes["reason"] = reason;
        var updated = Support.Event(copy, WaveEventKind.Updated, now, changes, request.IdempotencyKey, op, fingerprint);
        if (await SaveKeyedAsync(copy, wave.Version, [updated], null, ct)) return Response<WaveDto>.Success(WaveReads.Dto(copy, await TotalsAsync(copy.Id, ct)));
        return await ReplayAsync(request.WaveId, op, request.IdempotencyKey, fingerprint, ct) ?? WaveSupport.Conflict<WaveDto>();
    }
}
