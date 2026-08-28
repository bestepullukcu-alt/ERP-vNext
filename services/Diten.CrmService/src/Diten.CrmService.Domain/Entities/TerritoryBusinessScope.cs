namespace Diten.CrmService.Domain.Entities;

/// <summary>
/// MOD-0151 FU02A value object embedded on a <see cref="TerritoryModel"/>. A business scope is a crossing sales
/// dimension the plan applies to (pack §9). FU02A only supports <c>business-unit</c> scopes (e.g. alpha / beta /
/// gamma); product-portfolio / brand-group are later FUs and are rejected by validation. <see cref="ScopeCode"/> is a
/// MOD-0048 published <c>business-unit</c> value code. No assignment / preview logic is triggered from it (later FU).
/// </summary>
public sealed class TerritoryBusinessScope
{
    /// <summary>Classification of the scope. FU02A: always <c>business-unit</c>.</summary>
    public string ScopeType { get; set; } = string.Empty;

    /// <summary>MOD-0048 published value code within the scope type's value set (e.g. <c>alpha</c>).</summary>
    public string ScopeCode { get; set; } = string.Empty;
}
