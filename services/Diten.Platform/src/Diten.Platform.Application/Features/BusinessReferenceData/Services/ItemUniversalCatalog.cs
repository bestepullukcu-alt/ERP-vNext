using Diten.Platform.Application.Features.BusinessReferenceData.Models;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Services;

/// <summary>
/// MOD-0048-FU01 §4 named step "Universal Item Reference Lists for MOD-0290-FU04" (WP-PLT-ITEM-LISTS-01): the three
/// code-owned universal item lists — <c>item-uom</c>, <c>storage-condition</c>, <c>material-type</c> — deployment-versioned
/// as one identity, immutable and identical for every tenant. Like GSKU-UNIVERSAL-V1 they are not tenant-stewarded BRD
/// records: no reference tenant, no assignment, no load / publish, no override. The existing <c>uom</c>,
/// <c>pack-applicability</c> and <c>market</c> sets are not touched.
///
/// Versioning: a value change is a new deployment version, and only the CHANGED list gets a new version id and number;
/// an unchanged list keeps both, so a proof an item stored under an earlier deployment is the same proof today. MDM keeps
/// the proof of a code it already holds and resolves only new codes (ItemReferenceCheck), so a V1 proof stays valid.
///   ITEM-UNIVERSAL-V1 (2026-10-07): the three lists, each version 1.
///   ITEM-UNIVERSAL-V2 (2026-10-09, WP-PLT-ITEM-LISTS-02): item-uom version 2 — the local unit IU (quality unit's
///     decision, OD-7; MOD-0290-FU04 O-21) and the CodeStandard attribute on every unit. storage-condition and
///     material-type unchanged (version 1, same ids).
/// </summary>
public static class ItemUniversalCatalog
{
    public const string CatalogVersion = "ITEM-UNIVERSAL-V2";
    public const string ResolutionMode = "LATEST";
    public const int MaximumSelections = 200;

    public const string ItemUomSetCode = "item-uom";
    public const string StorageConditionSetCode = "storage-condition";
    public const string MaterialTypeSetCode = "material-type";

    // item-uom attributes.
    public const string DimensionCode = "DimensionCode";
    public const string MaximumDecimalPrecision = "MaximumDecimalPrecision";
    public const string FactorToDimensionBase = "FactorToDimensionBase";
    // Which code list a unit code comes from: UN/ECE Recommendation 20, or a local code Rec 20 does not have.
    public const string CodeStandard = "CodeStandard";

    public const string UnEceRec20 = "UNECE_REC20";
    public const string Local = "LOCAL";
    // IU's own dimension: an IU is a measure of biological activity, specific to each substance. IU ↔ mg is never
    // derived from a factor (it is not MASS); an item states it as an explicit conversion.
    public const string BiologicalActivity = "BIOLOGICAL_ACTIVITY";

    // storage-condition attributes (O-2: MOD-0290 enforces "at most one TEMPERATURE" from ConditionKind).
    public const string ConditionKind = "ConditionKind";
    public const string TemperatureMinC = "TemperatureMinC";
    public const string TemperatureMaxC = "TemperatureMaxC";
    public const string ProtectFromLight = "ProtectFromLight";
    public const string ProtectFromMoisture = "ProtectFromMoisture";

    public const string Temperature = "TEMPERATURE";
    public const string Protection = "PROTECTION";

    // item-uom version 1 (ITEM-UNIVERSAL-V1): kept named, because items created under V1 carry it as their proof.
    public static readonly Guid ItemUomCatalogVersionIdV1 = Guid.Parse("c3f1d0a4-6b2e-4f0a-9d71-2e5b7a1c0001");
    public static readonly Guid ItemUomCatalogVersionId = Guid.Parse("c3f1d0a4-6b2e-4f0a-9d71-2e5b7a1c0004");
    public static readonly Guid StorageConditionCatalogVersionId = Guid.Parse("c3f1d0a4-6b2e-4f0a-9d71-2e5b7a1c0002");
    public static readonly Guid MaterialTypeCatalogVersionId = Guid.Parse("c3f1d0a4-6b2e-4f0a-9d71-2e5b7a1c0003");

    public static IReadOnlyList<string> SetCodes { get; } = [ItemUomSetCode, StorageConditionSetCode, MaterialTypeSetCode];

    public static IReadOnlyList<ItemUniversalValue> Values { get; } = Array.AsReadOnly(
    [
        // item-uom — UN/ECE Recommendation 20 (Rev 11e), measured 2026-10-07 (MOD-0290-FU04 O-21); X-codes are Rec 21
        // package codes, which Rec 20 carries with the X prefix.
        Uom("C62", "One", 10, "COUNT", 0, "1", UnEceRec20),
        Uom("MC", "Microgram", 15, "MASS", 3, "0.000000001", UnEceRec20),
        Uom("MGM", "Milligram", 20, "MASS", 3, "0.000001", UnEceRec20),
        Uom("GRM", "Gram", 30, "MASS", 3, "0.001", UnEceRec20),
        Uom("KGM", "Kilogram", 40, "MASS", 3, "1", UnEceRec20),
        // V2: the international unit. Rec 20 has no single-unit code — its HIU (hundred international units) is not used
        // (quality unit's decision, 2026-10-09). Whole units: a stock quantity in IU is counted, not weighed; a potency
        // per mg is a conversion factor, which has its own precision.
        Uom("IU", "International unit", 45, BiologicalActivity, 0, "1", Local),
        Uom("MLT", "Millilitre", 50, "VOLUME", 3, "0.001", UnEceRec20),
        Uom("LTR", "Litre", 60, "VOLUME", 3, "1", UnEceRec20),
        Uom("MTR", "Metre", 70, "LENGTH", 3, "1", UnEceRec20),
        Uom("MTK", "Square metre", 80, "AREA", 3, "1", UnEceRec20),
        Uom("XBX", "Box", 90, "PACKAGE", 0, null, UnEceRec20),
        Uom("XCS", "Case", 100, "PACKAGE", 0, null, UnEceRec20),
        Uom("XRO", "Roll", 110, "PACKAGE", 0, null, UnEceRec20),

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

    public static int GetVersionNumber(string setCode) => setCode switch
    {
        ItemUomSetCode => 2,
        StorageConditionSetCode => 1,
        MaterialTypeSetCode => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(setCode), setCode, "Unsupported item universal catalog set.")
    };

    private static ItemUniversalValue Uom(string code, string name, int sort, string dimension, int precision, string? factor,
        string standard) =>
        new(ItemUomSetCode, code, name, sort, new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [DimensionCode] = dimension,
            [MaximumDecimalPrecision] = precision.ToString(System.Globalization.CultureInfo.InvariantCulture),
            [FactorToDimensionBase] = factor,
            [CodeStandard] = standard
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
