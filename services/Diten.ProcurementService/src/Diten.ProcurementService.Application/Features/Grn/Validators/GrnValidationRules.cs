using System.Globalization;
using System.Text.RegularExpressions;
using Diten.ProcurementService.Domain.Entities;

namespace Diten.ProcurementService.Application.Features.Grn.Validators;

/// <summary>
/// Ortak GRN validation yardımcıları (MOD-0142 §12). quantity Decimal string (^-?\d+(\.\d+)?$); float YASAK.
/// skuLevel/toStockStatus contract enum'larına (case-insensitive) parse edilir; geçersiz → 422.
/// </summary>
internal static partial class GrnValidationRules
{
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

    public static bool TryParseSkuLevel(string? value, out GrnSkuLevel level)
        => Enum.TryParse(value?.Trim(), ignoreCase: true, out level) && Enum.IsDefined(level);

    public static bool TryParseStockStatus(string? value, out GrnStockStatus status)
        => Enum.TryParse(value?.Trim(), ignoreCase: true, out status) && Enum.IsDefined(status);
}
