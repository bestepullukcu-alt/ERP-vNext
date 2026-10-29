using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Application.Features.Returns.Commands;
using Diten.SupplyChainService.Application.Features.Shipments.Dispatch;
using Diten.SupplyChainService.Domain.Features.Returns;
namespace Diten.SupplyChainService.Application.Features.Returns.Receipt;

/// <summary>
/// MOD-0186 §34 (E-7, DL-1): <c>InTransit → Received</c> posts one INVENTORY CUSTOMER_RETURN per line into QUARANTINE before
/// the return changes state. INVENTORY is called over HTTP, so this cannot be one Mongo transaction (plan r3 §1.2):
/// A — read the shipment's issue over the seam (first command only); B — open or resume the stored attempt under a lease,
/// apply <c>unpostedLines</c> and correct Refused lines under <c>a{k+1}</c>; C — post each line in stored order, sent marker
/// first; D — Received with each line's reference, the receipt, the in-product and central audit and the frozen
/// <c>ReturnReceived</c> event in one transaction. Posted lines are never reversed by the lease holder.
/// </summary>
public sealed class ReturnReceiptCoordinator(IReturnRepository repository, IReturnReferenceReader reader, IInventoryMovementClient inventory,
    ReturnLateWriterPolicy lateWriter, ReturnRequestContext context, DispatchOptions options, TimeProvider clock, ILogger<ReturnReceiptCoordinator> logger)
{
    public const string PreWpP1 = "PRE_WP_P1";
    public const string IntegrityFault = "INTEGRITY_FAULT";
    private const string InvalidRequest = "INVALID_REQUEST";
    // §34.2 permanent refusal: no retry can change these, so the line may be received without stock posting.
    private static readonly string[] Permanent = ["ITEM_NOT_ACTIVE", "SKU_LEVEL_NOT_STOCKABLE", "UNKNOWN_ITEM"];

    public async Task<Response<ReturnResponse>> ReceiveAsync(TransitionReturnCommand request, CancellationToken ct)
    {
        var b = request.Body;
        // The service sets each line's reference: a client value on Received is refused (§34.2).
        if (ReturnWire.OptionalText(b, "inventoryTransactionReferenceId") is not null) return Fail(InvalidRequest, 400);
        var warehouse = ReturnWire.OptionalText(b, "toWarehouseId");
        if (string.IsNullOrWhiteSpace(warehouse)) return Fail(InvalidRequest, 400);
        var location = ReturnWire.OptionalText(b, "toLocationId");
        var occurredAt = ReturnInstant.Normalize(b.GetProperty("occurredAt").GetString()!);
        if (!CustomerReturnMovementMapper.TryInstant(occurredAt, out var at)) return Fail(InvalidRequest, 400);
        var unposted = ReturnWire.UnpostedLines(b);
        if (unposted.Select(u => u.ShipmentLineNumber).Distinct(StringComparer.Ordinal).Count() != unposted.Count) return Fail(InvalidRequest, 400);
        var fingerprint = ReturnRequestFingerprint.Transition(b);
        try
        {
            // The receipt is written only in D, so a replay after success posts nothing and emits nothing (R34-2).
            if (await repository.FindTransitionReplayAsync(context.Scope, request.ReturnId, context.IdempotencyKey, fingerprint, context.CorrelationId, ct) is { } replay)
                return Answer(replay);
            var order = await repository.GetAsync(context.Scope, request.ReturnId, ct);
            if (order is null) return Fail(ContractErrorCodes.ReturnNotFound, 404);
            if (order.CorrelationRoot != context.CorrelationId) return Fail(ContractErrorCodes.CorrelationRootMismatch, 409);
            if (order.Status != ReturnStatus.InTransit) return Fail(ContractErrorCodes.InvalidReturnTransition, 422);
            var now = clock.GetUtcNow();
            var observed = order.ReceiptAttempt;
            if (observed is not null && observed.LeaseUntil > now) return InProgress(observed);
            ReceiptAttempt next;
            if (observed is null)
            {
                var opened = await OpenAsync(order, warehouse!, location, occurredAt, at, ct);
                if (opened.Failure is not null) return opened.Failure;
                next = opened.Attempt!;
            }
            else
            {
                next = observed.Copy();
                // §34.2 F-1: a command may change only a Refused line. With none Refused and none Unknown, values other than
                // the last accepted command's are a conflict that shows those values (review 01 F-1: never each line's, which
                // differ after a correction); an Unknown line keeps its request and is resent first (R34-13).
                if (!next.Lines.Any(l => l.State is ReceiptLineState.Refused or ReceiptLineState.Unknown)
                    && (Current(next).Warehouse != warehouse || Current(next).Location != location || Current(next).OccurredAt != occurredAt))
                    return FailWith(ContractErrorCodes.ReturnReceiptInProgress, 409, Stored(next));
            }
            next.CurrentWarehouseId = warehouse; next.CurrentLocationId = location; next.CurrentOccurredAt = occurredAt;
            if (Unpost(next, unposted) is { } refused) return refused;
            if (observed is not null) Correct(order, next, warehouse!, location, occurredAt, at);
            next.LeaseId = Guid.NewGuid(); next.LeaseUntil = now + options.Lease;
            if (!await repository.TryBeginOrResumeReceiptAsync(context.Scope, order.Id, observed, next, ct))
            {
                var current = await repository.GetAsync(context.Scope, order.Id, ct);
                if (current is null) return Fail(ContractErrorCodes.ReturnNotFound, 404);
                if (current.Status != ReturnStatus.InTransit) return Fail(ContractErrorCodes.InvalidReturnTransition, 422);
                return InProgress(current.ReceiptAttempt ?? next);
            }
            return await RunAsync(order, next, occurredAt, fingerprint, ct);
        }
        catch (ReturnFailureException e) { return Fail(e.Code, e.Status); }
    }

    /// <summary>A: the issue of the completed dispatch attempt, read over the shipment seam — never MOD-0183's store.</summary>
    private async Task<(ReceiptAttempt? Attempt, Response<ReturnResponse>? Failure)> OpenAsync(ReturnOrder order, string warehouse, string? location,
        string occurredAt, DateTimeOffset at, CancellationToken ct)
    {
        var source = await reader.ObserveAsync(order, ct);
        if (source.ShipmentId != order.ShipmentId) return (null, Fail(ContractErrorCodes.DependencyResponseInvalid, 502));
        var attempt = new ReceiptAttempt();
        foreach (var line in order.Lines)
        {
            var row = new ReceiptAttemptLine { ShipmentLineNumber = line.ShipmentLineNumber, K = 1, ToWarehouseId = warehouse, ToLocationId = location, OccurredAt = occurredAt };
            if (source.StockIssued is null)
            {
                // R34-10: shipped before the stock issue — received the old way, no INVENTORY call.
                row.State = ReceiptLineState.NotPosted; row.PostingReason = PreWpP1;
            }
            else
            {
                var issued = source.Lines.FirstOrDefault(s => s.LineNumber == line.ShipmentLineNumber);
                // R34-6: a stock issue without this line's movement is an integrity fault — no movement, no lot guessed.
                if (issued?.IssueTransactionId is null)
                    return (null, FailWith(ContractErrorCodes.ReturnSourceMovementMissing, 422, new JsonObject { ["shipmentLineNumber"] = line.ShipmentLineNumber }));
                row.State = ReceiptLineState.Pending; row.IssueTransactionId = issued.IssueTransactionId;
                row.RequestJson = CustomerReturnMovementMapper.Receipt(order.Id, order.ShipmentId, line, issued, 1, warehouse, location, at);
            }
            attempt.Lines.Add(row);
        }
        return (attempt, null);
    }

    /// <summary>§34.2 permanent refusal: only a line Refused with a permanent code (and that code as reason), or Blocked by
    /// INVENTORY 400 INVALID_REQUEST (reason INTEGRITY_FAULT), may be received without stock posting.</summary>
    private static Response<ReturnResponse>? Unpost(ReceiptAttempt attempt, IReadOnlyList<(string ShipmentLineNumber, string ReasonCode)> unposted)
    {
        foreach (var (lineNumber, reason) in unposted)
        {
            var line = attempt.Lines.FirstOrDefault(l => l.ShipmentLineNumber == lineNumber);
            var allowed = line is not null
                && ((line.State == ReceiptLineState.Refused && Permanent.Contains(line.LastInventoryCode) && reason == line.LastInventoryCode)
                    || (line.State == ReceiptLineState.Blocked && line.LastInventoryCode == InvalidRequest && reason == IntegrityFault));
            if (!allowed)
                return FailWith(ContractErrorCodes.ReturnLineNotUnpostable, 422, new JsonObject { ["shipmentLineNumber"] = lineNumber, ["lastInventoryCode"] = line?.LastInventoryCode });
            line!.State = ReceiptLineState.NotPosted; line.PostingReason = reason;
        }
        return null;
    }

    /// <summary>§34.2: a Refused line takes this command's values under the next key <c>a{k+1}</c> — a key answered 422 is
    /// never sent again, with corrected or unchanged values (r8). Every other line keeps its stored request.</summary>
    private static void Correct(ReturnOrder order, ReceiptAttempt attempt, string warehouse, string? location, string occurredAt, DateTimeOffset at)
    {
        foreach (var line in attempt.Lines.Where(l => l.State == ReceiptLineState.Refused))
        {
            line.K++; line.ToWarehouseId = warehouse; line.ToLocationId = location; line.OccurredAt = occurredAt;
            line.RequestJson = Reissue(line.RequestJson!, ReceiptAttempt.ReceiptKey(order.Id, line.ShipmentLineNumber, line.K), warehouse, location, at);
            line.State = ReceiptLineState.Pending; line.SentKey = null;
        }
    }

    private static string Reissue(string stored, string key, string warehouse, string? location, DateTimeOffset at)
    {
        var body = JsonNode.Parse(stored)!.AsObject();
        var instant = InventoryMovementMapper.Instant(at);
        body["idempotencyKey"] = key; body["toWarehouseId"] = warehouse; body["transactionDate"] = instant; body["postingDate"] = instant;
        if (location is null) body.Remove("toLocationId"); else body["toLocationId"] = location;
        return body.ToJsonString();
    }

    /// <summary>C: every line not NotPosted is sent in stored order with its stored key and body. A Posted line is resent and
    /// answers by replay (R34-3, R34-11); its stored transactionId never changes.</summary>
    private async Task<Response<ReturnResponse>> RunAsync(ReturnOrder order, ReceiptAttempt a, string occurredAt, string fingerprint, CancellationToken ct)
    {
        foreach (var line in a.Lines)
        {
            if (line.State == ReceiptLineState.NotPosted) continue;
            if (line.State == ReceiptLineState.Blocked)
            {
                // Exits only through unpostedLines (INTEGRITY_FAULT); an IDEMPOTENCY_KEY_REUSED line never does.
                logger.LogError("MOD-0186 integrity fault: return {Return} line {Line} is Blocked by INVENTORY 400 {Code}.", order.Id, line.ShipmentLineNumber, line.LastInventoryCode);
                return await StopAsync(order, a, Fail(ContractErrorCodes.InternalError, 500), ct);
            }
            var key = ReceiptAttempt.ReceiptKey(order.Id, line.ShipmentLineNumber, line.K);
            // The sent marker, conditional on the lease, before INVENTORY is called: a lost answer leaves the line Unknown.
            if (line.State == ReceiptLineState.Pending) { line.SentKey = key; line.State = ReceiptLineState.Unknown; }
            if (!await RenewAsync(order, a, ct)) return InProgress(a);
            var result = await inventory.PostAsync(new InventoryPostRequest(key, line.RequestJson!, context.Scope.TenantId, order.LegalEntityId, context.CorrelationId, context.Authorization), ct);
            var answeredAt = clock.GetUtcNow();
            if (line.State == ReceiptLineState.Posted)
            {
                if (result.Outcome == InventoryOutcome.Posted && result.TransactionId != line.TransactionId)
                {
                    // Review 01 F-2 (Natig 2026-10-29): INVENTORY answered the stored key with another movement — an integrity
                    // fault. The line becomes Unknown, the command stops, the pair is recorded; no automatic REVERSAL.
                    logger.LogError("MOD-0186 integrity fault: resend of posted return {Return} line {Line} a{K} answered {Other}, stored {Receipt}.",
                        order.Id, line.ShipmentLineNumber, line.K, result.TransactionId, line.TransactionId);
                    await repository.AppendReceiptAuditAsync(context.Scope, order.Id, new ReceiptAuditEntry(line.ShipmentLineNumber, line.K, result.TransactionId!, null,
                        "ResendTransactionMismatch:" + line.TransactionId, answeredAt, context.CorrelationId), ct);
                    line.State = ReceiptLineState.Unknown; line.LastHttpStatus = result.HttpStatus;
                    return await StopAsync(order, a, Fail(ContractErrorCodes.InternalError, 500), ct);
                }
                if (result.Outcome != InventoryOutcome.Posted)
                    logger.LogWarning("MOD-0186 receipt: resend of posted return {Return} line {Line} a{K} answered {Status} {Code}; stored {Receipt} kept.",
                        order.Id, line.ShipmentLineNumber, line.K, result.HttpStatus, result.InventoryCode, line.TransactionId);
                continue;
            }
            line.LastHttpStatus = result.HttpStatus;
            switch (result.Outcome)
            {
                case InventoryOutcome.Posted:
                    line.State = ReceiptLineState.Posted; line.TransactionId = result.TransactionId; line.LastInventoryCode = null;
                    if (!await repository.SaveReceiptAttemptAsync(context.Scope, order.Id, a, a.LeaseId, ct))
                    {
                        await lateWriter.HandleAsync(order, line.ShipmentLineNumber, line.K, line.RequestJson!, result.TransactionId!, answeredAt, ct);
                        return InProgress(a);
                    }
                    break;
                case InventoryOutcome.Refused:
                    // R34-3: the return stays InTransit; posted lines stay posted, no REVERSAL. The key a{k} is never resent.
                    line.State = ReceiptLineState.Refused; line.LastInventoryCode = result.InventoryCode;
                    var details = new JsonObject { ["shipmentLineNumber"] = line.ShipmentLineNumber, ["inventoryCode"] = result.InventoryCode };
                    if (result.Details is not null) details["inventory"] = result.Details.DeepClone();
                    return await StopAsync(order, a, FailWith(ContractErrorCodes.InventoryRefused, 422, details), ct);
                case InventoryOutcome.Invalid:
                    // INVENTORY 400 is our malformed request (or IDEMPOTENCY_KEY_REUSED): an integrity fault, the line Blocked.
                    line.State = ReceiptLineState.Blocked; line.LastInventoryCode = result.InventoryCode;
                    logger.LogError("MOD-0186 integrity fault: INVENTORY 400 {Code} on return {Return} line {Line} a{K}.", result.InventoryCode, order.Id, line.ShipmentLineNumber, line.K);
                    return await StopAsync(order, a, Fail(ContractErrorCodes.InternalError, 500), ct);
                case InventoryOutcome.Forbidden:
                    // R34-4: an authorization outcome, never 503. Nothing was stored; the line stays Unknown and is resent.
                    logger.LogWarning("MOD-0186 receipt: INVENTORY 403 {Code} on return {Return} line {Line}.", result.InventoryCode, order.Id, line.ShipmentLineNumber);
                    return await StopAsync(order, a, FailWith(ContractErrorCodes.InvalidRequest, 403, new JsonObject { ["inventoryCode"] = result.InventoryCode }), ct);
                default:
                    // 5xx / timeout: the line stays Unknown — never Refused — and its stored request is resent first.
                    return await StopAsync(order, a, Fail(ContractErrorCodes.DependencyUnavailable, 503), ct);
            }
        }
        return await CompleteAsync(order, a, occurredAt, fingerprint, ct);
    }

    /// <summary>D: every line Posted or NotPosted with a reason → Received in one transaction (R34-1, R34-2).</summary>
    private async Task<Response<ReturnResponse>> CompleteAsync(ReturnOrder order, ReceiptAttempt a, string occurredAt, string fingerprint, CancellationToken ct)
    {
        var lines = a.Lines.Select(l => new ReturnLine(l.ShipmentLineNumber, "", "", l.State == ReceiptLineState.Posted ? l.TransactionId : null,
            l.State == ReceiptLineState.Posted ? "Posted" : "NotPosted", l.State == ReceiptLineState.Posted ? null : l.PostingReason)).ToArray();
        // The in-product audit row carries each line's reason and last INVENTORY answer (central audit: open point F-4 of MOD-0183).
        var detail = JsonSerializer.Serialize(new
        {
            lines = a.Lines.Select(l => new { l.ShipmentLineNumber, l.K, State = l.State.ToString(), l.TransactionId, l.IssueTransactionId, l.PostingReason, l.LastHttpStatus, l.LastInventoryCode,
                l.ToWarehouseId, l.ToLocationId, l.OccurredAt })
        });
        var result = await repository.MutateAsync(context.Scope, order.Id, context.IdempotencyKey, fingerprint, context.CorrelationId, null, ReturnStatus.Received,
            occurredAt, null, null, (_, _) => throw new InvalidOperationException("Received reads the shipment only in step A."), ct, completion: new ReturnReceiptCompletion(a.LeaseId, lines, detail));
        if (result.ErrorCode == ContractErrorCodes.ReturnReceiptInProgress) return InProgress(a);
        // Review 01 F-4: a failed commit (e.g. 503 PERSISTENCE_UNAVAILABLE) also releases the lease for the user's retry.
        return result.ErrorCode is null ? Answer(result) : await StopAsync(order, a, Answer(result), ct);
    }

    /// <summary>Every stop records the line's answer and releases the lease, so the user's retry may start at once.</summary>
    private async Task<Response<ReturnResponse>> StopAsync(ReturnOrder order, ReceiptAttempt a, Response<ReturnResponse> answer, CancellationToken ct)
    {
        a.LeaseUntil = clock.GetUtcNow();
        return await repository.SaveReceiptAttemptAsync(context.Scope, order.Id, a, a.LeaseId, ct) ? answer : InProgress(a);
    }

    /// <summary>The last accepted values; an attempt stored before they existed falls back to its first line's.</summary>
    private static (string? Warehouse, string? Location, string? OccurredAt) Current(ReceiptAttempt a) => a.CurrentWarehouseId is not null
        ? (a.CurrentWarehouseId, a.CurrentLocationId, a.CurrentOccurredAt)
        : a.Lines.Count == 0 ? (null, null, null) : (a.Lines[0].ToWarehouseId, a.Lines[0].ToLocationId, a.Lines[0].OccurredAt);

    private async Task<bool> RenewAsync(ReturnOrder order, ReceiptAttempt a, CancellationToken ct)
    {
        a.LeaseUntil = clock.GetUtcNow() + options.Lease;
        return await repository.SaveReceiptAttemptAsync(context.Scope, order.Id, a, a.LeaseId, ct);
    }

    private static JsonObject Stored(ReceiptAttempt a)
    {
        var (warehouse, location, occurredAt) = Current(a);
        return new() { ["toWarehouseId"] = warehouse, ["toLocationId"] = location, ["occurredAt"] = occurredAt };
    }
    private static Response<ReturnResponse> InProgress(ReceiptAttempt a) =>
        FailWith(ContractErrorCodes.ReturnReceiptInProgress, 409, a.Lines.Count == 0 ? null : Stored(a));
    private static Response<ReturnResponse> Fail(string code, int status) => Response<ReturnResponse>.Fail(code, status);
    private static Response<ReturnResponse> FailWith(string code, int status, JsonObject? details) => Response<ReturnResponse>.FailWith(code, status, details);
    private static Response<ReturnResponse> Answer(ReturnMutationResult r) => r.ErrorCode is null
        ? Response<ReturnResponse>.Success(ReturnResponse.From(r), r.StatusCode)
        : FailWith(r.ErrorCode, r.StatusCode, r.DetailsJson is null ? null : JsonNode.Parse(r.DetailsJson)!.AsObject());
}
