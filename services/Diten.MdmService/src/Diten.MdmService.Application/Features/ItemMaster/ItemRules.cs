using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Application.Features.ItemMaster;

// MOD-0290-FU04 S1: the item's field values after reading a request, and the rules that do not need the store
// (§4.1, §4.4 rules 1, 3, 4 (pair level), §12). The chain rules (≤ 4 steps, no cycle across pairs, same-dimension
// factors) are S6.
public sealed record ItemFieldValues(
    string Name,
    string? Description,
    ItemMaterialType MaterialType,
    string BaseUomCode,
    IReadOnlyList<ItemUomConversion> UomConversions,
    IReadOnlyList<string> StorageConditionCodes,
    bool LotControlled,
    bool Stockable,
    int? ShelfLifeDays,
    int? RetestDays,
    int? MinRemainingShelfLifeDays);

public sealed record ItemReferenceProof(
    ReferenceCatalogSelection MaterialType,
    ReferenceCatalogSelection BaseUom,
    IReadOnlyList<ReferenceCatalogSelection> StorageConditions,
    IReadOnlyDictionary<string, string> StorageConditionKinds);

public static class ItemRules
{
    public const int NameMaximumLength = 200;
    public const int DescriptionMaximumLength = 2000;
    public const int DaysMaximum = 3650;
    public const int MaximumConversions = 50;
    public const int MaximumStorageConditions = 10;
    public const int IdempotencyKeyMaximumLength = 120;
    // The reservation key space is shared by every code-bearing kind (one ledger): the item's own prefix keeps an item
    // key from ever colliding with a Global Product / GSKU / LSKU / Finished Good creation key.
    public const string ReservationCommandPrefix = "ITEM:";

    private static readonly IReadOnlyDictionary<string, ItemMaterialType> MaterialTypeCodes =
        new Dictionary<string, ItemMaterialType>(StringComparer.Ordinal)
        {
            ["ACTIVE_INGREDIENT"] = ItemMaterialType.ActiveIngredient,
            ["EXCIPIENT"] = ItemMaterialType.Excipient,
            ["RAW_MATERIAL"] = ItemMaterialType.RawMaterial,
            ["PACKAGING_PRIMARY"] = ItemMaterialType.PackagingPrimary,
            ["PACKAGING_SECONDARY"] = ItemMaterialType.PackagingSecondary,
            ["INTERMEDIATE"] = ItemMaterialType.Intermediate
        };

    public static IReadOnlyCollection<string> MaterialTypeValueCodes => MaterialTypeCodes.Keys.ToArray();

    public static bool TryParseMaterialType(string? code, out ItemMaterialType type) =>
        MaterialTypeCodes.TryGetValue(code?.Trim() ?? string.Empty, out type);

    public static string Code(ItemMaterialType type) =>
        MaterialTypeCodes.FirstOrDefault(pair => pair.Value == type).Key ?? type.ToString();

    public static bool IsPackaging(ItemMaterialType type) =>
        type is ItemMaterialType.PackagingPrimary or ItemMaterialType.PackagingSecondary;

    public static string NameKey(ItemMaterialType type, string normalizedName) => $"{Code(type)}|{normalizedName}";

    public static string NormalizeName(string name) => GlobalProductNameRules.NormalizeDuplicateKey(name);

    // Reads the request; the structural checks (lengths, ranges, required) are the validator's. Null: the material type
    // code is not one of the six (ITEM_REFERENCE_VALUE_INVALID).
    public static ItemFieldValues? Read(ItemModels.ItemFieldsInput input)
    {
        if (!TryParseMaterialType(input.MaterialType, out var type)) return null;
        return new ItemFieldValues(
            GlobalProductNameRules.CleanVisible(input.Name ?? string.Empty),
            string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
            type,
            (input.BaseUomCode ?? string.Empty).Trim(),
            (input.UomConversions ?? []).Select(conversion => new ItemUomConversion
            {
                FromUom = (conversion.FromUom ?? string.Empty).Trim(),
                ToUom = (conversion.ToUom ?? string.Empty).Trim(),
                Numerator = conversion.Numerator ?? string.Empty,
                Denominator = conversion.Denominator ?? string.Empty
            }).ToList(),
            (input.StorageConditionCodes ?? []).Select(code => (code ?? string.Empty).Trim()).ToList(),
            input.LotControlled ?? false,
            input.Stockable ?? false,
            input.ShelfLifeDays,
            input.RetestDays,
            input.MinRemainingShelfLifeDays);
    }

    // §4.4 rules 1, 3, 4 at pair level: a decimal text > 0 (no exponent, no leading '+'), From ≠ To, one record per unit
    // pair (the reverse direction is derived, never stored). Null when valid; otherwise the cause.
    public static string? ConversionShapeFailure(IReadOnlyList<ItemUomConversion> conversions)
    {
        var pairs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var conversion in conversions)
        {
            if (conversion.FromUom.Length == 0 || conversion.ToUom.Length == 0) return "UOM_REQUIRED";
            if (string.Equals(conversion.FromUom, conversion.ToUom, StringComparison.Ordinal)) return "SAME_UOM";
            if (!PositiveDecimalText(conversion.Numerator) || !PositiveDecimalText(conversion.Denominator))
                return "FACTOR_NOT_A_POSITIVE_DECIMAL";
            var key = string.CompareOrdinal(conversion.FromUom, conversion.ToUom) < 0
                ? $"{conversion.FromUom}|{conversion.ToUom}"
                : $"{conversion.ToUom}|{conversion.FromUom}";
            if (!pairs.Add(key)) return "PAIR_REPEATED";
        }
        return null;
    }

    public const int MaximumFactorLength = 40;

    // FIX1 M5: positivity is read from the text itself — digits with at most one inner '.', and at least one non-zero
    // digit. No numeric parse: a 39-digit factor is not refused for overflowing a decimal, and a factor with 29+
    // decimals is not rounded to zero.
    internal static bool PositiveDecimalText(string text)
    {
        if (text.Length is 0 or > MaximumFactorLength || text.Any(c => !(char.IsAsciiDigit(c) || c == '.'))) return false;
        if (text.Count(c => c == '.') > 1 || text[0] == '.' || text[^1] == '.') return false;
        return text.Any(c => c is >= '1' and <= '9');
    }

    // O-8 / O-9 / O-25: what Stockable = true needs on an item. Null when allowed.
    public static bool StockableIncomplete(ItemFieldValues values) =>
        values.Stockable && !IsPackaging(values.MaterialType)
        && (values.StorageConditionCodes.Count == 0 || values.ShelfLifeDays is null && values.RetestDays is null);

    // Every item-uom code the record uses (base + both sides of each conversion), distinct.
    public static IReadOnlyList<string> UomCodes(ItemFieldValues values) =>
        new[] { values.BaseUomCode }
            .Concat(values.UomConversions.SelectMany(conversion => new[] { conversion.FromUom, conversion.ToUom }))
            .Distinct(StringComparer.Ordinal)
            .ToList();

    public static ReferenceCatalogSelection? ToSelection(ItemReferenceSelection? source) =>
        source is null || source.CatalogVersionId == Guid.Empty || source.CatalogVersionNumber <= 0
        || source.ResolutionMode != "LATEST" || source.ResolvedAtUtc == default
        || source.IsRetired || !source.SelectableForNew
            ? null
            : new ReferenceCatalogSelection
            {
                SetCode = source.SetCode,
                ValueCode = source.ValueCode,
                CatalogVersionId = source.CatalogVersionId,
                CatalogVersionNumber = source.CatalogVersionNumber,
                ResolutionMode = ReferenceCatalogResolutionMode.Latest,
                ResolvedAtUtc = source.ResolvedAtUtc
            };

    // The creation request's facts, for the same-key replay: same key + same facts = the first answer; same key +
    // other facts = IDEMPOTENCY_KEY_CONFLICT.
    public static string CreationFingerprint(ItemFieldValues values)
    {
        var builder = new StringBuilder()
            .Append(NormalizeName(values.Name)).Append('\u001f')
            .Append(values.Description).Append('\u001f')
            .Append(Code(values.MaterialType)).Append('\u001f')
            .Append(values.BaseUomCode).Append('\u001f');
        foreach (var conversion in values.UomConversions)
            builder.Append(conversion.FromUom).Append('>').Append(conversion.ToUom).Append('=')
                .Append(conversion.Numerator).Append('/').Append(conversion.Denominator).Append(';');
        builder.Append('\u001f').AppendJoin(';', values.StorageConditionCodes.Order(StringComparer.Ordinal))
            .Append('\u001f').Append(values.LotControlled).Append('|').Append(values.Stockable)
            .Append('|').Append(values.ShelfLifeDays).Append('|').Append(values.RetestDays)
            .Append('|').Append(values.MinRemainingShelfLifeDays);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())));
    }

    // On an Active / Deactivated item only the name and the description change freely; a locked field may be filled
    // when it is still empty (O-8), never changed. Returns the first locked field the request changes, or null.
    public static string? LockedFieldChanged(Item current, ItemFieldValues next)
    {
        if (current.MaterialType != next.MaterialType) return nameof(Item.MaterialType);
        if (!string.Equals(current.BaseUomCode, next.BaseUomCode, StringComparison.Ordinal)) return nameof(Item.BaseUomCode);
        // FU04 S2: a null lot flag (a legacy document) is empty — its first fill is allowed (O-8), never a change.
        if (current.LotControlled is not null && current.LotControlled != next.LotControlled) return nameof(Item.LotControlled);
        if (current.Stockable != next.Stockable) return nameof(Item.Stockable);
        if (Filled(current.ShelfLifeDays) && current.ShelfLifeDays != next.ShelfLifeDays) return nameof(Item.ShelfLifeDays);
        if (Filled(current.RetestDays) && current.RetestDays != next.RetestDays) return nameof(Item.RetestDays);
        if (Filled(current.MinRemainingShelfLifeDays) && current.MinRemainingShelfLifeDays != next.MinRemainingShelfLifeDays)
            return nameof(Item.MinRemainingShelfLifeDays);
        if (current.StorageConditionCodes.Count > 0
            && !current.StorageConditionCodes.Order(StringComparer.Ordinal)
                .SequenceEqual(next.StorageConditionCodes.Order(StringComparer.Ordinal), StringComparer.Ordinal))
            return nameof(Item.StorageConditionCodes);
        if (current.UomConversions.Count > 0 && !SameConversions(current.UomConversions, next.UomConversions))
            return nameof(Item.UomConversions);
        return null;
    }

    private static bool Filled(int? value) => value is not null;

    // FIX1 I6: the same conversions in another order are not a change (a record per unit pair, §4.4 rule 3).
    private static bool SameConversions(IReadOnlyList<ItemUomConversion> left, IReadOnlyList<ItemUomConversion> right) =>
        left.Count == right.Count
        && left.Select(Key).Order(StringComparer.Ordinal).SequenceEqual(right.Select(Key).Order(StringComparer.Ordinal),
            StringComparer.Ordinal);

    private static string Key(ItemUomConversion c) => $"{c.FromUom}>{c.ToUom}={c.Numerator}/{c.Denominator}";

    public static ItemModels.ItemDto ToDto(Item item) => new(
        item.Id,
        item.CanonicalCode,
        item.Name,
        item.Description,
        Code(item.MaterialType),
        item.BaseUomCode,
        item.UomConversions.Select(conversion =>
            new ItemModels.ItemUomConversionDto(conversion.FromUom, conversion.ToUom, conversion.Numerator, conversion.Denominator))
            .ToList(),
        item.StorageConditionCodes.ToList(),
        item.LotControlled,
        item.Stockable,
        item.ShelfLifeDays,
        item.RetestDays,
        item.MinRemainingShelfLifeDays,
        item.LifecycleStatus,
        item.Version,
        item.CreatedAt,
        item.UpdatedAt);
}
