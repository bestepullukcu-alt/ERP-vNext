using System.Globalization;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Application.Features.CountVariances.Commands;
using Diten.InventoryService.Application.Features.CycleCounting;
using Diten.InventoryService.Application.Features.CycleCounting.Services;
using Diten.InventoryService.Application.Features.CycleCounts;
using Diten.InventoryService.Application.Features.CycleCounts.Handlers.CommandHandlers;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.CycleCounting.Repositories;
using Diten.InventoryService.Domain.CycleCounting.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.CountVariances.Handlers.CommandHandlers;

/// <summary>
/// <c>requestCountRecount</c> (pack §8.5, §4.7, §12; AC-32, AC-13 (e)). Shape: 1..200 distinct <c>lineIds</c>, a reason (1..500, 400
/// <c>COUNT_REASON_REQUIRED</c>), <c>plannedDate</c> ≥ today, <c>note</c> ≤ 500 — <c>blind</c>, <c>countType</c> and the scope are never members (400,
/// the reader) → key replay (the new count) → source count (404) → If-Match (its version, 409) → Submitted (409) → every line of THIS count and
/// AwaitingApproval or (Approved + Refused) (409 <c>COUNT_INVALID_STATE</c>) → none at <c>RecountDepth ≥ MaxRecounts</c> (409 <c>COUNT_RECOUNT_LIMIT</c>,
/// the whole request — no partial recount) → assignee → company prefix and a NEW number (taken before the transaction; skipped if the write fails).
/// ONE snapshot transaction: the selected bins' snapshot (AVAILABLE rows with stock of the selected SKUs + the window's movement ids), a new
/// <c>Cycle</c> count — warehouse and <c>Blind</c> copied, scope = the selected lines' bins and SKUs, released at once — whose lines are EXACTLY
/// the selected keys (expected = the snapshot's balance or 0; <c>Origin = Recount</c>, depth + 1), its <c>RecountExcludedKeys</c> = the snapshot's
/// other keys on those bins × SKUs, the selected bins' claims handed over (a bin the source does not hold is claimed anew — another open count's
/// claim refuses it: 409 <c>COUNT_SCOPE_OVERLAP</c>), the source lines Recounted with the decision, and the source closed when every line is final.
/// </summary>
public sealed class RequestCountRecountHandler : CycleCountCommandHandler, IRequestHandler<RequestCountRecountCommand, Response<CycleCountDto>>
{
    private readonly CycleCountChecks _checks;

    public RequestCountRecountHandler(ICycleCountRepository repository, CycleCountSupport support, CycleCountChecks checks) : base(repository, support)
        => _checks = checks;

    public async Task<Response<CycleCountDto>> Handle(RequestCountRecountCommand request, CancellationToken ct)
    {
        const string op = CycleCountContract.OpRequestCountRecount;
        var input = request.Input;
        var lineIds = input.LineIds.Distinct().ToList();
        if (lineIds.Count == 0 || lineIds.Count != input.LineIds.Count || lineIds.Count > CycleCountContract.MaxRecountLines)
            return CycleCountSupport.Invalid<CycleCountDto>($"lineIds must name 1-{CycleCountContract.MaxRecountLines} distinct lines.");
        if (CycleCountRules.Reason(input.Reason) is not { } reason) return CycleCountSupport.ReasonRequired<CycleCountDto>();
        var note = CycleCountSupport.Trimmed(input.Note);
        if (note is { Length: > CycleCountRules.MaxNoteLength }) return CycleCountSupport.Invalid<CycleCountDto>("note must be at most 500 characters.");
        var today = DateOnly.FromDateTime(Support.Now().UtcDateTime);
        var planned = today;
        if (input.PlannedDate is not null
            && (!DateOnly.TryParseExact(input.PlannedDate, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out planned) || planned < today))
            return CycleCountSupport.Invalid<CycleCountDto>("plannedDate must be a date (yyyy-MM-dd), today (UTC) or later.");

        var plannedText = planned.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var fingerprint = CycleCountSupport.Fingerprint(("op", op), ("countId", request.CountId.ToString("D")),
            ("lineIds", CycleCountSupport.List(lineIds.Order().Select(x => x.ToString("D")))), ("reason", reason),
            ("assignedTo", input.AssignedTo?.ToString("D")), ("plannedDate", input.PlannedDate is null ? null : plannedText), ("note", note));
        if (await ReplayRecountAsync(request.CountId, request.IdempotencyKey, fingerprint, ct) is { } replay) return replay;

        var source = await Repository.FindAsync(request.CountId, ct);
        if (source is null) return CycleCountSupport.NotFound<CycleCountDto>();
        if (source.Version != request.ExpectedVersion) return CycleCountSupport.Conflict<CycleCountDto>();
        if (!CycleCountRules.TakesDecisions(source)) return CycleCountSupport.InvalidState<CycleCountDto>(source.Status);

        var selected = (await Repository.FindLinesAsync(lineIds, ct)).Where(l => l.CountId == source.Id).ToList();
        if (selected.Count != lineIds.Count)
            return LinesRefused("Every line must belong to this count.", lineIds.Except(selected.Select(l => l.Id)).First(), null);
        if (selected.FirstOrDefault(l => !CycleCountDecisionRules.CanRecount(l)) is { } wrong)
            return LinesRefused($"The line is {wrong.Status}; only an awaiting or refused line is recounted.", wrong.Id, wrong.Status);
        var policy = CycleCountPolicy.Of(await Repository.FindSettingsAsync(ct));
        if (selected.FirstOrDefault(l => l.RecountDepth >= policy.MaxRecounts) is { } deep)
        {
            return CycleCountSupport.Fail<CycleCountDto>(CycleCountErrorCodes.RecountLimit, "A selected line has reached the recount limit; nothing was recounted.", 409,
                new Dictionary<string, object?> { ["lineId"] = deep.Id.ToString("D"), ["maxRecounts"] = policy.MaxRecounts });
        }

        if (await _checks.AssigneeAsync<CycleCountDto>(input.AssignedTo, ct) is { } assigneeRefusal) return assigneeRefusal;
        var (companyCode, codeRefusal) = await _checks.CompanyCodeAsync<CycleCountDto>(ct);
        if (codeRefusal is not null) return codeRefusal;
        var (number, numberRefusal) = await _checks.NumberAsync<CycleCountDto>(companyCode!, ct);
        if (numberRefusal is not null) return numberRefusal;

        var bins = selected.Select(l => l.LocationId).Distinct().OrderBy(x => x).ToList();
        var skus = selected.Select(l => l.SkuId).Distinct().OrderBy(x => x).ToList();
        var snapshotAt = Support.Now();
        var scope = new CycleCountScopeRequest(source.WarehouseId, false, bins, skus, CycleCountRules.WindowStart(snapshotAt));
        CycleCount? created = null;
        try
        {
            var outcome = await Repository.InTransactionAsync(async (session, tx) =>
            {
                created = null;
                var sourceLines = await session.ReadLinesAsync(source.Id, tx);
                var current = sourceLines.Where(l => lineIds.Contains(l.Id)).ToList();
                if (current.Count != selected.Count || current.Any(l => selected.Single(s => s.Id == l.Id).Version != l.Version))
                    return new CycleCountWriteOutcome(0, source.Id); // a selected line moved since it was read — 409 below

                var snapshot = await session.ReadScopeSnapshotAsync(scope, tx);
                var now = Support.Now();
                var recount = NewCount(source, number!, companyCode!, bins, skus, plannedText, input.AssignedTo, note, snapshotAt, snapshot.TransactionIds, now,
                    fingerprint);
                var keys = current.Select(CycleCountLineKey.Of).ToHashSet();
                var lines = current.OrderBy(l => l.LocationId).ThenBy(l => l.SkuId).ThenBy(l => l.LotId, StringComparer.Ordinal)
                    .Select(l => RecountLine(recount, l, snapshot.Rows.Where(r => Key(r) == CycleCountLineKey.Of(l)).Sum(r => r.OnHand), now)).ToList();
                recount.LineCount = lines.Count;
                recount.RecountExcludedKeys = snapshot.Rows.Select(Key).Distinct().Where(k => !keys.Contains(k))
                    .Select(k => new CycleCountKeyRef { LocationId = k.LocationId, SkuId = k.SkuId, SkuLevel = k.SkuLevel, LotId = k.LotId, StockStatus = k.StockStatus })
                    .ToList();

                // the source: its selected lines Recounted (the decision), the head's rows, and its close when every line is now final
                var head = source.Copy();
                head.Version = source.Version + 1;
                head.UpdatedAt = now;
                var recounted = current.Select(l =>
                {
                    var changed = l.Copy();
                    changed.Status = CycleCountLineStatus.Recounted;
                    changed.Decision = new CycleCountDecision
                        { Kind = CycleCountDecisionKind.Recount, Source = CycleCountDecisionSource.User, By = Support.UserId, At = now, Reason = reason };
                    changed.Version = l.Version + 1;
                    changed.UpdatedAt = now;
                    return (Line: changed, Expected: l.Version);
                }).ToList();

                var transferred = await session.TransferBinClaimsAsync(source.Id, recount.Id, bins, now, tx);
                var claimed = bins.Except(transferred).Select(bin => new CycleCountBinClaim
                {
                    Id = Guid.NewGuid(), CreatedAt = now, Version = 1, BinId = bin, CountId = recount.Id, Open = true, ClaimedAt = now
                }).ToList();

                var sourceEvents = new List<CycleCountEvent>
                {
                    Support.Event(head, CycleCountEventKind.RecountRequested, now, new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        ["recountCountId"] = recount.Id.ToString("D"), ["recountNumber"] = recount.Number, ["lineCount"] = CycleCountSupport.Int(current.Count),
                        ["reason"] = reason, ["transferredBinClaims"] = CycleCountSupport.Int(transferred.Count),
                        ["assignedTo"] = input.AssignedTo?.ToString("D"), ["plannedDate"] = plannedText, ["note"] = note
                    }, request.IdempotencyKey, op, fingerprint)
                };
                sourceEvents.AddRange(recounted.Select(r => Support.Event(head, CycleCountEventKind.RecountRequested, now,
                    new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        ["recountCountId"] = recount.Id.ToString("D"), ["previousStatus"] = current.Single(c => c.Id == r.Line.Id).Status.ToString(),
                        ["recountDepth"] = CycleCountSupport.Int(r.Line.RecountDepth + 1)
                    }, lineId: r.Line.Id)));
                var after = sourceLines.Select(l => recounted.FirstOrDefault(r => r.Line.Id == l.Id).Line ?? l).ToList();
                var closes = after.All(CycleCountRules.IsFinal);
                if (closes)
                {
                    head.Status = CycleCountStatus.Closed;
                    sourceEvents.Add(Support.Event(head, CycleCountEventKind.Closed, now,
                        new Dictionary<string, string?>(StringComparer.Ordinal) { ["reason"] = "AllLinesFinal" }));
                }

                var recountEvents = new List<CycleCountEvent>
                {
                    Support.Event(recount, CycleCountEventKind.Created, now, new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        ["number"] = recount.Number, ["recountOfId"] = source.Id.ToString("D"), ["countType"] = recount.CountType.ToString(),
                        ["blind"] = recount.Blind ? "true" : "false", ["plannedDate"] = plannedText, ["assignedTo"] = input.AssignedTo?.ToString("D"), ["note"] = note
                    }),
                    // F-13: the Released row carries counts only — never a per-line expected quantity
                    Support.Event(recount, CycleCountEventKind.Released, now, new Dictionary<string, string?>(StringComparer.Ordinal)
                    {
                        ["snapshotAt"] = CycleCountSupport.Wire(snapshotAt), ["binCount"] = CycleCountSupport.Int(bins.Count),
                        ["lineCount"] = CycleCountSupport.Int(lines.Count), ["claimedBinCount"] = CycleCountSupport.Int(bins.Count),
                        ["excludedKeyCount"] = CycleCountSupport.Int(recount.RecountExcludedKeys.Count)
                    })
                };

                if (!await session.ReplaceCountAsync(head, source.Version, tx)) return new CycleCountWriteOutcome(0, source.Id);
                if (!await session.ReplaceLinesAsync(recounted, tx)) throw new RecountLostException();
                await session.InsertCountAsync(recount, tx);
                await session.InsertLinesAsync(lines, tx);
                await session.InsertBinClaimsAsync(claimed, tx);
                if (closes) await session.ReleaseBinClaimsAsync(source.Id, now, tx);
                await session.InsertEventsAsync([.. sourceEvents, .. recountEvents], tx);
                created = recount;
                return new CycleCountWriteOutcome(1, recount.Id);
            }, ct);
            if (outcome.Affected == 1 && created is not null) return Response<CycleCountDto>.Success(CycleCountReads.Dto(created), 201);
        }
        catch (CycleCountInventoryUnavailableException)
        {
            return CycleCountSupport.Fail<CycleCountDto>(CycleCountErrorCodes.InventoryUnavailable, "The stock snapshot could not be read; nothing was written.", 503);
        }
        catch (CycleCountDuplicateException ex) when (ex.Index == CycleCountUniqueIndex.BinClaim)
        {
            return await ReplayRecountAsync(source.Id, request.IdempotencyKey, fingerprint, ct)
                   ?? CycleCountSupport.Fail<CycleCountDto>(CycleCountErrorCodes.ScopeOverlap, "A bin of the recount is being counted by another open count.", 409);
        }
        catch (CycleCountDuplicateException)
        {
            // a concurrent twin of this request committed first — answered below
        }
        catch (RecountLostException)
        {
            // a selected line changed under the write — 409 below (the head write is rolled back with it)
        }

        return await ReplayRecountAsync(source.Id, request.IdempotencyKey, fingerprint, ct) ?? CycleCountSupport.Conflict<CycleCountDto>();
    }

    /// <summary>The source count's key event names the recount it created: same fingerprint → 200 + that count, other → 400.</summary>
    private async Task<Response<CycleCountDto>?> ReplayRecountAsync(Guid sourceId, string key, string fingerprint, CancellationToken ct)
    {
        var earlier = await Repository.FindKeyEventAsync(sourceId, Support.UserId, CycleCountContract.OpRequestCountRecount, key, ct);
        if (earlier is null) return null;
        if (earlier.RequestFingerprint != fingerprint) return CycleCountSupport.KeyReused<CycleCountDto>();
        var created = earlier.Data.TryGetValue("recountCountId", out var id) && Guid.TryParse(id, out var countId)
            ? await Repository.FindIncludingDeletedAsync(countId, ct)
            : null;
        return created is null ? CycleCountSupport.NotFound<CycleCountDto>() : Response<CycleCountDto>.Success(CycleCountReads.Dto(created));
    }

    private static Response<CycleCountDto> LinesRefused(string message, Guid lineId, CycleCountLineStatus? status)
        => CycleCountSupport.Fail<CycleCountDto>(CycleCountErrorCodes.InvalidState, message, 409,
            new Dictionary<string, object?> { ["lineId"] = lineId.ToString("D"), ["lineStatus"] = status?.ToString() });

    private static CycleCountLineKey Key(CycleCountSnapshotRow r) => new(r.LocationId, r.SkuId, r.SkuLevel, r.LotId, r.StockStatus);

    /// <summary>Pack §8.5 table: the recount's fields — derived from the selection, copied from the source, or taken from the request.</summary>
    private CycleCount NewCount(CycleCount source, string number, string companyCode, IReadOnlyList<Guid> bins, IReadOnlyList<Guid> skus, string plannedDate,
        Guid? assignedTo, string? note, DateTimeOffset snapshotAt, IReadOnlyList<Guid> snapshotTransactionIds, DateTimeOffset now, string fingerprint)
    {
        var id = Guid.NewGuid();
        return new CycleCount
        {
            Id = id, CreatedAt = now, Version = 1, Number = number, CompanyCodePrefix = companyCode, WarehouseId = source.WarehouseId,
            CountType = CycleCountType.Cycle, Blind = source.Blind, ScopeLocationIds = [.. bins], ScopeSkuIds = [.. skus], PlannedDate = plannedDate,
            AssignedTo = assignedTo, Note = note, Status = CycleCountStatus.Released, SnapshotAt = snapshotAt, SnapshotTransactionIds = [.. snapshotTransactionIds],
            ScopeBinIds = [.. bins], RecountOfId = source.Id, CreatedBy = Support.UserId,
            // the create-key index is per creating user + key; a recount is keyed on its SOURCE's timeline, so its own value never meets a create key
            IdempotencyKey = "recount:" + id.ToString("D"), RequestFingerprint = fingerprint, CorrelationId = Support.CorrelationId
        };
    }

    /// <summary>A selected key as a recount line: <c>Origin = Recount</c>, the snapshot's balance (0 when none — a found line is recounted too).</summary>
    private static CycleCountLine RecountLine(CycleCount recount, CycleCountLine source, decimal expected, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), CreatedAt = now, Version = 1, CountId = recount.Id, WarehouseId = recount.WarehouseId, LocationId = source.LocationId,
        ItemId = source.ItemId, SkuId = source.SkuId, SkuLevel = source.SkuLevel, LotId = source.LotId, StockStatus = source.StockStatus,
        BaseUomId = source.BaseUomId, Origin = CycleCountLineOrigin.Recount, RecountOfLineId = source.Id, RecountDepth = source.RecountDepth + 1,
        ExpectedQuantity = expected, SerialControlled = source.SerialControlled, Status = CycleCountLineStatus.Open
    };

    private sealed class RecountLostException() : Exception("A selected line changed since it was read.");
}
