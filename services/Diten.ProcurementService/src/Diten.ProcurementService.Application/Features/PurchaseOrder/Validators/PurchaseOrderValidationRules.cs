using System.Globalization;
using System.Text.RegularExpressions;

namespace Diten.ProcurementService.Application.Features.PurchaseOrder.Validators;

/// <summary>
/// Ortak validation + para hesap yardımcıları (MOD-0141 §8/§12). Parasal alanlar Decimal string (^-?\d+(\.\d+)?$);
/// float YASAK — string kalır, serileştirmede float'a dönmez. lineAmount/totalAmount SERVER-COMPUTED (Decimal).
/// </summary>
internal static partial class PurchaseOrderValidationRules
{
    [GeneratedRegex(@"^-?\d+(\.\d+)?$")]
    private static partial Regex DecimalRegex();

    /// <summary>Contract Decimal deseni (^-?\d+(\.\d+)?$).</summary>
    public static bool IsValidDecimal(string? value)
        => !string.IsNullOrWhiteSpace(value) && DecimalRegex().IsMatch(value.Trim());

    /// <summary>Decimal string'i işaret kontrolü için parse eder (regex geçtikten sonra kullanılır).</summary>
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

    /// <summary>SERVER-COMPUTED satır tutarı: quantity × unitPrice (Decimal; float YASAK). Invariant Decimal string döner.</summary>
    public static string ComputeLineAmount(string quantity, string unitPrice)
    {
        TryParseDecimal(quantity, out var q);
        TryParseDecimal(unitPrice, out var p);
        return (q * p).ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>SERVER-COMPUTED toplam: Σ lineAmount (Decimal). Invariant Decimal string döner.</summary>
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
}
