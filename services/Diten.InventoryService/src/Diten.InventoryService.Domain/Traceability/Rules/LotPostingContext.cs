namespace Diten.InventoryService.Domain.Traceability.Rules;

/// <summary>
/// WP-0174-P2 — what the pre-check decided about the serials of one ledger posting, handed to the posting transaction
/// (the transaction document itself does not say whether the product is serial-controlled).
/// </summary>
/// <param name="SerialLifecycle">The posting registers / consumes / moves its serials in <c>trace_serials</c>.</param>
/// <param name="AcceptUnregisteredSerials">
/// An outbound / transfer serial unknown to the registry passes without a registry write — stock received before P2 has
/// no serial rows (ASSUMPTION-0174-10 applied to serials: only while <c>Trace:EnforceRegisteredLots</c> is off and the
/// lot is not a registered lot; a REVERSAL always, since its original may predate P2).
/// </param>
/// <param name="RegisterNewSerials">
/// An inbound serial without a row is created. False only for a REVERSAL: it restores the serials of its original that
/// the registry knows, and never creates a serial the original did not register.
/// </param>
public sealed record LotPostingContext(bool SerialLifecycle, bool AcceptUnregisteredSerials, bool RegisterNewSerials)
{
    /// <summary>A REVERSAL: re-run the serial lifecycle of its original on the serials the registry knows.</summary>
    public static LotPostingContext ForReversal(bool hasSerials) => new(hasSerials, AcceptUnregisteredSerials: true, RegisterNewSerials: false);
}
