using Diten.Platform.Application.Features.BusinessReferenceData.Models;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

/// <summary>
/// MOD-0048-FU01 §4 named step "Universal Item Reference Lists for MOD-0290-FU04" (WP-PLT-ITEM-LISTS-01): the three
/// code-owned universal item lists — <c>item-uom</c>, <c>storage-condition</c>, <c>material-type</c> — deployment-versioned
/// as one identity (ITEM-UNIVERSAL-V1), immutable and identical for every tenant. Like GSKU-UNIVERSAL-V1 they are not
/// tenant-stewarded BRD records: no reference tenant, no assignment, no load / publish, no override. A value change is a
/// new deployment version. The existing <c>uom</c>, <c>pack-applicability</c> and <c>market</c> sets are not touched.
/// "International unit" is not in V1: UN/ECE Rec 20 has no single-unit code (only HIU); the question is open with the
/// inventory and quality units (MOD-0290-FU04 O-21).
/// </summary>
public static class ItemUniversalCatalog
{
    public const string CatalogVersion = "ITEM-UNIVERSAL-V1";
    public const int CatalogVersionNumber = 1;
    public const string ResolutionMode = "LATEST";
    public const int MaximumSelections = 200;

    public const string ItemUomSetCode = "item-uom";
    public const string StorageConditionSetCode = "storage-condition";
    public const string MaterialTypeSetCode = "material-type";

    // item-uom attributes.
    public const string DimensionCode = "DimensionCode";
    public const string MaximumDecimalPrecision = "MaximumDecimalPrecision";
    public const string FactorToDimensionBase = "FactorToDimensionBase";

    // storage-condition attributes (O-2: MOD-0290 enforces "at most one TEMPERATURE" from ConditionKind).
    public const string ConditionKind = "ConditionKind";
    public const string TemperatureMinC = "TemperatureMinC";
    public const string TemperatureMaxC = "TemperatureMaxC";
    public const string ProtectFromLight = "ProtectFromLight";
    public const string ProtectFromMoisture = "ProtectFromMoisture";

    public const string Temperature = "TEMPERATURE";
    public const string Protection = "PROTECTION";

    public static readonly Guid ItemUomCatalogVersionId = Guid.Parse("c3f1d0a4-6b2e-4f0a-9d71-2e5b7a1c0001");
    public static readonly Guid StorageConditionCatalogVersionId = Guid.Parse("c3f1d0a4-6b2e-4f0a-9d71-2e5b7a1c0002");
    public static readonly Guid MaterialTypeCatalogVersionId = Guid.Parse("c3f1d0a4-6b2e-4f0a-9d71-2e5b7a1c0003");

    public static IReadOnlyList<string> SetCodes { get; } = [ItemUomSetCode, StorageConditionSetCode, MaterialTypeSetCode];

    public static IReadOnlyList<ItemUniversalValue> Values { get; } = Array.AsReadOnly(
    [
        // item-uom — UN/ECE Recommendation 20 (Rev 11e), measured 2026-10-07 (MOD-0290-FU04 O-21).
        Uom("C62", "One", 10, "COUNT", 0, "1"),
        Uom("MC", "Microgram", 15, "MASS", 3, "0.000000001"),
        Uom("MGM", "Milligram", 20, "MASS", 3, "0.000001"),
        Uom("GRM", "Gram", 30, "MASS", 3, "0.001"),
        Uom("KGM", "Kilogram", 40, "MASS", 3, "1"),
        Uom("MLT", "Millilitre", 50, "VOLUME", 3, "0.001"),
        Uom("LTR", "Litre", 60, "VOLUME", 3, "1"),
        Uom("MTR", "Metre", 70, "LENGTH", 3, "1"),
        Uom("MTK", "Square metre", 80, "AREA", 3, "1"),
        Uom("XBX", "Box", 90, "PACKAGE", 0, null),
        Uom("XCS", "Case", 100, "PACKAGE", 0, null),
        Uom("XRO", "Roll", 110, "PACKAGE", 0, null),

        Storage("AMBIENT_15_25", "Store at 15-25 °C", 10, Temperature, "15", "25", false, false),
        Storage("COOL_2_8", "Store at 2-8 °C", 20, Temperature, "2", "8", false, false),
        Storage("FROZEN_BELOW_MINUS_20", "Store at or below -20 °C", 30, Temperature, null, "-20", false, false),
        Storage("PROTECT_FROM_LIGHT", "Protect from light", 40, Protection, null, null, true, false),
        Storage("PROTECT_FROM_MOISTURE", "Protect from moisture", 50, Protection, null, null, false, true),

        // material-type — one for one with product-master-bundle v1.1 MaterialType.
        Material("ACTIVE_INGREDIENT", "Active ingredient", 10),
        Material("EXCIPIENT", "Excipient", 20),
        Material("RAW_MATERIAL", "Other raw material", 30),
        Material("PACKAGING_PRIMARY", "Primary packaging", 40),
        Material("PACKAGING_SECONDARY", "Secondary packaging", 50),
        Material("INTERMEDIATE", "Intermediate", 60)
    ]);

    public static bool IsSet(string? setCode) =>
        setCode is ItemUomSetCode or StorageConditionSetCode or MaterialTypeSetCode;

    public static ItemUniversalValue? Find(string setCode, string valueCode) =>
        Values.SingleOrDefault(value => value.SetCode == setCode && value.ValueCode == valueCode);

    public static Guid GetVersionId(string setCode) => setCode switch
    {
        ItemUomSetCode => ItemUomCatalogVersionId,
        StorageConditionSetCode => StorageConditionCatalogVersionId,
        MaterialTypeSetCode => MaterialTypeCatalogVersionId,
        _ => throw new ArgumentOutOfRangeException(nameof(setCode), setCode, "Unsupported item universal catalog set.")
    };

    private static ItemUniversalValue Uom(string code, string name, int sort, string dimension, int precision, string? factor) =>
        new(ItemUomSetCode, code, name, sort, new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [DimensionCode] = dimension,
            [MaximumDecimalPrecision] = precision.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [FactorToDimensionBase] = factor
        });

    private static ItemUniversalValue Storage(string code, string name, int sort, string kind, string? min, string? max,
        bool light, bool moisture) =>
        new(StorageConditionSetCode, code, name, sort, new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [ConditionKind] = kind,
            [TemperatureMinC] = min,
            [TemperatureMaxC] = max,
            [ProtectFromLight] = light ? "true" : "false",
            [ProtectFromMoisture] = moisture ? "true" : "false"
        });

    private static ItemUniversalValue Material(string code, string name, int sort) =>
        new(MaterialTypeSetCode, code, name, sort, new Dictionary<string, string?>(StringComparer.Ordinal));
}
