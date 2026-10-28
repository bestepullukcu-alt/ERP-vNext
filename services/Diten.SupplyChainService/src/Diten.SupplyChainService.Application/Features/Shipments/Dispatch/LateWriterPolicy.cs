using Microsoft.Extensions.Logging;
using Diten.SupplyChainService.Application.Common;
using Diten.SupplyChainService.Domain.Features.Shipments;
namespace Diten.SupplyChainService.Application.Features.Shipments.Dispatch;

/// <summary>
/// MOD-0183 §23.4 "Late writer": a writer whose lease expired and who got a 201 it cannot record. The only INVENTORY call
/// made without the lease. Its own attempt still open (stored n equal, InProgress or Completed) → log only: the holder
/// resends the same key and gets the same transactionId. Its attempt closed (stored n greater, or equal and Compensated /
/// CompensationFailed) → nobody resends its key, so it reverses its issue with <c>…:reversal:a{own n}</c>. An issue the
/// shipment already records (the holder's resend replayed it, CT-APR-02 r6 F-1) is never reversed here.
/// </summary>
public sealed class LateWriterPolicy(IShipmentRepository repository, IInventoryMovementClient inventory, RequestContext context,
    TimeProvider clock, ILogger<LateWriterPolicy> logger)
{
    public async Task HandleAsync(Guid shipmentId, int ownN, string lineNumber, string issueRequestJson, string issueTransactionId, CancellationToken ct)
    {
        var s = await repository.GetAsync(context.Scope, shipmentId, ct);
        if (s is null) return;
        var current = s.DispatchAttempt;
        // A compensating attempt is closed to posting: its holder never resends a refused line, so a late issue there is the
        // late writer's to reverse (review 3 F-3), with the same key and stored date the holder would use.
        var ownAttemptOpen = current is not null && current.N == ownN
            && (current.State == DispatchState.Completed || (current.State == DispatchState.InProgress && !current.IsCompensating()));
        var recorded = (current?.Lines ?? []).Concat(s.DispatchHistory.SelectMany(h => h.Lines)).Any(l => l.IssueTransactionId == issueTransactionId)
            || s.DispatchLateWrites.Any(w => w.IssueTransactionId == issueTransactionId);
        if (ownAttemptOpen || recorded)
        {
            logger.LogWarning("MOD-0183 late writer: attempt {Attempt} line {Line} issue {Issue} answered after the lease; left to the lease holder (recorded={Recorded}).",
                ownN, lineNumber, issueTransactionId, recorded);
            return;
        }
        var at = clock.GetUtcNow();
        // Same key as the holder's REVERSAL of this line, so the same bytes (review F-5): the attempt's stored reversal date.
        // An attempt closed by compensation always has one; the clock is only a fallback.
        var own = current?.N == ownN ? current : s.DispatchHistory.FirstOrDefault(h => h.N == ownN);
        var key = $"{shipmentId}:{lineNumber}:reversal:a{ownN}";
        var body = InventoryMovementMapper.Reversal(shipmentId, issueRequestJson, issueTransactionId, key, own?.ReversalDate ?? at);
        var result = await inventory.PostAsync(new InventoryPostRequest(key, body, context.Scope.TenantId, s.LegalEntityId, context.CorrelationId, context.Authorization), ct);
        var outcome = result.Outcome == InventoryOutcome.Posted ? "Reversed"
            : result.Outcome == InventoryOutcome.Refused && result.InventoryCode == "ALREADY_REVERSED" ? "AlreadyReversed"
            : "ReversalFailed:" + (result.InventoryCode ?? result.HttpStatus.ToString(System.Globalization.CultureInfo.InvariantCulture));
        logger.LogError("MOD-0183 integrity fault: late writer of closed attempt {Attempt} line {Line} issue {Issue} → {Outcome}.", ownN, lineNumber, issueTransactionId, outcome);
        await repository.RecordLateWriteAsync(context.Scope, shipmentId, new DispatchLateWrite(ownN, lineNumber, issueTransactionId,
                result.Outcome == InventoryOutcome.Posted ? result.TransactionId : null, outcome, at),
            new DispatchAuditEntry(ownN, "LateWriter" + outcome, context.CorrelationId, at,
                [issueTransactionId], result.Outcome == InventoryOutcome.Posted ? [result.TransactionId!] : [], ["LATE_WRITER line " + lineNumber]), ct);
    }
}
