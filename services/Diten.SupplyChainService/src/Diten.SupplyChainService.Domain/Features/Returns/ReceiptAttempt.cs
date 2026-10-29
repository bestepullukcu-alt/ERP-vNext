namespace Diten.SupplyChainService.Domain.Features.Returns;
/// <summary>
/// MOD-0186 §34.2 (r8) — the receipt attempt a return stores at its first <c>Received</c> command: per line the full §34.1
/// request, its version <c>k</c>, the sent marker and the last INVENTORY answer, plus a single-writer lease. Every write of a
/// lease holder is conditional on <see cref="LeaseId"/>; a takeover is conditional on <see cref="Rev"/>.
/// </summary>
public sealed class ReceiptAttempt
{
    public Guid LeaseId { get; set; }
    public DateTimeOffset LeaseUntil { get; set; }
    /// <summary>+1 on every stored write of the attempt.</summary>
    public long Rev { get; set; }
    public List<ReceiptAttemptLine> Lines { get; set; } = [];

    public static string ReceiptKey(Guid returnId, string lineNumber, int k) => $"{returnId}:{lineNumber}:customer-return:a{k}";
    public static string ReversalKey(Guid returnId, string lineNumber, int k) => $"{returnId}:{lineNumber}:customer-return-reversal:a{k}";
    public ReceiptAttempt Copy()
    {
        var copy = (ReceiptAttempt)MemberwiseClone();
        copy.Lines = Lines.Select(l => l.Copy()).ToList();
        return copy;
    }
    /// <summary>§34.2 completion: every line Posted, or NotPosted with a reason.</summary>
    public bool IsComplete() => Lines.All(l => l.State == ReceiptLineState.Posted || (l.State == ReceiptLineState.NotPosted && l.PostingReason is not null));
}

/// <summary>One return line inside a receipt attempt, in the return's stored line order.</summary>
public sealed class ReceiptAttemptLine
{
    public string ShipmentLineNumber { get; set; } = "";
    /// <summary>The request version: 1 for the first request, +1 each time a Refused line's request is corrected.</summary>
    public int K { get; set; } = 1;
    /// <summary>The full §34.1 CUSTOMER_RETURN body of version <see cref="K"/>; every send of that key uses these bytes.</summary>
    public string? RequestJson { get; set; }
    public string? ToWarehouseId { get; set; }
    public string? ToLocationId { get; set; }
    /// <summary>The normalized <c>occurredAt</c> the request's dates come from.</summary>
    public string? OccurredAt { get; set; }
    /// <summary>The sent marker: the key of <see cref="RequestJson"/>, written before INVENTORY is called.</summary>
    public string? SentKey { get; set; }
    public ReceiptLineState State { get; set; }
    public int? LastHttpStatus { get; set; }
    public string? LastInventoryCode { get; set; }
    public string? TransactionId { get; set; }
    /// <summary><c>PRE_WP_P1</c> or the accepted <c>unpostedLines</c> reason; null unless NotPosted.</summary>
    public string? PostingReason { get; set; }
    public ReceiptAttemptLine Copy() => (ReceiptAttemptLine)MemberwiseClone();
}

/// <summary>
/// §34.2 late writer: a CUSTOMER_RETURN a writer posted after it lost the lease, with its REVERSAL (or none when that
/// failed). Appended to <see cref="ReturnOrder.ReceiptAuditHistory"/> without the lease; never changed afterwards.
/// </summary>
public sealed record ReceiptAuditEntry(string ShipmentLineNumber, int K, string ReceiptTransactionId, string? ReversalTransactionId, string Outcome,
    DateTimeOffset At, Guid CorrelationId);

/// <summary>Step D of §34.2: the lease that must still be held and each line's result, committed with <c>Received</c>.</summary>
public sealed record ReturnReceiptCompletion(Guid LeaseId, IReadOnlyList<ReturnLine> Lines, string AuditDetailJson);
