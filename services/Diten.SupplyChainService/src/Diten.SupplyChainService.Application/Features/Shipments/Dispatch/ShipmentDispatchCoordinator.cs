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
        if (!await repository.TryBeginOrResumeDispatchAsync(context.Scope, s.Id, observed, next, ct)) return InProgress(next.N);
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
                    // Never a reason to post again: the attempt is blocked like CompensationFailed (§23.4, S23-11).
                    line.LastInventoryCode = KeyReused;
                    logger.LogError("MOD-0183 integrity fault: {Code} on issue of attempt {Attempt} line {Line}.", KeyReused, a.N, line.LineNumber);
                    if (!await BlockAsync(s, a, [KeyReused + " issue line " + line.LineNumber], ct)) return InProgress(a.N);
                    return Fail(ContractErrorCodes.InternalError, 500);
                case InventoryOutcome.Invalid:
                    line.LastInventoryCode = result.InventoryCode;
                    logger.LogError("MOD-0183 integrity fault: INVENTORY 400 {Code} on issue of attempt {Attempt} line {Line}.", result.InventoryCode, a.N, line.LineNumber);
                    return await CompensateAsync(s, a, request, fingerprint, line.LineNumber, result, ct);
                case InventoryOutcome.Forbidden:
                    logger.LogWarning("MOD-0183 dispatch: INVENTORY 403 {Code} on attempt {Attempt} line {Line}.", result.InventoryCode, a.N, line.LineNumber);
                    await StopAsync(s, a, line, definite: !resend, ct);
                    return FailWith(ContractErrorCodes.InvalidRequest, 403, new JsonObject { ["inventoryCode"] = result.InventoryCode });
                default:
                    await StopAsync(s, a, line, definite: !result.Ambiguous && !resend, ct);
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
            if (held is null || held.N != a.N || held.LeaseId != a.LeaseId || held.State != DispatchState.InProgress || held.Lines.Any(l => l.IssueTransactionId is null))
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
        if (await ReverseAllAsync(s, a, failedLine, faults, ct) is { } stopped) return stopped;
        if (!await repository.CloseCompensatedAttemptAsync(context.Scope, s.Id, a, a.LeaseId, ct)) return InProgress(a.N);
        await AuditAsync(s, a, "Compensated", faults, ct);
        var (status, code, details) = failure.Outcome == InventoryOutcome.Invalid
            ? (500, ContractErrorCodes.InternalError, (JsonObject?)null)
            : (422, ContractErrorCodes.InventoryRefused, new JsonObject { ["lineNumber"] = failedLine, ["inventoryCode"] = failure.InventoryCode });
        if (details is not null && failure.Details is not null) details["inventory"] = failure.Details.DeepClone();
        await repository.StoreOutcomeReceiptAsync(context.Scope, s.Id, Operation, context.IdempotencyKey, fingerprint, context.CorrelationId, status, code,
            details?.ToJsonString(), ct);
        return FailWith(code, status, details);
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
        if (await ReverseAllAsync(s, a, null, faults, ct) is { } stopped) return stopped;
        if (!await repository.CloseCompensatedAttemptAsync(context.Scope, s.Id, a, a.LeaseId, ct)) return InProgress(a.N);
        await AuditAsync(s, a, "Compensated", faults, ct);
        return null;
    }

    /// <summary>B′ then the REVERSALs in stored order; null when every posted line is reversed, else the stopping answer.</summary>
    private async Task<Response<JsonObject>?> ReverseAllAsync(Shipment s, DispatchAttempt a, string? refusedLine, List<string> faults, CancellationToken ct)
    {
        a.ReversalDate ??= clock.GetUtcNow();
        // B′: a sent line without an issue and without a definite answer may have been posted by a crashed or paused
        // writer — learn its id first. A line INVENTORY refused (or answered KEY_REUSED) is known not to be ours to reverse.
        foreach (var line in a.Lines.Where(l => l.Sent && l.IssueTransactionId is null && l.LineNumber != refusedLine && l.LastInventoryCode is null))
        {
            if (!await RenewAsync(s, a, ct)) return InProgress(a.N);
            var resent = await PostAsync(s, a.IssueKey(s.Id, line.LineNumber), line.RequestJson!, ct);
            if (resent.Outcome == InventoryOutcome.Posted) { line.IssueTransactionId = resent.TransactionId; line.LotId = resent.LotId ?? LotOf(line.RequestJson!); }
            else if (resent.Outcome == InventoryOutcome.Refused) line.LastInventoryCode = resent.InventoryCode;
            else if (resent.Outcome == InventoryOutcome.Invalid && resent.InventoryCode == KeyReused)
            {
                // The key holds other bytes in INVENTORY: an integrity fault for an operator, never a reason to post again.
                line.LastInventoryCode = KeyReused; faults.Add(KeyReused + " resend line " + line.LineNumber);
                logger.LogError("MOD-0183 integrity fault: {Code} on resend of attempt {Attempt} line {Line}.", KeyReused, a.N, line.LineNumber);
            }
            else return await FailCompensationAsync(s, a, resent, faults, ct);
        }
        foreach (var line in a.Lines.Where(l => l.IssueTransactionId is not null && !l.IsReversed()))
        {
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
        return null;
    }

    /// <summary>A REVERSAL failed (5xx, timeout, 403, 400, other 422) → CompensationFailed, 409 with the lines not reversed.</summary>
    private async Task<Response<JsonObject>> FailCompensationAsync(Shipment s, DispatchAttempt a, InventoryPostResult result, List<string> faults, CancellationToken ct)
    {
        logger.LogError("MOD-0183 compensation failed: attempt {Attempt}, INVENTORY {Status} {Code}.", a.N, result.HttpStatus, result.InventoryCode);
        if (!await BlockAsync(s, a, faults.Append("REVERSAL " + (result.InventoryCode ?? result.HttpStatus.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToList(), ct))
            return InProgress(a.N);
        var open = new JsonArray(a.Lines.Where(l => l.IssueTransactionId is not null && !l.IsReversed()).Select(l => (JsonNode)JsonValue.Create(l.LineNumber)!).ToArray());
        return FailWith(ContractErrorCodes.DispatchCompensationFailed, 409, new JsonObject { ["lines"] = open });
    }

    private async Task<bool> BlockAsync(Shipment s, DispatchAttempt a, List<string> faults, CancellationToken ct)
    {
        a.State = DispatchState.CompensationFailed; a.LeaseUntil = clock.GetUtcNow();
        if (!await repository.SaveDispatchAttemptAsync(context.Scope, s.Id, a, a.LeaseId, ct)) return false;
        await AuditAsync(s, a, "CompensationFailed", faults, ct);
        return true;
    }

    /// <summary>403 / 503: the attempt stays InProgress; a line sent for the first time that INVENTORY certainly did not post
    /// loses its sent marker, and an attempt that posted nothing at all is removed — "before the first post nothing changes"
    /// (§23.2, §23.3). A resent line keeps its marker: an earlier send of it may have been posted.</summary>
    private async Task StopAsync(Shipment s, DispatchAttempt a, DispatchAttemptLine line, bool definite, CancellationToken ct)
    {
        if (!definite) return;      // INVENTORY may still process it: keep the sent marker and the lease (request timeout + 30 s)
        line.Sent = false; line.RequestJson = null;
        if (a.Lines.All(l => !l.Sent && l.IssueTransactionId is null))
        {
            await repository.DropUnpostedAttemptAsync(context.Scope, s.Id, a.N, a.LeaseId, ct);
            return;
        }
        a.LeaseUntil = clock.GetUtcNow();
        await repository.SaveDispatchAttemptAsync(context.Scope, s.Id, a, a.LeaseId, ct);
    }

    private async Task<bool> RenewAsync(Shipment s, DispatchAttempt a, CancellationToken ct)
    {
        a.LeaseUntil = clock.GetUtcNow() + options.Lease;
        return await repository.SaveDispatchAttemptAsync(context.Scope, s.Id, a, a.LeaseId, ct);
    }

    private Task<InventoryPostResult> PostAsync(Shipment s, string key, string body, CancellationToken ct) =>
        inventory.PostAsync(new InventoryPostRequest(key, body, context.Scope.TenantId, s.LegalEntityId, context.CorrelationId, context.Authorization), ct);

    private Task AuditAsync(Shipment s, DispatchAttempt a, string outcome, IReadOnlyList<string> faults, CancellationToken ct) =>
        repository.AppendDispatchAuditAsync(context.Scope, s.Id, new DispatchAuditEntry(a.N, outcome, context.CorrelationId, clock.GetUtcNow(),
            a.Lines.Where(l => l.IssueTransactionId is not null).Select(l => l.IssueTransactionId!).ToArray(),
            a.Lines.Where(l => l.ReversalTransactionId is not null).Select(l => l.ReversalTransactionId!).ToArray(), faults), ct);

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
