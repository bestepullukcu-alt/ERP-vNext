namespace Diten.MdmService.Domain.ValueObjects;

// MOD-0290-FU04 §4.4: 1 FromUom = Numerator / Denominator ToUom; numerator and denominator are decimal TEXT, stored as
// entered (MDM never rounds). The chain rules (≤ 4 steps, no cycle, same-dimension factors) are S6.
public sealed class ItemUomConversion
{
    public string FromUom { get; set; } = string.Empty;
    public string ToUom { get; set; } = string.Empty;
    public string Numerator { get; set; } = string.Empty;
    public string Denominator { get; set; } = string.Empty;
}
