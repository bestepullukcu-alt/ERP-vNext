namespace Diten.InventoryService.Domain.Rules;

/// <summary>One PRODUCT-MASTER <c>UomConversion</c>: <c>1 FromUom = Numerator / Denominator ToUom</c>.</summary>
public sealed record UomConversionRule(
    string FromUom,
    string ToUom,
    decimal Numerator,
    decimal Denominator,
    DateTimeOffset? EffectiveFrom = null,
    DateTimeOffset? EffectiveTo = null)
{
    public bool IsEffectiveAt(DateTimeOffset at)
        => (EffectiveFrom is null || EffectiveFrom <= at) && (EffectiveTo is null || at <= EffectiveTo);
}
