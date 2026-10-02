namespace Diten.Web.Models.Inventory;

// MOD-0173-FU01 S-2 Movement history — contract shapes of GET /api/inventory/transactions and POST /movements as the
// MVC proxy uses them, and the row the screen renders. UI-1 has no form: the only state-changing call is Reversal.

/// <summary>One item of <c>GET /api/inventory/transactions</c> (contract <c>TransactionItem</c>).</summary>
public sealed class TransactionItemDto
{
    public string? TransactionId { get; set; }
    public string? TransactionNumber { get; set; }
    public string? MovementType { get; set; }
    public string? MovementReason { get; set; }
    public string? ItemId { get; set; }
    public string? SkuId { get; set; }
    public string? SkuLevel { get; set; }
    public string? Quantity { get; set; }
    public string? UomId { get; set; }
    public string? BaseQuantity { get; set; }
    public string? BaseUomId { get; set; }
    public string? ToWarehouseId { get; set; }
    public string? ToLocationId { get; set; }
    public string? LotId { get; set; }
    public string? FromStockStatus { get; set; }
    public string? ToStockStatus { get; set; }
    public string? SourceModule { get; set; }
    public string? SourceDocumentId { get; set; }
    public string? SourceLineId { get; set; }
    public string? TransactionDate { get; set; }
    public string? PostingDate { get; set; }
}

/// <summary><c>GET /api/inventory/transactions</c> page: <c>{ items, page, pageSize, total, contractVersion }</c>.</summary>
public sealed class TransactionPageDto
{
    public List<TransactionItemDto>? Items { get; set; }
    public int? Page { get; set; }
    public int? PageSize { get; set; }
    public int? Total { get; set; }
    public string? ContractVersion { get; set; }
}

/// <summary>A transaction as the S-2 table renders it.</summary>
public sealed class StockMovementRowViewModel
{
    public string Id { get; init; } = string.Empty;
    public string? TransactionNumber { get; init; }
    public string? MovementType { get; init; }
    /// <summary><c>+</c> inbound, <c>-</c> outbound, <c>transfer</c>, <c>reversal</c> — from the direction class (parent §12).</summary>
    public string Direction { get; init; } = string.Empty;
    /// <summary>Posted by another module (<c>sourceModule ≠ MOD-0173</c>, UI-08): reversed in that module, not here.</summary>
    public bool IsAutomatic { get; init; }
    public string? MovementReason { get; init; }
    public string? ItemId { get; init; }
    public string? ProductLabel { get; init; }
    public string? SkuId { get; init; }
    public string? SkuLevel { get; init; }
    public string? SkuLabel { get; init; }
    public string? Quantity { get; init; }
    public string? UomId { get; init; }
    public string? BaseQuantity { get; init; }
    public string? BaseUomId { get; init; }
    public string? ToWarehouseId { get; init; }
    public string? ToWarehouseLabel { get; init; }
    public string? ToLocationId { get; init; }
    public string? ToLocationLabel { get; init; }
    public string? LotId { get; init; }
    public string? FromStockStatus { get; init; }
    public string? ToStockStatus { get; init; }
    public string? SourceModule { get; init; }
    public string? SourceDocumentId { get; init; }
    public string? SourceLineId { get; init; }
    public string? TransactionDate { get; init; }
    public string? PostingDate { get; init; }
    /// <summary>
    /// A REVERSAL of this transaction is on the same page. BG-05: the contract cannot say "already reversed", so this
    /// is only what the page in hand proves; the service's 422 ALREADY_REVERSED stays the authority.
    /// </summary>
    public bool ReversedOnPage { get; init; }
    /// <summary>Manual (MOD-0173), not a reversal, not reversed on this page (UI-08). Permission is decided by the screen.</summary>
    public bool CanReverse { get; init; }
    /// <summary>
    /// UI-2 "Correct" (KD-1): every reversible row whose type is one of the SEVEN manual types (parent §21) — the form
    /// can only re-post a manual type. An automatic movement, a reversal or a reversed row is never offered it.
    /// </summary>
    public bool CanCorrect { get; init; }
}

/// <summary>Body of <c>POST /StockMovements/reverse/{transactionId}</c> — the original row's identity (UI-09, BG-06).</summary>
public sealed class ReversalRequestViewModel
{
    /// <summary>Minted by the screen when the confirmation opens; a double submit of the same dialog replays.</summary>
    public string? IdempotencyKey { get; set; }
    public string? ItemId { get; set; }
    public string? SkuId { get; set; }
    public string? SkuLevel { get; set; }
    public string? Quantity { get; set; }
    public string? UomId { get; set; }
}

/// <summary>Contract <c>MovementResponse</c> (the fields the reversal result shows).</summary>
public sealed class MovementResponseDto
{
    public string? TransactionId { get; set; }
    public string? TransactionNumber { get; set; }
    public bool IdempotentReplay { get; set; }
}

/// <summary>Contract <c>Error</c>: <c>{ error: { code, message, details, correlationId }, contractVersion }</c>.</summary>
public sealed class InventoryErrorEnvelope
{
    public InventoryErrorBody? Error { get; set; }
}

public sealed class InventoryErrorBody
{
    public string? Code { get; set; }
    public string? Message { get; set; }
    public Dictionary<string, object?>? Details { get; set; }
    public string? CorrelationId { get; set; }
}
