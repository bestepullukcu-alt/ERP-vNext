using Diten.MdmService.Application.Features.ProductMaster;
using Diten.MdmService.Domain.Entities;
using Diten.MdmService.Domain.Enums;
using Diten.MdmService.Domain.ValueObjects;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.StockBehaviour;

/// <summary>
/// MOD-0290-FU04 S5 (O-12, CT decision 3) — the "Stock behaviour" section of the GSKU / LSKU / Finished Good detail: the
/// values as the record has them (an LSKU's and a Finished Good's inherited ones read from its GSKU at read time, with
/// their source), whether it is stockable and — when not — which fields are missing, and which fields are locked (O-8:
/// free on a Draft; on an approved record a filled scalar is locked and a list only gains items). The screen draws from
/// this alone; it never reads the service-to-service contract (<c>/api/product-master</c>).
/// <para>S5-FIX1 (1): <c>Editable</c> is exactly what the write accepts — the status rule
/// (<see cref="StockBehaviourContext.Editable"/>, a Finished Good's draft cancellation by
/// <see cref="StockBehaviourContext.FinishedGoodEditable"/>) and no operation holding the record (the write's own
/// predicates, <c>IProductStockBehaviourRepository.*HeldAsync</c>).</para>
/// </summary>
public sealed record StockBehaviourDto(
    string? BaseUomCode,
    string? BaseUomSource,
    IReadOnlyList<StockUomConversionDto> UomConversions,
    StockUomConversionDto? DerivedPackConversion,
    int? ShelfLifeDays,
    string? ShelfLifeDaysSource,
    int? RetestDays,
    int? MinRemainingShelfLifeDays,
    string? DaysSource,
    IReadOnlyList<string> StorageConditionCodes,
    bool? LotControlled,
    string? LotControlledSource,
    bool? SerialControlled,
    string? Gtin,
    bool? StockableFlag,
    bool Stockable,
    IReadOnlyList<string> Missing,
    IReadOnlyList<string> LockedFields,
    IReadOnlyList<string> AddOnlyFields,
    bool Editable);

public sealed record StockUomConversionDto(string FromUom, string ToUom, string Numerator, string Denominator);

public static class StockBehaviourView
{
    public const string Own = "Own";
    public const string FromGsku = "Gsku";

    // The field names the section, the edit form and the refusals share (the request's JSON names).
    public const string BaseUomCode = "baseUomCode";
    public const string UomConversions = "uomConversions";
    public const string ShelfLifeDays = "shelfLifeDays";
    public const string RetestDays = "retestDays";
    public const string MinRemainingShelfLifeDays = "minRemainingShelfLifeDays";
    public const string StorageConditionCodes = "storageConditionCodes";
    public const string LotControlled = "lotControlled";
    public const string SerialControlled = "serialControlled";
    public const string Stockable = "stockable";
    public const string Gtin = "gtin";
    public const string CaseBoxConversion = "caseBoxConversion";
    public const string GskuStockable = "gsku.stockable";

    public static StockBehaviourDto ForGsku(Gsku gsku, bool heldByOperation = false)
    {
        var missing = new List<string>();
        if (string.IsNullOrEmpty(gsku.BaseUomCode)) missing.Add(BaseUomCode);
        if (gsku.ShelfLifeDays is null) missing.Add(ShelfLifeDays);
        if (gsku.StorageConditionCodes is not { Count: > 0 }) missing.Add(StorageConditionCodes);
        if (gsku.LotControlled is null) missing.Add(LotControlled);
        if (gsku.Stockable != true) missing.Add(Stockable);
        return new(
            gsku.BaseUomCode, Own, Conversions(gsku.UomConversions),
            Conversion(StockBehaviourRules.DerivedPackConversion(gsku.PackQuantity, gsku.PackUomCode)),
            gsku.ShelfLifeDays, gsku.ShelfLifeDays is null ? null : Own,
            gsku.RetestDays, gsku.MinRemainingShelfLifeDays, Own,
            gsku.StorageConditionCodes ?? [], gsku.LotControlled, Own, null, null,
            gsku.Stockable, ProductMasterContract.GskuStockable(gsku), missing,
            Locked(gsku.LifecycleStatus,
                (BaseUomCode, gsku.BaseUomCode is not null), (ShelfLifeDays, gsku.ShelfLifeDays is not null),
                (RetestDays, gsku.RetestDays is not null), (MinRemainingShelfLifeDays, gsku.MinRemainingShelfLifeDays is not null),
                (LotControlled, gsku.LotControlled is not null), (Stockable, gsku.Stockable is not null)),
            AddOnly(gsku.LifecycleStatus, UomConversions, StorageConditionCodes),
            StockBehaviourContext.Editable(gsku.LifecycleStatus) && !heldByOperation);
    }

    // O-3: everything but the serial flag is the GSKU's, read now; the LSKU's own field is only SerialControlled.
    public static StockBehaviourDto ForLsku(Gsku gsku, Lsku lsku, bool heldByOperation = false)
    {
        var parent = ForGsku(gsku);
        var missing = new List<string>();
        if (!parent.Stockable) missing.Add(GskuStockable);
        if (lsku.SerialControlled is null) missing.Add(SerialControlled);
        return parent with
        {
            BaseUomSource = FromGsku,
            ShelfLifeDaysSource = parent.ShelfLifeDays is null ? null : FromGsku,
            DaysSource = FromGsku,
            LotControlledSource = FromGsku,
            SerialControlled = lsku.SerialControlled,
            StockableFlag = null,
            Stockable = ProductMasterContract.LskuStockable(gsku, lsku),
            Missing = missing,
            LockedFields = Locked(lsku.LifecycleStatus, (SerialControlled, lsku.SerialControlled is not null)),
            AddOnlyFields = [],
            Editable = StockBehaviourContext.Editable(lsku.LifecycleStatus) && !heldByOperation
        };
    }

    // O-14: the base unit is the GSKU's; D-2: the shelf life is the FG's own or, when it has none, the GSKU's; the days
    // (retest / minimum remaining) are the GSKU's.
    public static StockBehaviourDto ForFinishedGood(Gsku gsku, FinishedGood finishedGood, bool heldByOperation = false)
    {
        var shelfLife = finishedGood.ShelfLifeDays ?? gsku.ShelfLifeDays;
        var missing = new List<string>();
        if (string.IsNullOrEmpty(gsku.BaseUomCode)) missing.Add("gsku." + BaseUomCode);
        if (shelfLife is null) missing.Add(ShelfLifeDays);
        if (!(finishedGood.UomConversions ?? []).Any(x => IsCaseBox(x))) missing.Add(CaseBoxConversion);
        if (finishedGood.StorageConditionCodes is not { Count: > 0 }) missing.Add(StorageConditionCodes);
        if (finishedGood.LotControlled is null) missing.Add(LotControlled);
        if (finishedGood.SerialControlled is null) missing.Add(SerialControlled);
        if (string.IsNullOrEmpty(finishedGood.Gtin)) missing.Add(Gtin);
        if (finishedGood.Stockable != true) missing.Add(Stockable);
        return new(
            gsku.BaseUomCode, FromGsku, Conversions(finishedGood.UomConversions), null,
            shelfLife, finishedGood.ShelfLifeDays is not null ? Own : shelfLife is null ? null : FromGsku,
            gsku.RetestDays, gsku.MinRemainingShelfLifeDays, FromGsku,
            finishedGood.StorageConditionCodes ?? [], finishedGood.LotControlled, Own, finishedGood.SerialControlled,
            finishedGood.Gtin, finishedGood.Stockable, ProductMasterContract.FinishedGoodStockable(gsku, finishedGood), missing,
            Locked(finishedGood.LifecycleStatus,
                (ShelfLifeDays, finishedGood.ShelfLifeDays is not null), (LotControlled, finishedGood.LotControlled is not null),
                (SerialControlled, finishedGood.SerialControlled is not null), (Stockable, finishedGood.Stockable is not null),
                (Gtin, !string.IsNullOrEmpty(finishedGood.Gtin))),
            AddOnly(finishedGood.LifecycleStatus, UomConversions, StorageConditionCodes),
            StockBehaviourContext.FinishedGoodEditable(finishedGood) && !heldByOperation);
    }

    // O-8: nothing is locked on a Draft; on any other status a filled scalar is.
    private static IReadOnlyList<string> Locked(ProductIdentityLifecycleStatus status, params (string Field, bool Filled)[] fields) =>
        status == ProductIdentityLifecycleStatus.Draft ? [] : fields.Where(x => x.Filled).Select(x => x.Field).ToList();

    // S3 FIX1 M5: on a non-Draft record the lists only gain items.
    private static IReadOnlyList<string> AddOnly(ProductIdentityLifecycleStatus status, params string[] fields) =>
        status == ProductIdentityLifecycleStatus.Draft ? [] : fields;

    private static IReadOnlyList<StockUomConversionDto> Conversions(IEnumerable<ItemUomConversion>? conversions) =>
        (conversions ?? []).Select(Conversion).ToList();

    private static StockUomConversionDto Conversion(ItemUomConversion conversion) =>
        new(conversion.FromUom, conversion.ToUom, conversion.Numerator, conversion.Denominator);

    private static bool IsCaseBox(ItemUomConversion conversion) =>
        conversion.FromUom == StockBehaviourRules.CaseUom && conversion.ToUom == StockBehaviourRules.BoxUom
        || conversion.FromUom == StockBehaviourRules.BoxUom && conversion.ToUom == StockBehaviourRules.CaseUom;
}
