using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.Shipments.Commands;
using Diten.SupplyChainService.Application.Features.Shipments.Handlers.CommandHandlers;
using Diten.SupplyChainService.Domain.Features.Shipments;
namespace Diten.SupplyChainService.Application.Features.Shipments.Dispatch;

/// <summary>
/// MOD-0183 §23 (W-4, DL-1): <c>Planned → Dispatched</c> issues every line's stock in INVENTORY before the shipment changes
/// state, and the exit from a failed compensation. INVENTORY is called over HTTP, so this cannot be one Mongo transaction:
/// A — open or resume the attempt under a lease; B — post each line in stored order (sent marker first); B′ — resend sent
/// lines without an issue before compensating; C — reverse every posted line on a refusal; D — Dispatched, the frozen
/// ShipmentDispatched event, the receipt and the central audit in one transaction. Plan r3 §1.
/// </summary>
public sealed class ShipmentDispatchCoordinator(IShipmentRepository repository, IInventoryMovementClient inventory, LateWriterPolicy lateWriter,
    RequestContext context, DispatchOptions options, TimeProvider clock, ILogger<ShipmentDispatchCoordinator> logger)
{
    private const string Operation = "transition";
    private const string AlreadyReversed = "ALREADY_REVERSED";
    private const string KeyReused = "IDEMPOTENCY_KEY_REUSED";

    /// <summary>A transition to Dispatched, or to Cancelled from a CompensationFailed attempt (the exit).</summary>
    public async Task<Response<JsonObject>> TransitionAsync(TransitionShipmentCommand request, ShipmentStatus target, CancellationToken ct)
    {
        var fingerprint = RequestFingerprint.For(request);
        // The receipt is written only in D or with a compensated outcome, so a replay after success posts nothing (S23-3).
        if (await repository.FindReceiptAsync(context.Scope, Operation, context.IdempotencyKey, fingerprint, context.CorrelationId, ct) is { } replay)
            return Answer(replay);
        var s = await repository.GetAsync(context.Scope, request.ShipmentId, ct);
        if (s is null) return Fail(ContractErrorCodes.ShipmentNotFound, 404);
        if (s.CorrelationId != context.CorrelationId) return Fail(ContractErrorCodes.InvalidRequest, 400);
        if (s.DispatchAttempt?.State == DispatchState.CompensationFailed)
        {
            if (await ExitAsync(s, ct) is { } blocked) return blocked;
            s = await repository.GetAsync(context.Scope, request.ShipmentId, ct);
            if (s is null) return Fail(ContractErrorCodes.ShipmentNotFound, 404);
        }
        if (target != ShipmentStatus.Dispatched)
        {
            var cancelled = await repository.MutateAsync(context.Scope, request.ShipmentId, Operation, context.IdempotencyKey, fingerprint, context.CorrelationId,
                current => TransitionShipmentHandler.Decide(current, target, request, context), ct);
            return Answer(cancelled);
        }
        return await DispatchAsync(s, request, fingerprint, ct);
    }

    private async Task<Response<JsonObject>> DispatchAsync(Shipment s, TransitionShipmentCommand request, string fingerprint, CancellationToken ct)
    {
        if (!ShipmentLifecycle.Allows(s.Status, ShipmentStatus.Dispatched)) return Fail(ContractErrorCodes.InvalidShipmentTransition, 422);
        // INVENTORY requires skuLevel: refuse before any call (r4 F-4). Whether a line is lot-tracked is INVENTORY's to say.
        if (s.Lines.FirstOrDefault(l => string.IsNullOrWhiteSpace(l.SkuLevel)) is { } incomplete)
            return FailWith(ContractErrorCodes.ShipmentLineStockIncomplete, 422, new JsonObject { ["lineNumber"] = incomplete.LineNumber });
        var now = clock.GetUtcNow();
        var observed = s.DispatchAttempt;
        DispatchAttempt next;
        if (observed is null)
        {
            var at = request.Body.OccurredAt;
            next = new DispatchAttempt
            {
                N = s.NextDispatchAttemptNumber(), State = DispatchState.InProgress, OccurredAt = at, TransactionDate = at, PostingDate = at,
                CorrelationId = context.CorrelationId, Lines = s.Lines.Select(l => new DispatchAttemptLine { LineNumber = l.LineNumber }).ToList()
            };
        }
        else if (observed.State == DispatchState.InProgress && observed.LeaseUntil <= now)
            next = observed.Copy();      // resume after the lease: same n, same dates, same keys and bodies
        else return InProgress(observed.N);
        next.LeaseId = Guid.NewGuid(); next.LeaseUntil = now + options.Lease;
        if (!await repository.TryBeginOrResumeDispatchAsync(context.Scope, s.Id, observed, next, ct))
        {
            // Lost the race: to another dispatch (409) or to a transition that left Planned, e.g. Cancelled (422, review F-7).
            var current = await repository.GetAsync(context.Scope, s.Id, ct);
            if (current is null) return Fail(ContractErrorCodes.ShipmentNotFound, 404);
            if (!ShipmentLifecycle.Allows(current.Status, ShipmentStatus.Dispatched)) return Fail(ContractErrorCodes.InvalidShipmentTransition, 422);
            return current.DispatchAttempt?.State == DispatchState.CompensationFailed ? Blocked(current.DispatchAttempt) : InProgress(next.N);   // re-review R-3
        }
        // An attempt whose compensation had started is never posted or completed again: the resume finishes the
        // compensation and answers the stored refusal (re-review N-1).
        if (next.IsCompensating()) return await FinishCompensationAsync(s, next, fingerprint, ct);
        return await RunAsync(s, next, request, fingerprint, ct);
    }

    private async Task<Response<JsonObject>> RunAsync(Shipment s, DispatchAttempt a, TransitionShipmentCommand request, string fingerprint, CancellationToken ct)
    {
        foreach (var line in a.Lines)
        {
            if (line.IssueTransactionId is not null) continue;
            // A line already marked sent is resent with its stored body (B′ within the order): a replay returns the issue a
            // crashed or paused writer made, a 422 means nothing was posted.
            var resend = line.Sent;
            if (!resend) { line.Sent = true; line.RequestJson = InventoryMovementMapper.Issue(s, s.Lines.First(l => l.LineNumber == line.LineNumber), a); }
            if (!await RenewAsync(s, a, ct)) return InProgress(a.N);
            var result = await PostAsync(s, a.IssueKey(s.Id, line.LineNumber), line.RequestJson!, ct);
            switch (result.Outcome)
            {
                case InventoryOutcome.Posted:
                    line.IssueTransactionId = result.TransactionId; line.LotId = result.LotId ?? LotOf(line.RequestJson!);
                    if (!await repository.SaveDispatchAttemptAsync(context.Scope, s.Id, a, a.LeaseId, ct))
                    {
                        await lateWriter.HandleAsync(s.Id, a.N, line.LineNumber, line.RequestJson!, result.TransactionId!, ct);
                        return InProgress(a.N);
                    }
                    break;
                case InventoryOutcome.Refused:
                    line.LastInventoryCode = result.InventoryCode;
                    return await CompensateAsync(s, a, request, fingerprint, line.LineNumber, result, ct);
                case InventoryOutcome.Invalid when result.InventoryCode == KeyReused:
                    // Never a reason to post again: the attempt is blocked like CompensationFailed (§23.4, S23-11) — after the
                    // lines it did record are reversed (review 3 F-1). The command answers 500 like any INVENTORY 400.
                    line.LastInventoryCode = KeyReused;
                    logger.LogError("MOD-0183 integrity fault: {Code} on issue of attempt {Attempt} line {Line}.", KeyReused, a.N, line.LineNumber);
                    var blocked = await CompensateAsync(s, a, request, fingerprint, line.LineNumber, result, ct);
                    return blocked.ErrorCode == ContractErrorCodes.DispatchCompensationFailed ? Fail(ContractErrorCodes.InternalError, 500) : blocked;
                case InventoryOutcome.Invalid:
                    line.LastInventoryCode = result.InventoryCode;
                    logger.LogError("MOD-0183 integrity fault: INVENTORY 400 {Code} on issue of attempt {Attempt} line {Line}.", result.InventoryCode, a.N, line.LineNumber);
                    return await CompensateAsync(s, a, request, fingerprint, line.LineNumber, result, ct);
                case InventoryOutcome.Forbidden:
                    logger.LogWarning("MOD-0183 dispatch: INVENTORY 403 {Code} on attempt {Attempt} line {Line}.", result.InventoryCode, a.N, line.LineNumber);
                    await StopAsync(s, a, line, result, resend, ct);
                    return FailWith(ContractErrorCodes.InvalidRequest, 403, new JsonObject { ["inventoryCode"] = result.InventoryCode });
                default:
                    await StopAsync(s, a, line, result, resend, ct);
                    return Fail(ContractErrorCodes.DependencyUnavailable, 503);
            }
        }
        return await CompleteAsync(s, a, request, fingerprint, ct);
    }

    /// <summary>D: every line has its issue → Dispatched with event, receipt and central audit in one transaction (G9).</summary>
    private async Task<Response<JsonObject>> CompleteAsync(Shipment s, DispatchAttempt a, TransitionShipmentCommand request, string fingerprint, CancellationToken ct)
    {
        var result = await repository.MutateAsync(context.Scope, s.Id, Operation, context.IdempotencyKey, fingerprint, context.CorrelationId, current =>
        {
            if (current is null) return ShipmentChange.Fail(ContractErrorCodes.ShipmentNotFound, 404);
            var held = current.DispatchAttempt;
            // A compensating attempt (a reversal date, a stored refusal or a reversed line) never completes (re-review N-1).
            if (held is null || held.N != a.N || held.LeaseId != a.LeaseId || held.State != DispatchState.InProgress || held.Lines.Any(l => l.IssueTransactionId is null)
                || held.IsCompensating())
                return ShipmentChange.Fail(ContractErrorCodes.DispatchInProgress, 409);
            if (!ShipmentLifecycle.Allows(current.Status, ShipmentStatus.Dispatched)) return ShipmentChange.Fail(ContractErrorCodes.InvalidShipmentTransition, 422);
            held.State = DispatchState.Completed;
            current.Status = ShipmentStatus.Dispatched; current.DispatchedAt = held.OccurredAt; current.UpdatedBy = context.Scope.ActorId; current.Version++;
            return new ShipmentChange(current, null, 200, held.OccurredAt, request.Body.ReasonCode, request.Body.Note);
        }, ct);
        return result.ErrorCode == ContractErrorCodes.DispatchInProgress ? InProgress(a.N) : Answer(result);
    }

    /// <summary>C: a line was refused (422) or our issue was malformed (400) → reverse every posted line, then answer.</summary>
    private async Task<Response<JsonObject>> CompensateAsync(Shipment s, DispatchAttempt a, TransitionShipmentCommand request, string fingerprint,
        string failedLine, InventoryPostResult failure, CancellationToken ct)
    {
        var faults = new List<string>();
        if (failure.Outcome == InventoryOutcome.Invalid) faults.Add("INVENTORY 400 " + failure.InventoryCode + " issue line " + failedLine);
        var (status, code, details) = failure.Outcome == InventoryOutcome.Invalid
            ? (500, ContractErrorCodes.InternalError, (JsonObject?)null)
            : (422, ContractErrorCodes.InventoryRefused, new JsonObject { ["lineNumber"] = failedLine, ["inventoryCode"] = failure.InventoryCode });
        if (details is not null && failure.Details is not null) details["inventory"] = failure.Details.DeepClone();
        // Durable before the first REVERSAL (re-review N-1) and the only place the reversal date is set (R-2): the date
        // and the answer, so a holder that resumes
        // this attempt after a crash or a lost lease continues the compensation instead of posting the lines again.
        a.ReversalDate ??= clock.GetUtcNow();
        a.Refusal = new DispatchRefusal(failedLine, status, code, details?.ToJsonString(), [.. faults]);
        if (!await repository.SaveDispatchAttemptAsync(context.Scope, s.Id, a, a.LeaseId, ct)) return InProgress(a.N);
        return await FinishCompensationAsync(s, a, fingerprint, ct);
    }

    /// <summary>C, from its start or from a resume: reverse every posted line, then close Compensated with the stored answer.</summary>
    private async Task<Response<JsonObject>> FinishCompensationAsync(Shipment s, DispatchAttempt a, string fingerprint, CancellationToken ct)
    {
        // An attempt compensating without a stored answer (written by an earlier build) answers like an INVENTORY 400.
        var refusal = a.Refusal ?? new DispatchRefusal("", 500, ContractErrorCodes.InternalError, null, []);
        var faults = refusal.Faults.ToList();
        if (await ReverseAllAsync(s, a, refusal.LineNumber, faults, ct) is { } stopped) return stopped;
        // Close, audit and the receipt of THIS command's key commit together (review F-3). A resumer stores its own key's
        // receipt; the key that opened the attempt has none if that request died, and its retry opens a{n+1} on a fully
        // reversed a{n} (review 3 F-5).
        var receipt = new DispatchOutcomeReceipt(Operation, context.IdempotencyKey, fingerprint, context.CorrelationId, refusal.StatusCode, refusal.ErrorCode, refusal.DetailsJson);
        if (!await SettleAsync(s, a, DispatchState.Compensated, faults, receipt, ct)) return InProgress(a.N);
        return FailWith(refusal.ErrorCode, refusal.StatusCode, refusal.DetailsJson is null ? null : JsonNode.Parse(refusal.DetailsJson)!.AsObject());
    }

    /// <summary>The exit (F-4): resend the open reversals; all done → Compensated and null, else the 409 that blocks.</summary>
    private async Task<Response<JsonObject>?> ExitAsync(Shipment s, CancellationToken ct)
    {
        var observed = s.DispatchAttempt!;
        var now = clock.GetUtcNow();
        if (observed.LeaseUntil > now) return InProgress(observed.N);
        var a = observed.Copy(); a.LeaseId = Guid.NewGuid(); a.LeaseUntil = now + options.Lease;
        if (!await repository.TryBeginOrResumeDispatchAsync(context.Scope, s.Id, observed, a, ct)) return InProgress(a.N);
        var faults = new List<string>();
        if (await ReverseAllAsync(s, a, a.Refusal?.LineNumber, faults, ct) is { } stopped) return stopped;   // review 3 F-2
        return await SettleAsync(s, a, DispatchState.Compensated, faults, null, ct) ? null : InProgress(a.N);
    }

    /// <summary>B′ then the REVERSALs in stored order; null when every posted line is reversed, else the stopping answer.</summary>
    private async Task<Response<JsonObject>?> ReverseAllAsync(Shipment s, DispatchAttempt a, string? refusedLine, List<string> faults, CancellationToken ct)
    {
        // B′: a sent line without an issue and without a definite answer may have been posted by a crashed or paused
        // writer — learn its id first. A line INVENTORY refused (422) or found malformed (400) holds nothing. A line whose
        // key answered KEY_REUSED may hold an unrecorded movement: it is resent, and it stays unresolved (re-review N-2).
        // An unresolved line no longer stops the reversals: every recorded issue is still reversed, and only then does the
        // attempt block on the unresolved lines, so the stock we know of comes back (review 3 F-1).
        var unresolved = new List<string>(); InventoryPostResult? unresolvedAnswer = null;
        foreach (var line in a.Lines.Where(l => l.Sent && l.IssueTransactionId is null
            && (l.LastInventoryCode is KeyReused || (l.LastInventoryCode is null && l.LineNumber != refusedLine))))
        {
            if (!await RenewAsync(s, a, ct)) return InProgress(a.N);
            var resent = await PostAsync(s, a.IssueKey(s.Id, line.LineNumber), line.RequestJson!, ct);
            if (resent.Outcome == InventoryOutcome.Posted) { line.IssueTransactionId = resent.TransactionId; line.LotId = resent.LotId ?? LotOf(line.RequestJson!); }
            else if (resent.Outcome == InventoryOutcome.Refused) line.LastInventoryCode = resent.InventoryCode;
            else
            {
                if (resent.Outcome == InventoryOutcome.Invalid && resent.InventoryCode == KeyReused)
                {
                    // §23.4: an integrity fault that blocks the attempt like CompensationFailed, never a reason to post again.
                    line.LastInventoryCode = KeyReused; faults.Add(KeyReused + " resend line " + line.LineNumber);
                    logger.LogError("MOD-0183 integrity fault: {Code} on resend of attempt {Attempt} line {Line}.", KeyReused, a.N, line.LineNumber);
                }
                unresolved.Add(line.LineNumber); unresolvedAnswer = resent;
            }
        }
        foreach (var line in a.Lines.Where(l => l.IssueTransactionId is not null && !l.IsReversed()))
        {
            // A late writer already reversed this issue under the same key and stored date (review F-5): take its id.
            if (s.DispatchLateWrites.FirstOrDefault(w => w.N == a.N && w.IssueTransactionId == line.IssueTransactionId && w.ReversalTransactionId is not null) is { } late)
            {
                line.ReversalTransactionId = late.ReversalTransactionId;
                if (!await repository.SaveDispatchAttemptAsync(context.Scope, s.Id, a, a.LeaseId, ct)) return InProgress(a.N);
                continue;
            }
            if (!await RenewAsync(s, a, ct)) return InProgress(a.N);
            var key = a.ReversalKey(s.Id, line.LineNumber);
            var result = await PostAsync(s, key, InventoryMovementMapper.Reversal(s.Id, line.RequestJson!, line.IssueTransactionId!, key, a.ReversalDate.Value), ct);
            if (result.Outcome == InventoryOutcome.Posted) line.ReversalTransactionId = result.TransactionId;
            else if (result.Outcome == InventoryOutcome.Refused && result.InventoryCode == AlreadyReversed)
            {
                // Reversed in INVENTORY by someone else: the stock is back, the line counts as reversed (r4 F-2).
                line.ExternallyReversed = true; faults.Add(AlreadyReversed + " issue " + line.IssueTransactionId + " line " + line.LineNumber);
                logger.LogError("MOD-0183 integrity fault: {Code} for issue {Issue} of attempt {Attempt} line {Line}.", AlreadyReversed, line.IssueTransactionId, a.N, line.LineNumber);
            }
            else
            {
                if (result.Outcome == InventoryOutcome.Invalid) faults.Add("INVENTORY 400 " + result.InventoryCode + " reversal line " + line.LineNumber);
                return await FailCompensationAsync(s, a, result, faults, ct);
            }
            if (!await repository.SaveDispatchAttemptAsync(context.Scope, s.Id, a, a.LeaseId, ct)) return InProgress(a.N);
        }
        if (unresolvedAnswer is not null) return await FailCompensationAsync(s, a, unresolvedAnswer, faults, ct, "resend");
        return null;
    }

    /// <summary>A REVERSAL failed (5xx, timeout, 403, 400, other 422) → CompensationFailed, 409 with the lines not reversed.</summary>
    private async Task<Response<JsonObject>> FailCompensationAsync(Shipment s, DispatchAttempt a, InventoryPostResult result, List<string> faults, CancellationToken ct,
        string call = "REVERSAL")
    {
        logger.LogError("MOD-0183 compensation failed: attempt {Attempt}, INVENTORY {Status} {Code}.", a.N, result.HttpStatus, result.InventoryCode);
        if (!await BlockAsync(s, a, faults.Append(call + " " + (result.InventoryCode ?? result.HttpStatus.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToList(), ct))
            return InProgress(a.N);
        return Blocked(a);
    }

    private static Response<JsonObject> Blocked(DispatchAttempt a) => FailWith(ContractErrorCodes.DispatchCompensationFailed, 409, new JsonObject
    {
        ["lines"] = new JsonArray(a.Lines.Where(l => l.IssueTransactionId is not null && !l.IsReversed()).Select(l => (JsonNode)JsonValue.Create(l.LineNumber)!).ToArray())
    });

    private async Task<bool> BlockAsync(Shipment s, DispatchAttempt a, List<string> faults, CancellationToken ct)
    {
        // Every block comes through a compensation, which stored the reversal date before its first REVERSAL, so a late
        // writer and the exit send the same bytes (re-review R-2).
        a.LeaseUntil = clock.GetUtcNow();
        return await SettleAsync(s, a, DispatchState.CompensationFailed, faults, null, ct);
    }

    /// <summary>403 / 5xx / timeout: the attempt stays InProgress (§23.2, §23.3). An ambiguous answer (timeout, 409, 500, 502,
    /// 504 — INVENTORY may have committed, review F-1) keeps the sent marker and the lease (request timeout + 30 s), so the
    /// next holder resends the same key. A definite one (403, INVENTORY's own 503) releases the lease (review F-6); a line
    /// sent for the first time then loses its marker, and an attempt that posted nothing at all is removed — "before the
    /// first post nothing changes". A resent line keeps its marker: an earlier send of it may have been posted.</summary>
    private async Task StopAsync(Shipment s, DispatchAttempt a, DispatchAttemptLine line, InventoryPostResult result, bool resend, CancellationToken ct)
    {
        if (result.Ambiguous) return;
        a.LeaseUntil = clock.GetUtcNow();
        if (resend)
        {
            await repository.SaveDispatchAttemptAsync(context.Scope, s.Id, a, a.LeaseId, ct);
            return;
        }
        line.Sent = false; line.RequestJson = null;
        if (a.Lines.All(l => !l.Sent && l.IssueTransactionId is null))
        {
            await repository.DropUnpostedAttemptAsync(context.Scope, s.Id, a.N, a.LeaseId, ct);
            return;
        }
        await repository.SaveDispatchAttemptAsync(context.Scope, s.Id, a, a.LeaseId, ct);
    }

    private async Task<bool> RenewAsync(Shipment s, DispatchAttempt a, CancellationToken ct)
    {
        a.LeaseUntil = clock.GetUtcNow() + options.Lease;
        return await repository.SaveDispatchAttemptAsync(context.Scope, s.Id, a, a.LeaseId, ct);
    }

    private Task<InventoryPostResult> PostAsync(Shipment s, string key, string body, CancellationToken ct) =>
        inventory.PostAsync(new InventoryPostRequest(key, body, context.Scope.TenantId, s.LegalEntityId, context.CorrelationId, context.Authorization), ct);

    private async Task<bool> SettleAsync(Shipment s, DispatchAttempt a, DispatchState outcome, IReadOnlyList<string> faults, DispatchOutcomeReceipt? receipt, CancellationToken ct)
    {
        var settled = a.Copy(); settled.State = outcome;
        var audit = new DispatchAuditEntry(a.N, outcome.ToString(), context.CorrelationId, clock.GetUtcNow(),
            a.Lines.Where(l => l.IssueTransactionId is not null).Select(l => l.IssueTransactionId!).ToArray(),
            a.Lines.Where(l => l.ReversalTransactionId is not null).Select(l => l.ReversalTransactionId!).ToArray(), faults);
        if (!await repository.SettleDispatchAttemptAsync(context.Scope, s.Id, settled, a.LeaseId, audit, receipt, ct)) return false;
        a.State = outcome; a.Rev = settled.Rev;
        return true;
    }

    private static string? LotOf(string requestJson)
    {
        using var body = JsonDocument.Parse(requestJson);
        return body.RootElement.TryGetProperty("lotId", out var lot) && lot.ValueKind == JsonValueKind.String ? lot.GetString() : null;
    }
    private static Response<JsonObject> InProgress(int n) => FailWith(ContractErrorCodes.DispatchInProgress, 409, new JsonObject { ["attempt"] = n });
    private static Response<JsonObject> Fail(string code, int status) => Response<JsonObject>.Fail(code, status);
    private static Response<JsonObject> FailWith(string code, int status, JsonObject? details) => Response<JsonObject>.FailWith(code, status, details);
    private static Response<JsonObject> Answer(ShipmentMutationResult r) => r.ErrorCode is null
        ? Response<JsonObject>.Success(ShipmentProjection.Mutation(r), r.StatusCode)
        : FailWith(r.ErrorCode, r.StatusCode, r.DetailsJson is null ? null : JsonNode.Parse(r.DetailsJson)!.AsObject());
}
