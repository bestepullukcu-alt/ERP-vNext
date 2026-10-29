namespace Diten.SupplyChainService.Domain.Features.Shipments;
/// <summary>One shipment line inside a dispatch attempt (MOD-0183 §23.4), in the shipment's stored line order.</summary>
public sealed class DispatchAttemptLine
{
    public string LineNumber { get; set; } = string.Empty;
    /// <summary>Set, with the body, before the issue is posted; a sent line without an issue id is resent with the same key.</summary>
    public bool Sent { get; set; }
    /// <summary>The full GOODS_ISSUE_SALES request as first sent; every resend uses these bytes.</summary>
    public string? RequestJson { get; set; }
    public string? IssueTransactionId { get; set; }
    public string? LotId { get; set; }
    public string? ReversalTransactionId { get; set; }
    /// <summary>Our REVERSAL answered ALREADY_REVERSED: the issue was reversed in INVENTORY under another key (§23.4).</summary>
    public bool ExternallyReversed { get; set; }
    public string? LastInventoryCode { get; set; }
    public bool IsReversed() => ReversalTransactionId is not null || ExternallyReversed;
}
