using System.Globalization;
using System.Text.Json.Nodes;
using Diten.MdmService.Application.Contracts.ReferenceData;
using Diten.MdmService.Application.Features.ItemMaster;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Application.Features.ProductMaster;

/// <summary>
/// MOD-0290-FU04 S4 — the product-master-bundle v1.1 shapes (pack §16). Bare JSON, not the MDM envelope: a field the
/// record does not have yet is ABSENT in ProductRef and null in SkuRef (contract x-notes, O-25), and a Global Product's
/// ProductRef carries no stock field at all (O-1 — behaviour narrowing, not behind a flag).
/// </summary>
public static class ProductMasterContract
{
    public const string ContractVersion = "v1.1";
    public const string ReadPermission = "mdm.product-master.read";

    public const int MaximumBatch = 200;
    public const int MaximumPageSize = 100;
    public const int DefaultPageSize = 25;
    public const int MaximumPage = 1_000_000;
    public const int MaximumQueryLength = 200;
    // S4 FIX1 (R4): how deep the stockable search pages — page × pageSize; past it 400 INVALID_PARAMETER (proposed).
    public const int MaximumSearchWindow = 1000;
    // S4 FIX1 (R5): the POST bodies (bulk read, validate) — 200 UUIDs are ~8 KB; past it 413 REQUEST_TOO_LARGE.
    public const int MaximumBodyBytes = 64 * 1024;

    public const string UnknownItem = "UNKNOWN_ITEM";
    public const string UnknownSku = "UNKNOWN_SKU";
    public const string BatchLimitExceeded = "BATCH_LIMIT_EXCEEDED";
    public const string InvalidUuid = "INVALID_UUID";
    public const string InvalidParameter = "INVALID_PARAMETER";
    public const string InvalidRef = "INVALID_REF";
    public const string TenantContextRequired = "TENANT_CONTEXT_REQUIRED";
    public const string Unavailable = "PRODUCT_MASTER_UNAVAILABLE";
    public const string RequestTooLarge = "REQUEST_TOO_LARGE";

    public const string LevelGsku = "Gsku";
    public const string LevelLsku = "Lsku";
    public const string LevelFinishedGood = "FinishedGood";
    public const string LevelItem = "Item";
    public static readonly IReadOnlyList<string> SkuLevels = [LevelGsku, LevelLsku, LevelFinishedGood, LevelItem];
    // S4 FIX1: a level-less id is looked up as an item first (no product scope, one read).
    public static readonly IReadOnlyList<string> LevelResolutionOrder = [LevelItem, LevelGsku, LevelLsku, LevelFinishedGood];

    public const string KindGlobalProduct = "GlobalProduct";
    public const string KindItem = "Item";

    public static readonly IReadOnlyList<string> LifecycleStatuses = ["Draft", "Active", "Archived", "Deactivated", "Retired"];

    /// <summary>Contract ids are the hyphenated 36-character UUID form (format: uuid).</summary>
    public static bool TryParseId(string? text, out Guid id) =>
        Guid.TryParseExact(text, "D", out id);

    // ------------------------------------------------------------------------------------------- §16.3 status

    public static string Status(ItemLifecycleStatus status, ItemActivationKind? activationKind) => status switch
    {
        ItemLifecycleStatus.Draft => "Draft",
        ItemLifecycleStatus.PendingActivation => activationKind == ItemActivationKind.Reactivation ? "Deactivated" : "Draft",
        ItemLifecycleStatus.Active => "Active",
        ItemLifecycleStatus.Deactivated => "Deactivated",
        ItemLifecycleStatus.Retired => "Retired",
        _ => status.ToString()
    };

    // O-4: v1.0 Archived is never published.
    public static string Status(ProductIdentityLifecycleStatus status) => status switch
    {
        ProductIdentityLifecycleStatus.Draft or ProductIdentityLifecycleStatus.PendingIdentityApproval => "Draft",
        ProductIdentityLifecycleStatus.IdentityApproved => "Active",
        ProductIdentityLifecycleStatus.Retired => "Retired",
        _ => status.ToString()
    };

    public static string Status(Item item) => Status(item.LifecycleStatus, item.ActivationKind);

    // ------------------------------------------------------------------------------------------- O-5 names

    public static string DecimalText(decimal value) =>
        value.ToString("0.############################", CultureInfo.InvariantCulture);

    public static string GskuName(GlobalProduct product, Gsku gsku) =>
        $"{product.GlobalProductName} · {DecimalText(gsku.PackQuantity)} {gsku.PackUomCode}";

    public static string LskuName(GlobalProduct product, Gsku gsku, Lsku lsku) =>
        $"{GskuName(product, gsku)} · {lsku.MarketCode}";

    // A Finished Good is named as its GSKU (O-5).
    public static string FinishedGoodName(GlobalProduct product, Gsku gsku) => GskuName(product, gsku);

    // ------------------------------------------------------------------------------------------- stockable

    public static bool GskuStockable(Gsku gsku) =>
        gsku.Stockable == true
        && StockBehaviourRules.GskuIsComplete(gsku.BaseUomCode, gsku.ShelfLifeDays, gsku.StorageConditionCodes, gsku.LotControlled);

    public static bool LskuStockable(Gsku gsku, Lsku lsku) =>
        StockBehaviourRules.LskuEffectiveStockable(GskuStockable(gsku), lsku.SerialControlled);

    public static bool FinishedGoodStockable(Gsku gsku, FinishedGood finishedGood) =>
        finishedGood.Stockable == true
        && StockBehaviourRules.FinishedGoodIsComplete(
            gsku.BaseUomCode,
            finishedGood.ShelfLifeDays ?? gsku.ShelfLifeDays,
            finishedGood.UomConversions,
            finishedGood.StorageConditionCodes,
            finishedGood.LotControlled,
            finishedGood.SerialControlled,
            finishedGood.Gtin);

    // ------------------------------------------------------------------------------------------- ProductRef

    // O-1 as narrowed by OD-1 (stock team's written answer, 2026-10-09): a Global Product carries none of the v1.0
    // stock fields EXCEPT the deprecated lot / serial pair, derived from its SKUs (getSku stays the authority).
    public static JsonObject GlobalProductRef(GlobalProduct product, bool? lotControlled, bool? serialControlled) => new()
    {
        ["itemId"] = Id(product.Id),
        ["itemKind"] = KindGlobalProduct,
        ["canonicalCode"] = product.CanonicalCode,
        ["name"] = product.GlobalProductName,
        ["lifecycleStatus"] = Status(product.LifecycleStatus),
        ["lotControlled"] = lotControlled,
        ["serialControlled"] = serialControlled,
        ["contractVersion"] = ContractVersion
    };

    /// <summary>
    /// OD-1, draft.7 value rule — true in doubt: any SKU true or null → true; only when every SKU is definitively false →
    /// false; no SKU → null. (The stock side reads a missing flag as "no"; a "disagreement → null" rule would silently
    /// switch lot / serial control off.) Lot is read over the product's non-retired GSKUs, serial over its non-retired
    /// LSKUs and Finished Goods (serial is decided at SKU level; a GSKU has none).
    /// </summary>
    public static bool? TrueInDoubt(IEnumerable<bool?> values)
    {
        var any = false;
        foreach (var value in values)
        {
            if (value != false) return true;
            any = true;
        }
        return any ? false : null;
    }

    public static JsonObject ItemProductRef(Item item)
    {
        var node = new JsonObject
        {
            ["itemId"] = Id(item.Id),
            ["itemKind"] = KindItem,
            ["canonicalCode"] = item.CanonicalCode,
            ["name"] = item.Name,
            ["materialType"] = ItemRules.Code(item.MaterialType),
            ["lifecycleStatus"] = Status(item)
        };
        if (!string.IsNullOrEmpty(item.BaseUomCode)) node["baseUomId"] = item.BaseUomCode;
        node["shelfLifeDays"] = item.ShelfLifeDays;
        node["retestDays"] = item.RetestDays;
        node["minRemainingShelfLifeDays"] = item.MinRemainingShelfLifeDays;
        // v1.0 single condition: the item's temperature condition, as the list classed it when it was stored.
        node["storageCondition"] = TemperatureCondition(item);
        node["storageConditions"] = Codes(item.StorageConditionCodes);
        node["lotControlled"] = item.LotControlled;
        // An item has no serial tracking: false means "does not apply" (contract x-notes).
        node["serialControlled"] = false;
        node["stockable"] = item.Stockable;
        node["contractVersion"] = ContractVersion;
        return node;
    }

    private static string? TemperatureCondition(Item item) =>
        item.StorageConditionKinds is null
            ? null
            : item.StorageConditionCodes.FirstOrDefault(code =>
                item.StorageConditionKinds.TryGetValue(code, out var kind)
                && string.Equals(kind, ItemReferenceSets.TemperatureKind, StringComparison.Ordinal));

    // ------------------------------------------------------------------------------------------- SkuRef (§16.1 table)

    public static JsonObject ItemSkuRef(Item item)
    {
        var node = new JsonObject
        {
            ["skuId"] = Id(item.Id),
            ["skuLevel"] = LevelItem,
            ["itemKind"] = KindItem,
            ["canonicalCode"] = item.CanonicalCode,
            ["itemId"] = Id(item.Id),
            ["name"] = item.Name,
            ["lifecycleStatus"] = Status(item),
            ["packUomCode"] = null,
            ["gtin"] = null,
            ["marketCode"] = null,
            ["materialType"] = ItemRules.Code(item.MaterialType),
            ["baseUomId"] = string.IsNullOrEmpty(item.BaseUomCode) ? null : item.BaseUomCode
        };
        ShelfLife(node, item.ShelfLifeDays, "Own");
        node["retestDays"] = item.RetestDays;
        node["minRemainingShelfLifeDays"] = item.MinRemainingShelfLifeDays;
        node["storageConditions"] = Codes(item.StorageConditionCodes);
        node["lotControlled"] = item.LotControlled;
        node["serialControlled"] = false;
        node["stockable"] = item.Stockable;
        node["contractVersion"] = ContractVersion;
        return node;
    }

    public static JsonObject GskuSkuRef(GlobalProduct product, Gsku gsku)
    {
        var node = ProductSkuHead(gsku.Id, LevelGsku, gsku.CanonicalCode, product, GskuName(product, gsku),
            Status(gsku.LifecycleStatus));
        node["packUomCode"] = gsku.PackUomCode;
        node["packQuantity"] = DecimalText(gsku.PackQuantity);
        node["gtin"] = null;
        node["marketCode"] = null;
        node["materialType"] = null;
        node["baseUomId"] = gsku.BaseUomCode;
        ShelfLife(node, gsku.ShelfLifeDays, "Own");
        // S3b (R-06): the GSKU's own; an LSKU and a Finished Good read them from their GSKU (never copied).
        node["retestDays"] = gsku.RetestDays;
        node["minRemainingShelfLifeDays"] = gsku.MinRemainingShelfLifeDays;
        if (gsku.StorageConditionCodes is { Count: > 0 } codes) node["storageConditions"] = Codes(codes);
        node["lotControlled"] = gsku.LotControlled;
        // Serial control is decided at LSKU / Finished Good level; a GSKU has none.
        node["serialControlled"] = null;
        node["stockable"] = GskuStockable(gsku);
        node["contractVersion"] = ContractVersion;
        return node;
    }

    // O-3: everything but serial control is the GSKU's at read time; no GTIN on an LSKU yet (O-6).
    public static JsonObject LskuSkuRef(GlobalProduct product, Gsku gsku, Lsku lsku)
    {
        var node = ProductSkuHead(lsku.Id, LevelLsku, lsku.CanonicalCode, product, LskuName(product, gsku, lsku),
            Status(lsku.LifecycleStatus));
        node["packUomCode"] = gsku.PackUomCode;
        node["packQuantity"] = DecimalText(gsku.PackQuantity);
        node["gtin"] = null;
        node["marketCode"] = lsku.MarketCode;
        node["materialType"] = null;
        node["baseUomId"] = gsku.BaseUomCode;
        ShelfLife(node, gsku.ShelfLifeDays, "Gsku");
        // S3b (R-06): the GSKU's own; an LSKU and a Finished Good read them from their GSKU (never copied).
        node["retestDays"] = gsku.RetestDays;
        node["minRemainingShelfLifeDays"] = gsku.MinRemainingShelfLifeDays;
        if (gsku.StorageConditionCodes is { Count: > 0 } codes) node["storageConditions"] = Codes(codes);
        node["lotControlled"] = gsku.LotControlled;
        node["serialControlled"] = lsku.SerialControlled;
        node["stockable"] = LskuStockable(gsku, lsku);
        node["contractVersion"] = ContractVersion;
        return node;
    }

    // O-14: the base unit is the GSKU's; D-2: the shelf life is the FG's own or, when it has none, the GSKU's.
    public static JsonObject FinishedGoodSkuRef(GlobalProduct product, Gsku gsku, FinishedGood finishedGood)
    {
        var node = ProductSkuHead(finishedGood.Id, LevelFinishedGood, finishedGood.CanonicalCode, product,
            FinishedGoodName(product, gsku), Status(finishedGood.LifecycleStatus));
        node["gtin"] = finishedGood.Gtin;
        node["marketCode"] = null;
        node["materialType"] = null;
        node["baseUomId"] = gsku.BaseUomCode;
        if (finishedGood.ShelfLifeDays is not null) ShelfLife(node, finishedGood.ShelfLifeDays, "Own");
        else ShelfLife(node, gsku.ShelfLifeDays, "Gsku");
        // S3b (R-06): the GSKU's own; an LSKU and a Finished Good read them from their GSKU (never copied).
        node["retestDays"] = gsku.RetestDays;
        node["minRemainingShelfLifeDays"] = gsku.MinRemainingShelfLifeDays;
        if (finishedGood.StorageConditionCodes is { Count: > 0 } codes) node["storageConditions"] = Codes(codes);
        node["lotControlled"] = finishedGood.LotControlled;
        node["serialControlled"] = finishedGood.SerialControlled;
        node["stockable"] = FinishedGoodStockable(gsku, finishedGood);
        node["contractVersion"] = ContractVersion;
        return node;
    }

    private static JsonObject ProductSkuHead(
        Guid skuId, string level, string canonicalCode, GlobalProduct product, string name, string status) => new()
    {
        ["skuId"] = Id(skuId),
        ["skuLevel"] = level,
        ["itemKind"] = KindGlobalProduct,
        ["canonicalCode"] = canonicalCode,
        ["itemId"] = Id(product.Id),
        ["name"] = name,
        ["lifecycleStatus"] = status
    };

    // shelfLifeDaysSource is a non-null enum: it is written only next to a shelf life.
    private static void ShelfLife(JsonObject node, int? days, string source)
    {
        node["shelfLifeDays"] = days;
        if (days is not null) node["shelfLifeDaysSource"] = source;
    }

    // ------------------------------------------------------------------------------------------- link, uom

    public static JsonObject SkuProductLink(Guid skuId, string level, Guid itemId, string itemKind) => new()
    {
        ["skuId"] = Id(skuId),
        ["skuLevel"] = level,
        ["itemId"] = Id(itemId),
        ["itemKind"] = itemKind,
        ["contractVersion"] = ContractVersion
    };

    public static JsonObject UomInfo(string? baseUom, IEnumerable<ItemUomConversion> conversions)
    {
        var node = new JsonObject();
        if (!string.IsNullOrEmpty(baseUom)) node["baseUomId"] = baseUom;
        var list = new JsonArray();
        var pairs = new HashSet<(string, string)>();
        foreach (var conversion in conversions)
        {
            var pair = string.CompareOrdinal(conversion.FromUom, conversion.ToUom) < 0
                ? (conversion.FromUom, conversion.ToUom)
                : (conversion.ToUom, conversion.FromUom);
            if (!pairs.Add(pair)) continue;
            list.Add(new JsonObject
            {
                ["fromUom"] = conversion.FromUom,
                ["toUom"] = conversion.ToUom,
                ["numerator"] = conversion.Numerator,
                ["denominator"] = conversion.Denominator,
                ["effectiveFrom"] = null,
                ["effectiveTo"] = null
            });
        }
        node["conversions"] = list;
        node["contractVersion"] = ContractVersion;
        return node;
    }

    // The GSKU's chain: the derived box ↔ pack unit (O-14), then its own entries.
    public static IEnumerable<ItemUomConversion> GskuConversions(Gsku gsku) =>
        new[] { StockBehaviourRules.DerivedPackConversion(gsku.PackQuantity, gsku.PackUomCode) }
            .Concat(gsku.UomConversions ?? []);

    // ------------------------------------------------------------------------------------------- errors

    public static JsonObject Error(string code, string message, Guid correlationId) => new()
    {
        ["error"] = new JsonObject
        {
            ["code"] = code,
            ["message"] = message,
            ["details"] = new JsonObject(),
            ["correlationId"] = correlationId.ToString("D")
        },
        ["contractVersion"] = ContractVersion
    };

    public static string Message(string code) => code switch
    {
        UnknownItem => "No item with this id.",
        UnknownSku => "No SKU with this id at this level.",
        BatchLimitExceeded => "The request carries no entry or more than 200.",
        InvalidUuid => "An id is not a UUID.",
        InvalidParameter => "A request parameter is missing or not valid.",
        InvalidRef => "A reference is not in a valid shape.",
        RequestTooLarge => "The request body is larger than 64 KB.",
        _ => "The product master could not answer."
    };

    internal static string Id(Guid id) => id.ToString("D");

    private static JsonArray Codes(IEnumerable<string> codes) => new(codes.Select(code => (JsonNode?)code).ToArray());
}
