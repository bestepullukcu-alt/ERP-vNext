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
/// <c>createWave</c> (pack §4.1, §8.2 rule 1, §8.4, §8.14, §12; AC-2, AC-10, AC-14). Order (draft §4.1 "Yaratma sırası"): the caller's create
/// key FIRST (replay / 400 — a replay takes no number); the business rules (warehouse, the warehouse's wave setting, the cutoff in the
/// warehouse's time zone, the responsible user); the company prefix; THEN the number from the counter (not part of the transaction); then ONE
/// transaction (wave + <c>Created</c> event + central record); a unique-index refusal re-reads the key first (the concurrent twin's replay). A
/// number whose write failed is skipped, never reissued.
/// </summary>
public sealed class CreateWaveHandler : WaveCommandHandler, IRequestHandler<CreateWaveCommand, Response<WaveDto>>
{
    private readonly WaveChecks _checks;

    public CreateWaveHandler(IWaveRepository repository, WaveSupport support, WaveChecks checks) : base(repository, support) => _checks = checks;

    public async Task<Response<WaveDto>> Handle(CreateWaveCommand request, CancellationToken ct)
    {
        var form = WaveChecks.Normalize(request.Wave);
        var fingerprint = WaveSupport.Fingerprint([("op", WaveContract.OpCreateWave), .. form.Fields()]);
        if (await CreateReplayAsync(request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var (warehouse, warehouseRefusal) = await _checks.WarehouseAsync<WaveDto>(form.WarehouseId, 422, ct);
        if (warehouseRefusal is not null) return warehouseRefusal;
        var (timeZoneId, settingsRefusal) = await _checks.ManagedTimeZoneAsync<WaveDto>(form.WarehouseId, ct);
        if (settingsRefusal is not null) return settingsRefusal;
        var (cutoff, cutoffRefusal) = _checks.Cutoff<WaveDto>(form.CutoffLocal, timeZoneId!);
        if (cutoffRefusal is not null) return cutoffRefusal;
        if (await _checks.ResponsibleAsync<WaveDto>(form.ResponsibleUserId, ct) is { } responsibleRefusal) return responsibleRefusal;
        var (companyCode, companyRefusal) = await _checks.CompanyCodeAsync<WaveDto>(ct);
        if (companyRefusal is not null) return companyRefusal;
        var (number, numberRefusal) = await _checks.NumberAsync<WaveDto>(companyCode!, ct);
        if (numberRefusal is not null) return numberRefusal;

        var now = Support.Now();
        var wave = new Wave
        {
            Id = Guid.NewGuid(),
            CreatedAt = now,
            Version = 1,
            Number = number!,
            CompanyCodePrefix = companyCode!,
            WarehouseId = form.WarehouseId,
            WarehouseCode = warehouse!.Code,
            WaveType = form.WaveType,
            CutoffLocal = form.CutoffLocal,
            CutoffAtUtc = cutoff!.AtUtc,
            TimeZoneId = timeZoneId!,
            CutoffAmbiguousResolved = cutoff.AmbiguousResolved,
            ShipToFilter = form.ShipToFilter,
            MaxOrders = form.MaxOrders,
            MaxLines = form.MaxLines,
            ResponsibleUserId = form.ResponsibleUserId,
            Note = form.Note,
            Status = WaveStatus.Draft,
            CreatedBy = Support.UserId,
            IdempotencyKey = request.IdempotencyKey,
            RequestFingerprint = fingerprint,
            CorrelationId = Support.CorrelationId
        };
        var created = Support.Event(wave, WaveEventKind.Created, now, After(wave), request.IdempotencyKey, WaveContract.OpCreateWave, fingerprint);

        try
        {
            await Repository.InTransactionAsync(async (session, tx) =>
            {
                await session.InsertWaveAsync(wave, tx);
                await session.InsertEventsAsync([created], tx);
                return new WaveWriteOutcome(1, wave.Id);
            }, ct);
        }
        catch (WaveDuplicateException)
        {
            // whichever unique index refused it — the create key of a concurrent twin, or a number the store already holds
            if (await CreateReplayAsync(request.IdempotencyKey, fingerprint, ct) is { } twin) return twin;
            return WaveSupport.Fail<WaveDto>(WaveErrorCodes.RecordCodeUnavailable, "The wave could not be stored; retry.", 503);
        }

        return Response<WaveDto>.Success(WaveReads.Dto(wave, WaveTotals.Empty), 201);
    }

    private async Task<Response<WaveDto>?> CreateReplayAsync(string key, string fingerprint, CancellationToken ct)
    {
        var earlier = await Repository.FindByCreateKeyAsync(Support.UserId, key, ct);
        if (earlier is null) return null;
        return earlier.RequestFingerprint == fingerprint
            ? Response<WaveDto>.Success(WaveReads.Dto(earlier, await TotalsAsync(earlier.Id, ct)))
            : WaveSupport.KeyReused<WaveDto>();
    }

    /// <summary>The form values of a wave as event data (pack §21: number, warehouse, type, cutoff, time zone, filter, limits, responsible, note).</summary>
    internal static Dictionary<string, string?> After(Wave w) => new(StringComparer.Ordinal)
    {
        ["number"] = w.Number, ["warehouseId"] = w.WarehouseId.ToString("D"), ["waveType"] = w.WaveType.ToString(), ["cutoffLocal"] = w.CutoffLocal,
        ["cutoffAtUtc"] = WaveSupport.Wire(w.CutoffAtUtc), ["timeZoneId"] = w.TimeZoneId, ["shipToFilter"] = w.ShipToFilter,
        ["maxOrders"] = WaveSupport.Int(w.MaxOrders), ["maxLines"] = WaveSupport.Int(w.MaxLines),
        ["responsibleUserId"] = w.ResponsibleUserId?.ToString("D"), ["note"] = w.Note
    };
}
