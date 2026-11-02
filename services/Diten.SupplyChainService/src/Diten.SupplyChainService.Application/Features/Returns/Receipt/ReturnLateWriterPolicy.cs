using Microsoft.Extensions.Logging;
using Diten.SupplyChainService.Application.Features.Shipments.Dispatch;
using Diten.SupplyChainService.Domain.Features.Returns;
namespace Diten.SupplyChainService.Application.Features.Returns.Receipt;

/// <summary>
/// MOD-0186 §34.2 (r8) "Late writer": a writer whose lease was taken over and who got a CUSTOMER_RETURN 201 it cannot record.
/// It re-reads the return and compares its own version <c>k</c>, the line's stored <c>k</c> and the line's state. Stored
/// <c>k</c> equal and the line Unknown, or Posted with this very transactionId → log only: the lease holder resends that key
/// and body and gets the same id by replay (or already has it). Stored <c>k</c> greater, or equal and the line Refused,
/// NotPosted or Blocked → nobody resends <c>a{k}</c>, so the movement is a second, unrecorded receipt: it is reversed under
/// <c>…:customer-return-reversal:a{own k}</c>, dated the moment of the 201, and the pair is appended to the return's audit
/// history (append-only, no lease). A failed REVERSAL still appends the receipt — the stated residual risk.
/// </summary>
public sealed class ReturnLateWriterPolicy(IReturnRepository repository, IInventoryMovementClient inventory, ReturnRequestContext context,
    ILogger<ReturnLateWriterPolicy> logger)
{
    public async Task HandleAsync(ReturnOrder order, string lineNumber, int ownK, string receiptRequestJson, string receiptTransactionId,
        DateTimeOffset answeredAt, CancellationToken ct)
    {
        var current = await repository.GetAsync(context.Scope, order.Id, ct);
        var stored = current?.ReceiptAttempt?.Lines.FirstOrDefault(l => l.ShipmentLineNumber == lineNumber);
        if (stored is null)
        {
            logger.LogError("MOD-0186 late writer: return {Return} line {Line} receipt {Receipt} has no stored attempt line; left in INVENTORY.",
                order.Id, lineNumber, receiptTransactionId);
            return;
        }
        var leftToHolder = stored.K == ownK
            && (stored.State == ReceiptLineState.Unknown || (stored.State == ReceiptLineState.Posted && stored.TransactionId == receiptTransactionId));
        if (leftToHolder)
        {
            logger.LogWarning("MOD-0186 late writer: return {Return} line {Line} a{K} receipt {Receipt} answered after the lease; left to the lease holder.",
                order.Id, lineNumber, ownK, receiptTransactionId);
            return;
        }
        var key = ReceiptAttempt.ReversalKey(order.Id, lineNumber, ownK);
        var body = CustomerReturnMovementMapper.Reversal(order.ShipmentId, receiptRequestJson, receiptTransactionId, key, answeredAt);
        var result = await inventory.PostAsync(new InventoryPostRequest(key, body, context.Scope.TenantId, order.LegalEntityId, context.CorrelationId, context.Authorization), ct);
        // MVP-1 CT R-2 (open): ALREADY_REVERSED is not named in §34.2; it counts as reversed, like MOD-0183, and is a fault.
        var outcome = result.Outcome == InventoryOutcome.Posted ? "Reversed"
            : result.Outcome == InventoryOutcome.Refused && result.InventoryCode == "ALREADY_REVERSED" ? "AlreadyReversed"
            : "ReversalFailed:" + (result.InventoryCode ?? result.HttpStatus.ToString(System.Globalization.CultureInfo.InvariantCulture));
        logger.LogError("MOD-0186 integrity fault: late writer of return {Return} line {Line} a{K} (stored a{StoredK} {State}) receipt {Receipt} → {Outcome}.",
            order.Id, lineNumber, ownK, stored.K, stored.State, receiptTransactionId, outcome);
        await repository.AppendReceiptAuditAsync(context.Scope, order.Id, new ReceiptAuditEntry(lineNumber, ownK, receiptTransactionId,
            result.Outcome == InventoryOutcome.Posted ? result.TransactionId : null, outcome, answeredAt, context.CorrelationId), ct);
    }
}
