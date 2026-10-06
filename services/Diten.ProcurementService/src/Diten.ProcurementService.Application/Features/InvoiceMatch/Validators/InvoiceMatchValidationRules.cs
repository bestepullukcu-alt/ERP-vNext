using System.Globalization;
using System.Text.RegularExpressions;

namespace Diten.ProcurementService.Application.Features.InvoiceMatch.Validators;

/// <summary>
/// Ortak Invoice-Match validation + Decimal yardımcıları (MOD-0143 §12). Parasal alanlar Decimal string
/// (^-?\d+(\.\d+)?$); float YASAK. resolve.decision contract enum'ına (approve/reject/tolerance-override) parse edilir.
/// NOT: burada HİÇBİR tolerans sayısı yoktur — tolerans policy seam'inden (IMatchTolerancePolicy) gelir (ASSUMPTION-P2P-01).
/// </summary>
internal static partial class InvoiceMatchValidationRules
{
    public const string DecisionApprove = "approve";
    public const string DecisionReject = "reject";
    public const string DecisionToleranceOverride = "tolerance-override";

    [GeneratedRegex(@"^-?\d+(\.\d+)?$")]
    private static partial Regex DecimalRegex();

    /// <summary>Contract Decimal deseni (^-?\d+(\.\d+)?$).</summary>
    public static bool IsValidDecimal(string? value)
        => !string.IsNullOrWhiteSpace(value) && DecimalRegex().IsMatch(value.Trim());

    public static bool TryParseDecimal(string? value, out decimal parsed)
        => decimal.TryParse(
            (value ?? string.Empty).Trim(),
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture,
            out parsed);

    /// <summary>Pozitif Decimal (quantity > 0).</summary>
    public static bool IsPositiveDecimal(string? value)
        => IsValidDecimal(value) && TryParseDecimal(value, out var d) && d > 0m;

    /// <summary>Negatif olmayan Decimal (unitPrice ≥ 0).</summary>
    public static bool IsNonNegativeDecimal(string? value)
        => IsValidDecimal(value) && TryParseDecimal(value, out var d) && d >= 0m;

    /// <summary>SERVER-COMPUTED satır tutarı: quantity × unitPrice (Decimal; float YASAK).</summary>
    public static string ComputeLineAmount(string quantity, string unitPrice)
    {
        TryParseDecimal(quantity, out var q);
        TryParseDecimal(unitPrice, out var p);
        return (q * p).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>SERVER-COMPUTED toplam: Σ lineAmount (Decimal).</summary>
    public static string ComputeTotal(IEnumerable<string> lineAmounts)
    {
        var total = 0m;
        foreach (var amount in lineAmounts)
        {
            TryParseDecimal(amount, out var a);
            total += a;
        }
        return total.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>resolve.decision geçerli enum mu (approve/reject/tolerance-override).</summary>
    public static bool IsValidDecision(string? decision)
    {
        var d = decision?.Trim().ToLowerInvariant();
        return d is DecisionApprove or DecisionReject or DecisionToleranceOverride;
    }
}
