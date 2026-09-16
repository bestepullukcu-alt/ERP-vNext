using System.Text.RegularExpressions;

namespace Diten.ProcurementService.Application.Features.Requisition.Validators;

/// <summary>
/// Ortak validation yardımcıları (MOD-0141 §12). Miktar alanları Decimal string (^-?\d+(\.\d+)?$); float YASAK —
/// string kalır, serileştirmede float'a dönmez.
/// </summary>
internal static partial class RequisitionValidationRules
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
            System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowDecimalPoint,
            System.Globalization.CultureInfo.InvariantCulture,
            out parsed);

    /// <summary>Pozitif Decimal (quantity > 0).</summary>
    public static bool IsPositiveDecimal(string? value)
        => IsValidDecimal(value) && TryParseDecimal(value, out var d) && d > 0m;
}
