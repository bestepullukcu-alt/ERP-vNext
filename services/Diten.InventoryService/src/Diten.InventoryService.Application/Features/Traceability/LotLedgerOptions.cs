namespace Diten.InventoryService.Application.Features.Traceability;

/// <summary>
/// Configuration section <c>Trace</c> (pack ASSUMPTION-0174-10, WP-0174-P2).
/// </summary>
public sealed class LotLedgerOptions
{
    public const string SectionName = "Trace";

    /// <summary>
    /// <c>Trace:EnforceRegisteredLots</c> — default <b>false</b>; switched on when slice P3 is CT-accepted, not before.
    /// <list type="bullet">
    /// <item>false: a typed (unregistered) lot text is accepted on inbound and outbound exactly as before P2; a registered
    /// <c>lotId</c> gets every pack §12 rule; an outbound serial the registry does not know passes when the lot is not a
    /// registered lot (stock received before P2 has no serial rows).</item>
    /// <item>true: an inbound movement of a lot-controlled product is accepted only with a registered lot
    /// (<c>UNKNOWN_LOT</c>); a lot on a product that is not lot-controlled is refused even as typed text
    /// (<c>LOT_NOT_ALLOWED</c>); every outbound serial must be known. Outbound typed lots stay accepted until slice P4
    /// closes typing.</item>
    /// </list>
    /// </summary>
    public bool EnforceRegisteredLots { get; set; }
}
