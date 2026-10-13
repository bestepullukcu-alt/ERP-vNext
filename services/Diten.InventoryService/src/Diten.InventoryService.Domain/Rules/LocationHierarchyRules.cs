using System.Globalization;
using System.Text.RegularExpressions;
using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.Rules;

/// <summary>
/// MOD-0173-FU02 hierarchy rules (pack §12), framework-free:
/// <list type="bullet">
/// <item>K-01 parent matrix: Site → none · Warehouse → Site · StorageLocation → Warehouse · Zone → StorageLocation ·
/// Bin → Zone or StorageLocation.</item>
/// <item>K-08 stockable: default Bin = true, others false; only a Bin or a StorageLocation may hold stock
/// (ASSUMPTION-LOC1-04).</item>
/// <item>K-09 operating mode: Site / Warehouse only.</item>
/// <item>Field shapes (code, name, cost centre) and the K-12 bin code pattern <c>{AISLE}-{RACK:00}-{LEVEL:00}-{POS:00}</c>.</item>
/// </list>
/// </summary>
public static partial class LocationHierarchyRules
{
    public const int MaxCodeLength = 40;
    public const int MaxNameLength = 120;
    public const int MaxCostCenterLength = 40;
    public const int MaxBulkBins = 1000;

    public const string OperatingModeInternal = "internal";
    public const string OperatingModeExternalIntegrated = "external-integrated";

    public static readonly IReadOnlyList<string> OperatingModes = [OperatingModeInternal, OperatingModeExternalIntegrated];

    private static readonly IReadOnlyDictionary<LocationType, LocationType[]> AllowedParents = new Dictionary<LocationType, LocationType[]>
    {
        [LocationType.Site] = [],
        [LocationType.Warehouse] = [LocationType.Site],
        [LocationType.StorageLocation] = [LocationType.Warehouse],
        [LocationType.Zone] = [LocationType.StorageLocation],
        [LocationType.Bin] = [LocationType.Zone, LocationType.StorageLocation]
    };

    /// <summary>K-01: may a node of <paramref name="type"/> sit under <paramref name="parentType"/> (null = no parent)?</summary>
    public static bool IsAllowedParent(LocationType type, LocationType? parentType)
        => parentType is null
            ? type == LocationType.Site
            : AllowedParents[type].Contains(parentType.Value);

    /// <summary>K-08 default: a Bin holds stock, nothing else does until said so.</summary>
    public static bool DefaultStockable(LocationType type) => type == LocationType.Bin;

    /// <summary>Only a Bin or a StorageLocation may be stockable (ASSUMPTION-LOC1-04).</summary>
    public static bool MayBeStockable(LocationType type) => type is LocationType.Bin or LocationType.StorageLocation;

    /// <summary>K-09: operating mode lives on Site / Warehouse only.</summary>
    public static bool MayHaveOperatingMode(LocationType type) => type is LocationType.Site or LocationType.Warehouse;

    /// <summary>K-12: bulk bins go under a Zone or a StorageLocation.</summary>
    public static bool MayHoldBulkBins(LocationType type) => type is LocationType.Zone or LocationType.StorageLocation;

    public static bool IsValidCode(string? code)
        => code is { Length: > 0 and <= MaxCodeLength } && CodePattern().IsMatch(code);

    public static bool IsValidName(string? name) => name is { Length: > 0 and <= MaxNameLength };

    public static bool IsValidCostCenter(string? code)
        => code is { Length: > 0 and <= MaxCostCenterLength } && CostCenterPattern().IsMatch(code);

    public static bool IsValidOperatingMode(string? mode) => mode is not null && OperatingModes.Contains(mode, StringComparer.Ordinal);

    /// <summary>Uniqueness key of a code: upper-invariant (case-insensitive per company, K-02).</summary>
    public static string NormalizeCode(string code) => code.ToUpperInvariant();

    /// <summary>
    /// K-12 code generation. Aisles are one letter A–Z or 1–3 digits, both bounds of the same kind and ascending;
    /// racks / levels 1–99 ascending; positions per level 1–99. Returns null with a reason when the shape is wrong;
    /// the count limit is checked by the caller on <see cref="BulkBinPlan.Count"/> BEFORE the codes are built.
    /// </summary>
    public static BulkBinPlan? PlanBulkBins(
        string aisleFrom, string aisleTo, int rackFrom, int rackTo, int levelFrom, int levelTo, int positionsPerLevel, out string? error)
    {
        error = null;
        var aisles = Aisles(aisleFrom, aisleTo);
        if (aisles is null)
        {
            error = "aisleFrom/aisleTo must both be one letter A–Z or both 1–3 digits, ascending.";
            return null;
        }

        if (!InRange(rackFrom) || !InRange(rackTo) || rackFrom > rackTo)
        {
            error = "rackFrom/rackTo must be 1–99, ascending.";
            return null;
        }

        if (!InRange(levelFrom) || !InRange(levelTo) || levelFrom > levelTo)
        {
            error = "levelFrom/levelTo must be 1–99, ascending.";
            return null;
        }

        if (!InRange(positionsPerLevel))
        {
            error = "positionsPerLevel must be 1–99.";
            return null;
        }

        return new BulkBinPlan(aisles, rackFrom, rackTo, levelFrom, levelTo, positionsPerLevel);
    }

    private static bool InRange(int value) => value is >= 1 and <= 99;

    private static IReadOnlyList<string>? Aisles(string from, string to)
    {
        if (from.Length == 1 && to.Length == 1 && from[0] is >= 'A' and <= 'Z' && to[0] is >= 'A' and <= 'Z')
        {
            return from[0] <= to[0]
                ? Enumerable.Range(from[0], to[0] - from[0] + 1).Select(c => ((char)c).ToString()).ToList()
                : null;
        }

        if (DigitsPattern().IsMatch(from) && DigitsPattern().IsMatch(to))
        {
            var a = int.Parse(from, NumberStyles.None, CultureInfo.InvariantCulture);
            var b = int.Parse(to, NumberStyles.None, CultureInfo.InvariantCulture);
            // A digit aisle keeps the width the caller typed for the lower bound ("01".."12" → 01, 02 … 12).
            var width = from.Length;
            return a <= b
                ? Enumerable.Range(a, b - a + 1).Select(n => n.ToString(new string('0', width), CultureInfo.InvariantCulture)).ToList()
                : null;
        }

        return null;
    }

    [GeneratedRegex("^[A-Za-z0-9._/-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CodePattern();

    [GeneratedRegex("^[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant)]
    private static partial Regex CostCenterPattern();

    [GeneratedRegex("^[0-9]{1,3}$", RegexOptions.CultureInvariant)]
    private static partial Regex DigitsPattern();
}

/// <summary>A validated K-12 range. <see cref="Count"/> is known without building the codes.</summary>
public sealed record BulkBinPlan(IReadOnlyList<string> Aisles, int RackFrom, int RackTo, int LevelFrom, int LevelTo, int PositionsPerLevel)
{
    public long Count => (long)Aisles.Count * (RackTo - RackFrom + 1) * (LevelTo - LevelFrom + 1) * PositionsPerLevel;

    /// <summary><c>{AISLE}-{RACK:00}-{LEVEL:00}-{POS:00}</c> in aisle → rack → level → position order.</summary>
    public IReadOnlyList<string> Codes()
    {
        var codes = new List<string>();
        foreach (var aisle in Aisles)
            for (var rack = RackFrom; rack <= RackTo; rack++)
                for (var level = LevelFrom; level <= LevelTo; level++)
                    for (var position = 1; position <= PositionsPerLevel; position++)
                        codes.Add(string.Create(CultureInfo.InvariantCulture, $"{aisle}-{rack:00}-{level:00}-{position:00}"));
        return codes;
    }
}
