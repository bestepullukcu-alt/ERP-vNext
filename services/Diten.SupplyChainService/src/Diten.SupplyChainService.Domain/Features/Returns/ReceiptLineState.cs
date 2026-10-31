namespace Diten.SupplyChainService.Domain.Features.Returns;
/// <summary>
/// MOD-0186 §34.2 (r8): the state of one return line inside a receipt attempt. <see cref="Pending"/> is a stored request not
/// yet sent under its key; the other five are the pack's.
/// </summary>
public enum ReceiptLineState
{
    Pending,
    /// <summary>INVENTORY answered 201 / replay: the line has its CUSTOMER_RETURN transactionId.</summary>
    Posted,
    /// <summary>Received without stock posting: <c>PRE_WP_P1</c> or an accepted <c>unpostedLines</c> reason.</summary>
    NotPosted,
    /// <summary>The last answer is an INVENTORY 422: correctable, and its next request goes under <c>a{k+1}</c>.</summary>
    Refused,
    /// <summary>Marked sent, no recorded answer: resent with the same key and body before anything else.</summary>
    Unknown,
    /// <summary>INVENTORY 400 (<c>INVALID_REQUEST</c> or <c>IDEMPOTENCY_KEY_REUSED</c>): an integrity fault.</summary>
    Blocked
}
