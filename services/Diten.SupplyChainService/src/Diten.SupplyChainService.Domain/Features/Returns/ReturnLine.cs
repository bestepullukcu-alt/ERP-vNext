namespace Diten.SupplyChainService.Domain.Features.Returns;
/// <param name="InventoryTransactionReferenceId">MOD-0186 §34.2: the line's CUSTOMER_RETURN transactionId, set by the service at Received.</param>
/// <param name="InventoryPosting"><c>Posted</c> / <c>NotPosted</c>; null before Received.</param>
/// <param name="InventoryPostingReason"><c>PRE_WP_P1</c> or the <c>unpostedLines</c> reason; null when Posted.</param>
public sealed record ReturnLine(string ShipmentLineNumber, string Quantity, string UomId, string? InventoryTransactionReferenceId = null,
    string? InventoryPosting = null, string? InventoryPostingReason = null);
