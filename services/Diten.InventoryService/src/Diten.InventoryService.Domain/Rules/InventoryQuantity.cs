using System.Globalization;
using System.Text.RegularExpressions;

namespace Diten.InventoryService.Domain.Rules;

/// <summary>
/// Decimal-string handling for the contract <c>Decimal</c> schema (pack §12, ASSUMPTION-0173-09). Quantities are
/// parsed from strings into <see cref="decimal"/> — never through a float — and written back with at least three
/// fraction digits and no precision loss.
/// </summary>
public static class InventoryQuantity
{
    public const int MaxFractionDigits = 6;

    /// <summary>
    /// Posted quantity: plain digits, optional point with 1–6 fraction digits. No sign, no exponent, no grouping,
    /// no whitespace. Stricter than the contract's <c>^-?\d+(\.\d+)?$</c> because a posted quantity must be
    /// positive and within the ledger's 6-digit scale.
    /// </summary>
    private static readonly Regex PostedQuantityPattern = new(
        @"^\d{1,20}(\.\d{1,6})?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ContractDecimalPattern = new(
        @"^-?\d{1,20}(\.\d{1,20})?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private const string WireFormat = "0.000#########################";

    /// <summary>Parses a posted quantity. Returns false for anything that is not a positive 6-scale decimal.</summary>
    public static bool TryParsePositive(string? raw, out decimal value)
    {
        value = 0m;
        if (raw is null || !PostedQuantityPattern.IsMatch(raw))
        {
            return false;
        }

        if (!decimal.TryParse(raw, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }

        return value > 0m;
    }

    /// <summary>Parses any contract Decimal (upstream conversion factors, for example).</summary>
    public static bool TryParseContractDecimal(string? raw, out decimal value)
    {
        value = 0m;
        return raw is not null
               && ContractDecimalPattern.IsMatch(raw)
               && decimal.TryParse(raw, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                   CultureInfo.InvariantCulture, out value);
    }

    /// <summary>Contract Decimal string with at least three fraction digits (e.g. <c>100.000</c>, <c>0.125500</c> → <c>0.1255</c>).</summary>
    public static string ToWire(decimal value) => value.ToString(WireFormat, CultureInfo.InvariantCulture);

    /// <summary>Number of fraction digits actually needed to represent <paramref name="value"/>.</summary>
    public static int Scale(decimal value)
    {
        var text = value.ToString(CultureInfo.InvariantCulture);
        var point = text.IndexOf('.');
        return point < 0 ? 0 : text.TrimEnd('0').Length - point - 1;
    }
}
