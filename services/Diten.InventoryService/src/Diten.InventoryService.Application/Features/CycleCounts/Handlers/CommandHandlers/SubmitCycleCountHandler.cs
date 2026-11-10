using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Application.Features.CycleCounts.Commands;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using Diten.InventoryService.Domain.CycleCounting.Rules;
using Diten.InventoryService.Domain.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.CycleCounts.Handlers.CommandHandlers;

/// <summary>
/// <c>submitCycleCount</c> (pack §8.3, §8.4; AC-17…AC-20, AC-52 (b) (c)). Order: key replay → count (404) → If-Match (409) → Released / Counting (409).
/// Then ONE <c>ReadConcern.Snapshot</c> transaction: the lines — every <c>Snapshot</c> and <c>Recount</c> line counted, else 422
/// <c>COUNT_LINES_UNCOUNTED</c> and nothing written (r6, SB2/F-1) — and the ledger (the window's movements on the scope bins and their balance
/// today; unreadable → 503 <c>COUNT_INVENTORY_UNAVAILABLE</c>, nothing written — AC-20). Each counted line gets its variance from
/// <see cref="CycleCountVarianceRules"/> (no tolerance in DL-2: every variance, flag or drift awaits a decision). The count becomes
/// <c>Submitted</c> (<c>SubmittedBy</c> / <c>SubmittedAt</c>); when every line is final (all <c>NoVariance</c>) it closes in the same transaction
/// and releases its bin claims. Timeline: one <c>VarianceComputed</c> per line, <c>Submitted</c> (counts and flags), <c>Closed</c> when closed.
/// </summary>
public sealed class SubmitCycleCountHandler : CycleCountCommandHandler, IRequestHandler<SubmitCycleCountCommand, Response<CycleCountDto>>
{
    public SubmitCycleCountHandler(ICycleCountRepository repository, CycleCountSupport support) : base(repository, support)
    {
    }

    public async Task<Response<CycleCountDto>> Handle(SubmitCycleCountCommand request, CancellationToken ct)
    {
        const string op = CycleCountContract.OpSubmitCycleCount;
        var fingerprint = CycleCountSupport.Fingerprint(("op", op), ("countId", request.CountId.ToString("D")));
        if (await ReplayAsync(request.CountId, op, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var count = await Repository.FindAsync(request.CountId, ct);
        if (count is null) return CycleCountSupport.NotFound<CycleCountDto>();
        if (count.Version != request.ExpectedVersion) return CycleCountSupport.Conflict<CycleCountDto>();
        if (!CycleCountRules.IsCounting(count)) return CycleCountSupport.InvalidState<CycleCountDto>(count.Status);

        Response<CycleCountDto>? answer = null;
        try
        {
            var outcome = await Repository.InTransactionAsync(async (session, tx) =>
            {
                answer = null;
                var lines = await session.ReadLinesAsync(count.Id, tx);
                var uncounted = lines.Count(CycleCountRules.IsUncounted);
                if (uncounted > 0)
                {
                    answer = CycleCountSupport.Fail<CycleCountDto>(CycleCountErrorCodes.LinesUncounted, "Every snapshot and recount line must be counted before submit.", 422,
                        new Dictionary<string, object?> { ["uncountedLineCount"] = uncounted });
                    return new CycleCountWriteOutcome(0, count.Id);
                }

                var ledger = await session.ReadVarianceLedgerAsync(new CycleCountLedgerRequest(count.ScopeBinIds, CycleCountRules.WindowStart(count.SnapshotAt!.Value)), tx);
                var now = Support.Now();
                var copy = Copy(count);
                copy.Status = CycleCountStatus.Submitted;
                copy.SubmittedBy = Support.UserId;
                copy.SubmittedAt = now;
                copy.Version = count.Version + 1;
                copy.UpdatedAt = now;

                var snapshotIds = count.SnapshotTransactionIds.ToHashSet();
                var computed = new List<(CycleCountLine Line, int ExpectedVersion)>();
                foreach (var line in lines.OrderBy(l => l.LocationId).ThenBy(l => l.SkuId).ThenBy(l => l.LotId, StringComparer.Ordinal).ThenBy(l => l.Id))
                {
                    if (line.CountedQuantity is not { } counted) continue; // only an uncounted Found line could be here — and a Found line is born counted
                    var key = CycleCountLineKey.Of(line);
                    var onKey = ledger.Movements.Where(m => m.Legs.Any(l => l.Key == key))
                        .Select(m => new CycleCountKeyMovement(m.TransactionId, m.CreatedAt, m.Legs.Where(l => l.Key == key).Sum(l => l.Delta)))
                        .ToList();
                    var variance = CycleCountVarianceRules.Compute(line.ExpectedQuantity, counted, line.CountedAt!.Value, snapshotIds, onKey,
                        ledger.BalanceToday.GetValueOrDefault(key));
                    computed.Add((WithVariance(line, variance, now), line.Version));
                }

                // the Submitted row is the key event (the first Seq of this write), then one VarianceComputed per line, then Closed
                var events = new List<CycleCountEvent>
                {
                    Support.Event(copy, CycleCountEventKind.Submitted, now, new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        ["previousStatus"] = count.Status.ToString(), ["lineCount"] = CycleCountSupport.Int(computed.Count),
                        ["noVarianceLineCount"] = CycleCountSupport.Int(computed.Count(c => c.Line.Status == CycleCountLineStatus.NoVariance)),
                        ["awaitingApprovalLineCount"] = CycleCountSupport.Int(computed.Count(c => c.Line.Status == CycleCountLineStatus.AwaitingApproval)),
                        ["movementDuringCountLineCount"] = CycleCountSupport.Int(computed.Count(c => c.Line.MovementDuringCount)),
                        ["snapshotDriftLineCount"] = CycleCountSupport.Int(computed.Count(c => c.Line.SnapshotDrift))
                    }, request.IdempotencyKey, op, fingerprint)
                };
                events.AddRange(computed.Select(c => Support.Event(copy, CycleCountEventKind.VarianceComputed, now, new Dictionary<string, string?>(StringComparer.Ordinal)
                {
                    ["expectedQuantity"] = InventoryQuantity.ToWire(c.Line.ExpectedQuantity),
                    ["movementsSinceSnapshot"] = InventoryQuantity.ToWire(c.Line.MovementsSinceSnapshot!.Value),
                    ["expectedAtCount"] = InventoryQuantity.ToWire(c.Line.ExpectedAtCount!.Value),
                    ["varianceQuantity"] = InventoryQuantity.ToWire(c.Line.VarianceQuantity!.Value),
                    ["countedQuantity"] = InventoryQuantity.ToWire(c.Line.CountedQuantity!.Value),
                    ["movementDuringCount"] = c.Line.MovementDuringCount ? "true" : "false",
                    ["snapshotDrift"] = c.Line.SnapshotDrift ? "true" : "false",
                    ["status"] = c.Line.Status.ToString()
                }, lineId: c.Line.Id)));

                var closes = computed.All(c => CycleCountRules.IsFinal(c.Line));
                if (closes)
                {
                    copy.Status = CycleCountStatus.Closed;
                    events.Add(Support.Event(copy, CycleCountEventKind.Closed, now, new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        ["reason"] = "AllLinesFinal", ["releasedBinClaims"] = CycleCountSupport.Int(count.ScopeBinIds.Count)
                    }));
                }

                if (!await session.ReplaceCountAsync(copy, count.Version, tx)) return new CycleCountWriteOutcome(0, count.Id);
                if (!await session.ReplaceLinesAsync(computed, tx)) throw new LinesLostException();
                if (closes) await session.ReleaseBinClaimsAsync(count.Id, now, tx);
                await session.InsertEventsAsync(events, tx);
                answer = Response<CycleCountDto>.Success(CycleCountReads.Dto(copy));
                return new CycleCountWriteOutcome(1, count.Id);
            }, ct);

            if (outcome.Affected == 1 || answer is { IsSuccessful: false }) return answer!;
        }
        catch (CycleCountInventoryUnavailableException)
        {
            return CycleCountSupport.Fail<CycleCountDto>(CycleCountErrorCodes.InventoryUnavailable, "The ledger could not be read; the count was not submitted.", 503);
        }
        catch (LinesLostException)
        {
            // a line changed under the submit (a late entry) — the head write is rolled back with it
        }
        catch (CycleCountDuplicateException ex) when (ex.Index == CycleCountUniqueIndex.Event)
        {
            // a concurrent twin of this request committed its key event
        }

        return await ReplayAsync(count.Id, op, request.IdempotencyKey, fingerprint, ct) ?? CycleCountSupport.Conflict<CycleCountDto>();
    }

    private static CycleCountLine WithVariance(CycleCountLine l, CycleCountVariance v, DateTimeOffset now) => new()
    {
        Id = l.Id, TenantId = l.TenantId, LegalEntityId = l.LegalEntityId, CreatedAt = l.CreatedAt, UpdatedAt = now, Version = l.Version + 1,
        CountId = l.CountId, WarehouseId = l.WarehouseId, LocationId = l.LocationId, ItemId = l.ItemId, SkuId = l.SkuId, SkuLevel = l.SkuLevel,
        LotId = l.LotId, StockStatus = l.StockStatus, BaseUomId = l.BaseUomId, Origin = l.Origin, RecountOfLineId = l.RecountOfLineId,
        RecountDepth = l.RecountDepth, ExpectedQuantity = l.ExpectedQuantity, CountedQuantity = l.CountedQuantity, CountedBy = l.CountedBy,
        CountedAt = l.CountedAt, ScanRef = l.ScanRef, SerialControlled = l.SerialControlled,
        MovementsSinceSnapshot = v.MovementsSinceSnapshot, ExpectedAtCount = v.ExpectedAtCount, VarianceQuantity = v.VarianceQuantity,
        MovementDuringCount = v.MovementDuringCount, SnapshotDrift = v.SnapshotDrift, Status = v.Status
    };

    private sealed class LinesLostException() : Exception("A line changed since it was read.");
}
