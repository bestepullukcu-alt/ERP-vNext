using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.WavePlanning.Services;
using Diten.InventoryService.Domain.WavePlanning.Entities;
using Diten.InventoryService.Domain.WavePlanning.Repositories;
using Diten.InventoryService.Domain.WavePlanning.Rules;

namespace Diten.InventoryService.Application.Features.Waves.Handlers.CommandHandlers;

/// <summary>
/// The shared skeleton of the wave commands (draft §1 rules 5–6; the MOD-0181 <c>CycleCountCommandHandler</c> pattern): the caller's key event
/// of this wave and operation answers first (same fingerprint → 200 + the current record, other → 400), BEFORE If-Match and every business rule;
/// a keyed write that loses its compare-and-set or hits a unique index re-reads the key before answering 409.
/// </summary>
public abstract class WaveCommandHandler
{
    protected WaveCommandHandler(IWaveRepository repository, WaveSupport support)
    {
        Repository = repository;
        Support = support;
    }

    protected IWaveRepository Repository { get; }

    protected WaveSupport Support { get; }

    /// <summary>null = this key was not used; true = the same request (replay); false = another request used the key (400).</summary>
    protected async Task<bool?> KeyUsedAsync(Guid streamId, string op, string key, string fingerprint, CancellationToken ct)
    {
        var earlier = await Repository.FindKeyEventAsync(streamId, Support.UserId, op, key, ct);
        return earlier is null ? null : earlier.RequestFingerprint == fingerprint;
    }

    /// <summary>The wave as it is stored now, with the totals of its active memberships.</summary>
    protected async Task<WaveDto?> CurrentAsync(Guid waveId, CancellationToken ct)
    {
        var wave = await Repository.FindAsync(waveId, ct);
        return wave is null ? null : WaveReads.Dto(wave, await TotalsAsync(waveId, ct));
    }

    protected async Task<WaveTotals> TotalsAsync(Guid waveId, CancellationToken ct)
        => (await Repository.TotalsAsync([waveId], ct)).GetValueOrDefault(waveId, WaveTotals.Empty);

    /// <summary>null = not a replay; otherwise the answer (200 + the current wave, or 400 for a reused key).</summary>
    protected async Task<Response<WaveDto>?> ReplayAsync(Guid waveId, string op, string key, string fingerprint, CancellationToken ct)
    {
        switch (await KeyUsedAsync(waveId, op, key, fingerprint, ct))
        {
            case null:
                return null;
            case false:
                return WaveSupport.KeyReused<WaveDto>();
            default:
                return await CurrentAsync(waveId, ct) is { } current ? Response<WaveDto>.Success(current) : WaveSupport.NotFound<WaveDto>();
        }
    }

    /// <summary>
    /// A command's own keyed write: the wave (version + 1) and its rows with a compare-and-set on <paramref name="expectedVersion"/>, plus
    /// <paramref name="more"/> in the same transaction. True = written; false = lost (0 records, or a concurrent twin's key event) — the caller
    /// re-reads the key before answering 409.
    /// </summary>
    protected async Task<bool> SaveKeyedAsync(Wave copy, int expectedVersion, IReadOnlyList<WaveEvent> events, Func<IWaveWriteSession, CancellationToken, Task>? more,
        CancellationToken ct)
    {
        try
        {
            var outcome = await Repository.InTransactionAsync(async (session, tx) =>
            {
                if (!await session.ReplaceWaveAsync(copy, expectedVersion, tx)) return new WaveWriteOutcome(0, copy.Id);
                if (more is not null) await more(session, tx);
                await session.InsertEventsAsync(events, tx);
                return new WaveWriteOutcome(1, copy.Id);
            }, ct);
            return outcome.Affected == 1;
        }
        catch (WaveDuplicateException ex) when (ex.Index == WaveUniqueIndex.Event)
        {
            // a concurrent twin of this request committed its key event
            return false;
        }
    }

    /// <summary>A working copy of the stored wave (the stored instance stays as read for the compare-and-set).</summary>
    protected static Wave Copy(Wave w) => new()
    {
        Id = w.Id, TenantId = w.TenantId, LegalEntityId = w.LegalEntityId, CreatedAt = w.CreatedAt, UpdatedAt = w.UpdatedAt, Version = w.Version,
        IsDeleted = w.IsDeleted, DeletedAt = w.DeletedAt, Number = w.Number, CompanyCodePrefix = w.CompanyCodePrefix, WarehouseId = w.WarehouseId,
        WarehouseCode = w.WarehouseCode, WaveType = w.WaveType, CutoffLocal = w.CutoffLocal, CutoffAtUtc = w.CutoffAtUtc, TimeZoneId = w.TimeZoneId,
        CutoffAmbiguousResolved = w.CutoffAmbiguousResolved, ShipToFilter = w.ShipToFilter, MaxOrders = w.MaxOrders, MaxLines = w.MaxLines,
        ResponsibleUserId = w.ResponsibleUserId, Note = w.Note, Status = w.Status, CreatedBy = w.CreatedBy, ReleasedBy = w.ReleasedBy, ReleasedAt = w.ReleasedAt,
        CancelledBy = w.CancelledBy, CancelledAt = w.CancelledAt, CancelReason = w.CancelReason, CompletedAt = w.CompletedAt, IdempotencyKey = w.IdempotencyKey,
        RequestFingerprint = w.RequestFingerprint, EventSeq = w.EventSeq, CorrelationId = w.CorrelationId
    };
}
