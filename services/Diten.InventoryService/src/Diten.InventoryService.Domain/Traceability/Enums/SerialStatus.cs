namespace Diten.InventoryService.Domain.Traceability.Enums;

/// <summary>
/// The STORED serial status. TRACE v1 <c>SerialRef.status</c> also has <c>Blocked</c>, which is never stored: it is
/// derived on read from the serial's lot (pack §4 <c>SerialNumber</c>).
/// </summary>
public enum SerialStatus
{
    Active,
    Consumed
}
