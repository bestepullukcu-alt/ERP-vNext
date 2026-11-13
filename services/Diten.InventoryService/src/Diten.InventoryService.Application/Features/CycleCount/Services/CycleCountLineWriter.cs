using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using Diten.InventoryService.Domain.CycleCounting.Rules;

namespace Diten.InventoryService.Application.Features.CycleCounting.Services;

/// <summary>One timeline row a line decision writes (the first row of a keyed write carries the request's key).</summary>
public sealed record CycleCountLineEvent(CycleCountEventKind Kind, IReadOnlyDictionary<string, string?> Data);

/// <summary>What a decision makes of the line as read IN the transaction: a refusal (nothing written), no change, or the changed line + its rows.</summary>
public sealed record CycleCountLineStep<T>(Response<T>? Refusal, CycleCountLine? Changed, IReadOnlyList<CycleCountLineEvent> Events)
{
    public static CycleCountLineStep<T> Refuse(Response<T> refusal) => new(refusal, null, []);

    public static readonly CycleCountLineStep<T> Unchanged = new(null, null, []);

    public static CycleCountLineStep<T> Change(CycleCountLine line, params CycleCountLineEvent[] events) => new(null, line, events);
}

public enum CycleCountLineWriteKind
{
    Written,
    Unchanged,
    Refused,
    NotFound,

    /// <summary>A concurrent twin of this keyed request committed its key event first — the caller answers with the replay.</summary>
    Twin,

    /// <summary>The count head kept changing (or the line under the write) beyond <see cref="CycleCountContract.MaxHeadAttempts"/>.</summary>
    Conflict
}

public sealed record CycleCountLineWrite<T>(CycleCountLineWriteKind Kind, CycleCount? Count, CycleCountLine? Line, Response<T>? Refusal = null);

/// <summary>
/// The ONE write path of a DL-3 line decision (approve, reject, retry, a posting result, resolve-integrity): ONE transaction that reads the count's
/// lines, lets the decision see the line as it is now, writes the line (compare-and-set on its version), the count head (compare-and-set on its
/// version — the timeline <c>Seq</c>) and the rows, and — when every line is now final (pack §8.3) — closes the count, releases its own bin claims
/// and writes <c>Closed</c> in the same transaction (AC-33). A head lost to a concurrent decision on another line is re-read and the decision
/// re-taken (≤ <see cref="CycleCountContract.MaxHeadAttempts"/>).
/// </summary>
public sealed class CycleCountLineWriter
{
    private readonly ICycleCountRepository _repository;
    private readonly CycleCountSupport _support;

    public CycleCountLineWriter(ICycleCountRepository repository, CycleCountSupport support)
    {
        _repository = repository;
        _support = support;
    }

    public async Task<CycleCountLineWrite<T>> WriteAsync<T>(Guid countId, Guid lineId, Func<CycleCount, CycleCountLine, CycleCountLineStep<T>> decide,
        string? op, string? key, string? fingerprint, CancellationToken ct)
    {
        for (var attempt = 1; attempt <= CycleCountContract.MaxHeadAttempts; attempt++)
        {
            var count = await _repository.FindAsync(countId, ct);
            if (count is null) return new CycleCountLineWrite<T>(CycleCountLineWriteKind.NotFound, null, null);

            CycleCountLineWrite<T>? result = null;
            try
            {
                await _repository.InTransactionAsync(async (session, tx) =>
                {
                    result = null;
                    var lines = await session.ReadLinesAsync(countId, tx);
                    var line = lines.FirstOrDefault(l => l.Id == lineId);
                    if (line is null)
                    {
                        result = new CycleCountLineWrite<T>(CycleCountLineWriteKind.NotFound, count, null);
                        return new CycleCountWriteOutcome(0, lineId);
                    }

                    var step = decide(count, line);
                    if (step.Refusal is not null)
                    {
                        result = new CycleCountLineWrite<T>(CycleCountLineWriteKind.Refused, count, line, step.Refusal);
                        return new CycleCountWriteOutcome(0, lineId);
                    }

                    if (step.Changed is not { } changed)
                    {
                        result = new CycleCountLineWrite<T>(CycleCountLineWriteKind.Unchanged, count, line);
                        return new CycleCountWriteOutcome(0, lineId);
                    }

                    var now = _support.Now();
                    var head = count.Copy();
                    head.Version = count.Version + 1;
                    head.UpdatedAt = now;
                    changed.Version = line.Version + 1;
                    changed.UpdatedAt = now;
                    var events = step.Events.Select((e, i) => _support.Event(head, e.Kind, now, e.Data, i == 0 ? key : null, i == 0 ? op : null,
                        i == 0 ? fingerprint : null, lineId)).ToList();

                    var closes = head.Status == CycleCountStatus.Submitted && lines.Select(l => l.Id == lineId ? changed : l).All(CycleCountRules.IsFinal);
                    if (closes)
                    {
                        head.Status = CycleCountStatus.Closed;
                        events.Add(_support.Event(head, CycleCountEventKind.Closed, now,
                            new Dictionary<string, string?>(StringComparer.Ordinal) { ["reason"] = "AllLinesFinal" }));
                    }

                    if (!await session.ReplaceCountAsync(head, count.Version, tx)) return new CycleCountWriteOutcome(0, lineId);
                    if (!await session.ReplaceLinesAsync([(changed, line.Version)], tx)) throw new LineChangedException();
                    if (closes)
                    {
                        var released = await session.ReleaseBinClaimsAsync(countId, now, tx);
                        events[^1].Data["releasedBinClaims"] = released.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    }

                    await session.InsertEventsAsync(events, tx);
                    result = new CycleCountLineWrite<T>(CycleCountLineWriteKind.Written, head, changed);
                    return new CycleCountWriteOutcome(1, lineId);
                }, ct);
            }
            catch (LineChangedException)
            {
                result = null; // the line moved under the write — take the decision again on the new line
            }
            catch (CycleCountDuplicateException ex) when (ex.Index == CycleCountUniqueIndex.Event)
            {
                return new CycleCountLineWrite<T>(CycleCountLineWriteKind.Twin, null, null);
            }

            if (result is not null) return result;
        }

        return new CycleCountLineWrite<T>(CycleCountLineWriteKind.Conflict, null, null,
            CycleCountSupport.Fail<T>(CycleCountErrorCodes.Conflict, "The count kept changing while the line was written; retry.", 409));
    }

    private sealed class LineChangedException() : Exception("The line changed since it was read.");
}
