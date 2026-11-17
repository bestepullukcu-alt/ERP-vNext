using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using Diten.InventoryService.Domain.CycleCounting.Rules;

namespace Diten.InventoryService.Application.Features.CycleCounts.Handlers.CommandHandlers;

/// <summary>
/// The shared skeleton of the two DL-2 LINE writes (draft §1 rules 5–6): the key event of this count and operation answers first (same fingerprint
/// → 200 + the line as it is now, other → 400), before every business rule. A line write also writes the count head (its timeline <c>Seq</c>,
/// <c>Released → Counting</c>, the line count) with a compare-and-set; a head lost to a concurrent entry on ANOTHER line is re-read and the write
/// retried (the caller's own guard is the line's If-Match, not the head's version).
/// </summary>
public abstract class CycleCountLineCommandHandler : CycleCountCommandHandler
{
    protected CycleCountLineCommandHandler(ICycleCountRepository repository, CycleCountSupport support) : base(repository, support)
    {
    }

    /// <summary>null = not a replay; otherwise 200 + the line the first request wrote (as it is now), or 400 for another body.</summary>
    protected async Task<Response<CycleCountLineDto>?> ReplayLineAsync(Guid countId, string op, string key, string fingerprint, CancellationToken ct)
    {
        var earlier = await Repository.FindKeyEventAsync(countId, Support.UserId, op, key, ct);
        if (earlier is null) return null;
        if (earlier.RequestFingerprint != fingerprint) return CycleCountSupport.KeyReused<CycleCountLineDto>();
        var count = await Repository.FindIncludingDeletedAsync(countId, ct);
        var line = earlier.LineId is { } lineId ? await Repository.FindLineAsync(countId, lineId, ct) : null;
        return count is null || line is null
            ? CycleCountSupport.NotFound<CycleCountLineDto>()
            : Response<CycleCountLineDto>.Success(CycleCountReads.Line(line, CycleCountReads.ShowsExpected(count, callerReadsVariances: false)));
    }

    /// <summary>
    /// The count head a line write carries: <c>Released → Counting</c> on the first entry, the line count, version + 1 — and the line's timeline
    /// row (its <c>Seq</c> is the head's next one).
    /// </summary>
    protected (CycleCount Head, CycleCountEvent Event) Head(CycleCount count, int addedLines, CycleCountEventKind kind, DateTimeOffset now, Guid lineId,
        IReadOnlyDictionary<string, string?> data, string op, string key, string fingerprint)
    {
        var head = Copy(count);
        var payload = new Dictionary<string, string?>(data, StringComparer.Ordinal);
        if (count.Status == CycleCountStatus.Released)
        {
            head.Status = CycleCountStatus.Counting;
            payload["countStatus"] = CycleCountStatus.Counting.ToString();
        }

        head.LineCount = count.LineCount + addedLines;
        head.Version = count.Version + 1;
        head.UpdatedAt = now;
        return (head, Support.Event(head, kind, now, payload, key, op, fingerprint, lineId));
    }

    /// <summary>The refusal of a count that does not take entries (pack §8.3).</summary>
    protected static Response<CycleCountLineDto>? NotCounting(CycleCount count)
        => CycleCountRules.IsCounting(count) ? null : CycleCountSupport.InvalidState<CycleCountLineDto>(count.Status);

    public static Response<CycleCountLineDto> QuantityInvalid(string message)
        => CycleCountSupport.Fail<CycleCountLineDto>(CycleCountErrorCodes.QuantityInvalid, message, 400);

    /// <summary>A found line's / an entry's scan reference: trimmed, ≤ <see cref="CycleCountContract.MaxScanRefLength"/>; null = none.</summary>
    protected static bool TryScanRef(string? raw, out string? scanRef)
    {
        scanRef = CycleCountSupport.Trimmed(raw);
        return scanRef is null || scanRef.Length <= CycleCountContract.MaxScanRefLength;
    }

    /// <summary>Thrown inside a line write's transaction to abandon it when the LINE's compare-and-set lost (the head write is rolled back too).</summary>
    protected sealed class LineLostException() : Exception("The line changed since it was read.");
}
