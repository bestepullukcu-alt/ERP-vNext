using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;
namespace Diten.SupplyChainService.Domain.Features.SupplierPerformance;

public sealed record MetricInput(string MetricCode, string MeasuredValue, string Weight);
public sealed record ScoreOutcome(IReadOnlyList<EvaluationMetricResult> Metrics, string OverallScore, string RiskLevel);

/// <summary>
/// The pack-held policy <c>supplier-score-policy/1</c> (MOD-0147 §0.0 "Rules that bind v1", §0.4): four fixed PERCENT metrics,
/// weight 25 each, at least 3 of the 4, values 0..100 with at most four fractional digits. The aggregate is exact: every value
/// is held as an integer number of 1e-4 units, so Σ(value × 25) never rounds; overall = Σ(value × 25) / Σ(25) rounded ONCE,
/// half-even, to two decimals; the band compares Σ(value × 25) with threshold × Σ(25) and never divides.
/// </summary>
public static class ScorePolicy
{
    public const string Revision = "supplier-score-policy/1";
    public const int PolicyWeight = 25;
    public const int MinimumMetrics = 3;
    public const int MaxFractionDigits = 4;
    public static readonly IReadOnlyList<string> Codes = ["OTD", "QTY_ACC", "QUAL_ACC", "INV_MATCH"];
    private static readonly BigInteger Unit = 10_000; // 1e-4: the finest value v1 accepts
    private static readonly Regex DecimalText = new(@"\A(-?)([0-9]+)(?:\.([0-9]+))?\z", RegexOptions.CultureInvariant);

    /// <summary>Parses a contract Decimal into 1e-4 units; false for more than four fractional digits.</summary>
    public static bool TryScaled(string text, out BigInteger scaled)
    {
        scaled = BigInteger.Zero;
        var m = DecimalText.Match(text);
        if (!m.Success || m.Groups[3].Length > MaxFractionDigits) return false;
        scaled = BigInteger.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture) * Unit
            + BigInteger.Parse(m.Groups[3].Value.PadRight(MaxFractionDigits, '0'), CultureInfo.InvariantCulture);
        if (m.Groups[1].Length == 1) scaled = -scaled;
        return true;
    }

    /// <summary>Applies the v1 rules; a null outcome is a 422 BUSINESS_RULE_VIOLATION and nothing is written.</summary>
    public static ScoreOutcome? Score(IReadOnlyList<MetricInput> metrics)
    {
        if (metrics.Count < MinimumMetrics || metrics.Count > Codes.Count) return null;
        if (metrics.Select(x => x.MetricCode).Distinct(StringComparer.Ordinal).Count() != metrics.Count) return null;
        var weighted = BigInteger.Zero;
        foreach (var metric in metrics)
        {
            if (!Codes.Contains(metric.MetricCode, StringComparer.Ordinal)) return null;
            if (!TryScaled(metric.Weight, out var weight) || weight != PolicyWeight * Unit) return null;
            if (!TryScaled(metric.MeasuredValue, out var value) || value < 0 || value > 100 * Unit) return null;
            weighted += value * PolicyWeight;
        }
        var totalWeight = new BigInteger(PolicyWeight * metrics.Count);
        return new(metrics.Select(x => new EvaluationMetricResult(x.MetricCode, x.MeasuredValue, PolicyWeight.ToString(CultureInfo.InvariantCulture))).ToList(),
            Overall(weighted, totalWeight), Band(weighted, totalWeight));
    }

    /// <summary>round_half_even(weighted / totalWeight, 2) with weighted in 1e-4 units: one integer division, one rounding.</summary>
    public static string Overall(BigInteger weighted, BigInteger totalWeight)
    {
        var denominator = totalWeight * (Unit / 100);
        var hundredths = BigInteger.DivRem(weighted, denominator, out var remainder);
        var twice = remainder * 2;
        if (twice > denominator || (twice == denominator && !hundredths.IsEven)) hundredths += 1;
        return (hundredths / 100).ToString(CultureInfo.InvariantCulture) + "." + (hundredths % 100).ToString("00", CultureInfo.InvariantCulture);
    }

    /// <summary>The band from the unrounded aggregate, decided without dividing: weighted ≥ threshold × totalWeight.</summary>
    public static string Band(BigInteger weighted, BigInteger totalWeight)
    {
        bool AtLeast(int threshold) => weighted >= threshold * Unit * totalWeight;
        return AtLeast(90) ? "LOW" : AtLeast(75) ? "MEDIUM" : AtLeast(50) ? "HIGH" : "CRITICAL";
    }

    /// <summary>v1 period: periodStart is the first and periodEnd the last day of the same calendar month (UTC dates).</summary>
    public static bool IsCalendarMonth(DateOnly start, DateOnly end) =>
        start.Day == 1 && end == new DateOnly(start.Year, start.Month, DateTime.DaysInMonth(start.Year, start.Month));
}
