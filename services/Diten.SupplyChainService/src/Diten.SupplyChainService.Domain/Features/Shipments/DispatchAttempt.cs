namespace Diten.SupplyChainService.Domain.Features.Shipments;
/// <summary>
/// MOD-0183 §23.4 — the one dispatch attempt a shipment holds at a time. <see cref="N"/> grows only when an attempt closes
/// as <see cref="DispatchState.Compensated"/>; the dates come from the command that opened it and never change; every write
/// of a lease holder is conditional on <see cref="N"/> and <see cref="LeaseId"/>.
/// </summary>
public sealed class DispatchAttempt
{
    public int N { get; set; }
    public DispatchState State { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset TransactionDate { get; set; }
    public DateTimeOffset PostingDate { get; set; }
    public Guid LeaseId { get; set; }
    public DateTimeOffset LeaseUntil { get; set; }
    public Guid CorrelationId { get; set; }
    /// <summary>The moment compensation started (server UTC), set at the first reversal and resent unchanged.</summary>
    public DateTimeOffset? ReversalDate { get; set; }
    /// <summary>+1 on every stored write of the attempt; a lease takeover is conditional on it (review F-2).</summary>
    public long Rev { get; set; }
    public List<DispatchAttemptLine> Lines { get; set; } = [];

    public string IssueKey(Guid shipmentId, string lineNumber) => $"{shipmentId}:{lineNumber}:dispatch:a{N}";
    public string ReversalKey(Guid shipmentId, string lineNumber) => $"{shipmentId}:{lineNumber}:reversal:a{N}";
    public DispatchAttempt Copy()
    {
        var copy = (DispatchAttempt)MemberwiseClone();
        copy.Lines = Lines.Select(l => new DispatchAttemptLine
        {
            LineNumber = l.LineNumber, Sent = l.Sent, RequestJson = l.RequestJson, IssueTransactionId = l.IssueTransactionId, LotId = l.LotId,
            ReversalTransactionId = l.ReversalTransactionId, ExternallyReversed = l.ExternallyReversed, LastInventoryCode = l.LastInventoryCode
        }).ToList();
        return copy;
    }
}

/// <summary>An INVENTORY movement a writer made after it lost the lease (§23.4 late writer), kept without the lease condition.</summary>
public sealed record DispatchLateWrite(int N, string LineNumber, string IssueTransactionId, string? ReversalTransactionId, string Outcome, DateTimeOffset At);
