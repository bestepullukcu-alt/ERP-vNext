namespace Diten.InventoryService.Domain.Traceability.Enums;

/// <summary>
/// TRACE v1 <c>LotRef.status</c> (frozen enum). Draft → Released ⇄ Blocked by a person (K-E, pack §12 S); Expired only by
/// the system when the expiry date has passed (B-3) — never set or left by a caller.
/// </summary>
public enum LotStatus
{
    Draft,
    Released,
    Blocked,
    Expired
}
