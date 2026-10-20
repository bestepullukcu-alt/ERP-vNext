using Diten.CrmService.Domain.Entities.Commercial;

namespace Diten.CrmService.Application.Features.Commercial.Pricing.Evaluation;

/// <summary>
/// WP-VW-W6 W6-BE-b — MOD-0156 §4.6.2 (DESIGN-GAP §2, D7): the arithmetic, in ONE place (AC-EVAL-3; MOD-0168 and the client
/// keep no copy).
/// <list type="bullet">
/// <item>MF ratio a + b → b / (a + b); 10+2 → 16.7 %.</item>
/// <item>Free boxes floor(quantity / a) × b.</item>
/// <item>Combined discount 1 − (1 − discount) × (1 − MF ratio).</item>
/// <item>Every percentage rounded to 0.1; money to the currency's minor unit, away from zero, per line.</item>
/// </list>
/// </summary>
public static class PricingMath
{
    /// <summary>The exact MF ratio (0–1); null without a valid term.</summary>
    public static decimal? MfRatio(int buy, int free) => buy >= 1 && free >= 0 ? (decimal)free / (buy + free) : null;

    public static decimal? MfRatio(PricingMfTerm? term) => term is null ? null : MfRatio(term.Buy, term.Free);

    /// <summary>A ratio (0–1) as a percentage rounded to 0.1.</summary>
    public static decimal Pct(decimal ratio) => RoundPct(ratio * 100m);

    public static decimal RoundPct(decimal pct) => Math.Round(pct, 1, MidpointRounding.AwayFromZero);

    /// <summary>floor(quantity / buy) × free.</summary>
    public static int FreeBoxes(int quantity, int buy, int free) => buy >= 1 && quantity > 0 ? quantity / buy * free : 0;

    /// <summary>1 − (1 − i)(1 − m), i and m as ratios (0–1); returned as a ratio.</summary>
    public static decimal Combined(decimal discountRatio, decimal mfRatio) => 1m - (1m - discountRatio) * (1m - mfRatio);

    public static decimal RoundMoney(decimal amount, string? currency)
        => Math.Round(amount, MinorUnits(currency), MidpointRounding.AwayFromZero);

    /// <summary>ISO 4217 minor units of the currencies the program meets; 2 for any other.</summary>
    public static int MinorUnits(string? currency) => (currency ?? string.Empty).ToUpperInvariant() switch
    {
        "JPY" or "KRW" or "VND" or "CLP" or "ISK" or "UGX" or "XAF" or "XOF" => 0,
        "BHD" or "KWD" or "OMR" or "JOD" or "IQD" or "LYD" or "TND" => 3,
        _ => 2
    };

    public static string MfText(int buy, int free) => buy + "+" + free;
}
