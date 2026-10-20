namespace Diten.CrmService.Domain.Entities.Commercial;

/// <summary>
/// WP-VW-W6 W6-BE-b — MOD-0156 §4.5 (K-W10 g, D14): the interim FX rate table until MOD-0277. One rate per (legal entity,
/// from, to, date); sales operations enter it directly (no four eyes); a correction rewrites the row and the old value goes
/// to the change log. Orders keep their own snapshot.
/// </summary>
public sealed class FxRate : EntityBase
{
    public Guid LegalEntityId { get; set; }
    public string FromCurrency { get; set; } = string.Empty;
    public string ToCurrency { get; set; } = string.Empty;
    public DateOnly RateDate { get; set; }

    /// <summary>&gt; 0, at most six decimals.</summary>
    public decimal Rate { get; set; }

    /// <summary>Free text ≤ 60 (e.g. "Merkez Bankası", "manual") — shown on the order document.</summary>
    public string Source { get; set; } = string.Empty;

    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }
}
