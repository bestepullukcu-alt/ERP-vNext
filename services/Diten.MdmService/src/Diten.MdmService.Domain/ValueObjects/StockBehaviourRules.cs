using System.Globalization;
using System.Text.RegularExpressions;

namespace Diten.MdmService.Domain.ValueObjects;

/// <summary>
/// WP-MDM-ITEM-MASTER S3 — the record-level stock-behaviour rules (pack §4.2, §4.4, §12; CT decisions O-2, O-9, O-14,
/// O-15, O-25 and the S3 / S6 boundary). Pure: no store, no clock. What S3 enforces on ONE record:
/// numerator and denominator &gt; 0 as exact decimals, units from the list, no pair twice, every unit within 4 edges of the
/// base unit. Consistency ACROSS records and dimensions (contradicting ratios in a cycle, same-dimension derivation,
/// precision) is S6's.
/// </summary>
public static class StockBehaviourRules
{
    public const int MaximumChainEdges = 4;
    public const int MaximumConversions = 16;
    public const int MaximumShelfLifeDays = 3650;
    public const string BoxUom = "XBX";
    public const string CaseUom = "XCS";

    public const string ConversionInvalid = "UOM_CONVERSION_INVALID";
    public const string StorageConditionConflict = "ITEM_STORAGE_CONDITION_CONFLICT";
    public const string StorageConditionInvalid = "STOCK_STORAGE_CONDITION_INVALID";
    public const string UomInvalid = "STOCK_UOM_INVALID";
    public const string PackUomUnknown = "GSKU_PACK_UOM_UNKNOWN";
    public const string Incomplete = "STOCK_BEHAVIOUR_INCOMPLETE";
    public const string ShelfLifeInvalid = "STOCK_SHELF_LIFE_INVALID";
    public const string ShelfLifeExceedsGsku = "FG_SHELF_LIFE_EXCEEDS_GSKU";
    public const string GtinInvalid = "FG_GTIN_INVALID";

    // Exact decimal text: digits, at most one point, no sign, no exponent, no leading zeros before a non-zero digit.
    private static readonly Regex DecimalText = new(@"^(0|[1-9][0-9]{0,17})(\.[0-9]{1,18})?$", RegexOptions.CultureInvariant);

    /// <summary>A decimal written exactly as text and strictly greater than zero (rule 1).</summary>
    public static bool IsPositiveExactDecimal(string? text)
        => text is not null && DecimalText.IsMatch(text)
           && decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
           && value > 0m;

    /// <summary>
    /// The GSKU's box ↔ pack-unit conversion, DERIVED from its pack (O-14, CT decision (a): whatever the pack unit —
    /// 1 box = 100 MLT as well as 1 box = 30 C62). Never entered; the pack unit must be in <c>item-uom</c>.
    /// </summary>
    public static ItemUomConversion DerivedPackConversion(decimal packQuantity, string packUomCode) => new()
    {
        FromUom = BoxUom,
        ToUom = packUomCode,
        Numerator = packQuantity.ToString("0.############################", CultureInfo.InvariantCulture),
        Denominator = "1"
    };

    /// <summary>
    /// The record-level structure of a set of conversions around a base unit (rules 1–4 and the S3 chain bound):
    /// every unit listed, From ≠ To, numerator / denominator positive exact decimals, no unordered pair twice, and every
    /// unit used reaches the base unit in at most <see cref="MaximumChainEdges"/> edges. Null = valid.
    /// </summary>
    public static string? ValidateConversions(
        string baseUom,
        IReadOnlyCollection<ItemUomConversion> conversions,
        IReadOnlySet<string> listedUoms)
    {
        if (!listedUoms.Contains(baseUom)) return UomInvalid;
        if (conversions.Count > MaximumConversions) return ConversionInvalid;
        var pairs = new HashSet<(string, string)>();
        var edges = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var conversion in conversions)
        {
            if (conversion is null
                || !listedUoms.Contains(conversion.FromUom) || !listedUoms.Contains(conversion.ToUom))
                return UomInvalid;
            if (string.Equals(conversion.FromUom, conversion.ToUom, StringComparison.Ordinal)
                || !IsPositiveExactDecimal(conversion.Numerator) || !IsPositiveExactDecimal(conversion.Denominator))
                return ConversionInvalid;
            var pair = string.CompareOrdinal(conversion.FromUom, conversion.ToUom) < 0
                ? (conversion.FromUom, conversion.ToUom)
                : (conversion.ToUom, conversion.FromUom);
            if (!pairs.Add(pair)) return ConversionInvalid;
            Link(conversion.FromUom, conversion.ToUom);
            Link(conversion.ToUom, conversion.FromUom);
        }

        // Breadth-first from the base unit: the distance in edges of every unit used.
        var distance = new Dictionary<string, int>(StringComparer.Ordinal) { [baseUom] = 0 };
        var queue = new Queue<string>([baseUom]);
        while (queue.Count > 0)
        {
            var unit = queue.Dequeue();
            if (!edges.TryGetValue(unit, out var next)) continue;
            foreach (var neighbour in next.Where(neighbour => !distance.ContainsKey(neighbour)))
            {
                distance[neighbour] = distance[unit] + 1;
                queue.Enqueue(neighbour);
            }
        }
        return edges.Keys.All(unit => distance.TryGetValue(unit, out var steps) && steps <= MaximumChainEdges)
            ? null
            : ConversionInvalid;

        void Link(string from, string to)
        {
            if (!edges.TryGetValue(from, out var list)) edges[from] = list = [];
            list.Add(to);
        }
    }

    /// <summary>
    /// Storage conditions: every code listed, no repeat, at most one temperature condition (O-2). Which codes are
    /// temperatures is the list's own <c>ConditionKind</c> attribute (S1's rule), never a code list here. Null = valid.
    /// </summary>
    public static string? ValidateStorageConditions(
        IReadOnlyCollection<string> codes, IReadOnlySet<string> listedCodes, IReadOnlySet<string> temperatureCodes)
    {
        if (codes.Count == 0) return StorageConditionInvalid;
        if (codes.Any(code => !listedCodes.Contains(code))
            || codes.Distinct(StringComparer.Ordinal).Count() != codes.Count)
            return StorageConditionInvalid;
        return codes.Count(temperatureCodes.Contains) > 1 ? StorageConditionConflict : null;
    }

    /// <summary>
    /// O-8 / O-25 (pack §4.2 "gerekli" column), GSKU: <c>Stockable = true</c> needs a base unit, the licensed shelf life,
    /// a non-empty storage list (a GSKU is never packaging) and lot control set. The box ↔ pack conversion is derived
    /// (O-14), so it is never missing.
    /// </summary>
    public static bool GskuIsComplete(
        string? baseUomCode, int? shelfLifeDays, IReadOnlyCollection<string>? storageConditionCodes, bool? lotControlled)
        => !string.IsNullOrEmpty(baseUomCode)
           && shelfLifeDays is not null
           && storageConditionCodes is { Count: > 0 }
           && lotControlled is not null;

    /// <summary>
    /// O-8 / O-25, Finished Good: the base unit is the GSKU's (O-14) and the shelf life may be the GSKU's (D-2), so both
    /// are passed EFFECTIVE; it also needs a case ↔ box conversion, a non-empty storage list, lot and serial control set,
    /// and the sales-box GTIN.
    /// </summary>
    public static bool FinishedGoodIsComplete(
        string? effectiveBaseUomCode,
        int? effectiveShelfLifeDays,
        IReadOnlyCollection<ItemUomConversion>? conversions,
        IReadOnlyCollection<string>? storageConditionCodes,
        bool? lotControlled,
        bool? serialControlled,
        string? gtin)
        => !string.IsNullOrEmpty(effectiveBaseUomCode)
           && effectiveShelfLifeDays is not null
           && conversions is not null
           && conversions.Any(conversion => IsPair(conversion, CaseUom, BoxUom))
           && storageConditionCodes is { Count: > 0 }
           && lotControlled is not null
           && serialControlled is not null
           && !string.IsNullOrEmpty(gtin);

    /// <summary>
    /// O-3, LSKU: everything except serial control is the GSKU's at read time, so an LSKU is stockable exactly when its
    /// GSKU is and its own serial control is set. An LSKU never stores <c>Stockable</c>.
    /// </summary>
    public static bool LskuEffectiveStockable(bool? gskuStockable, bool? lskuSerialControlled)
        => gskuStockable == true && lskuSerialControlled is not null;

    private static bool IsPair(ItemUomConversion conversion, string first, string second)
        => (string.Equals(conversion.FromUom, first, StringComparison.Ordinal)
            && string.Equals(conversion.ToUom, second, StringComparison.Ordinal))
           || (string.Equals(conversion.FromUom, second, StringComparison.Ordinal)
               && string.Equals(conversion.ToUom, first, StringComparison.Ordinal));

    public static bool IsValidShelfLife(int? days) => days is null or >= 1 and <= MaximumShelfLifeDays;

    /// <summary>
    /// S3b (R-06): a GSKU's retest interval / minimum remaining shelf life at receipt — 0 to the shelf-life ceiling
    /// (<see cref="MaximumShelfLifeDays"/>, the same bound as the shelf life); 0 = "no minimum" / "retest at once" is
    /// the stock team's to read.
    /// </summary>
    public static bool IsValidOptionalDays(int? days) => days is null or >= 0 and <= MaximumShelfLifeDays;

    public const string DaysInvalid = "STOCK_DAYS_INVALID";

    /// <summary>O-15: a Finished Good's own shelf life never exceeds its GSKU's registered one.</summary>
    public static bool FinishedGoodShelfLifeWithinGsku(int? finishedGoodDays, int? gskuDays)
        => finishedGoodDays is null || gskuDays is null || finishedGoodDays <= gskuDays;

    /// <summary>
    /// The sales-box GTIN-14 (the (01) of the İTS data matrix): exactly 14 digits whose last is the GS1 mod-10 check
    /// digit (weights 3,1,3,1… from the right, excluding the check digit). No padding, no shorter GTINs in v1.
    /// </summary>
    public static bool IsValidGtin14(string? gtin)
    {
        if (gtin is null || gtin.Length != 14 || !gtin.All(char.IsAsciiDigit)) return false;
        var sum = 0;
        for (var i = 0; i < 13; i++)
        {
            var digit = gtin[12 - i] - '0';
            sum += i % 2 == 0 ? digit * 3 : digit;
        }
        return (10 - sum % 10) % 10 == gtin[13] - '0';
    }
}
