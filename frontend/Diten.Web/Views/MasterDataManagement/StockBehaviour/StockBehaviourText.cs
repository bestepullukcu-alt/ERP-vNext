namespace Diten.Web.Views.MasterDataManagement.StockBehaviour;

/// <summary>
/// MOD-0290-FU04 S5 (O-12) — the words of the "Stock behaviour" section and its edit offcanvas, shared by the GSKU,
/// Finished Good and LSKU screens (Resources/Views/MasterDataManagement/StockBehaviour/StockBehaviourText.*.resx, seven
/// languages). The refusal sentences stay in the GSKU code bridge (GskuRefusals).
///
/// <para>S5b (BL-600): the unit and storage-condition labels by stable code live here too — the item form (Items) and
/// the three stock forms offer the same two lists, so one resx says them once for both (the Items page reads them
/// through _ReferenceOptionLabels.cshtml). Material-type labels stay in ItemsIndex: only the item form offers them.</para>
/// </summary>
public sealed class StockBehaviourText
{
    /// <summary>The units of MOD-0048's item-uom list at S5b. A code arriving later (IU) is shown as its code.</summary>
    public static readonly IReadOnlyList<string> UomCodes =
        ["C62", "MC", "MGM", "GRM", "KGM", "MLT", "LTR", "MTR", "MTK", "XBX", "XCS", "XRO"];

    /// <summary>The storage conditions of MOD-0048's storage-condition list at S5b.</summary>
    public static readonly IReadOnlyList<string> StorageConditionCodes =
        ["AMBIENT_15_25", "COOL_2_8", "FROZEN_BELOW_MINUS_20", "PROTECT_FROM_LIGHT", "PROTECT_FROM_MOISTURE"];

    public static string UomKey(string code) => "Uom_" + code;

    public static string StorageKey(string code) => "Storage_" + code;

    /// <summary>
    /// What a form offering the lists needs: the labels by code, the "lists cannot be loaded" reason, the O-2 refusal
    /// (one temperature condition at most) and the select's empty choice.
    /// </summary>
    public static readonly IReadOnlyList<string> OptionKeys =
    [
        .. UomCodes.Select(UomKey),
        .. StorageConditionCodes.Select(StorageKey),
        "OptionsUnavailable", "TemperatureOnlyOne", "SelectUnit"
    ];

    /// <summary>
    /// S5b-FIX2: what the item form reads (_ReferenceOptionLabels.cshtml) — the option keys plus the Create form's own
    /// reason (no item can be created without the lists); the stock forms never create, so they do not carry it.
    /// </summary>
    public static readonly IReadOnlyList<string> ItemFormOptionKeys = [.. OptionKeys, "OptionsUnavailableCreate"];

    /// <summary>The words the browser module (StockBehaviour/stock-behaviour.js) reads from #stock-behaviour-l10n.</summary>
    public static readonly IReadOnlyList<string> ScriptKeys =
    [
        "FillIn", "Yes", "No", "NotSet", "SourceGsku", "LockedReason", "AddOnlyReason", "NotEditable",
        "BaseUom", "ShelfLife", "StorageConditions", "LotControlled", "SerialControlled", "Gtin", "StockableFlag",
        "MissingCaseBox", "MissingGskuBaseUom", "MissingGskuStockable", "DerivedPackConversion",
        "RemoveConversion", "ConversionFrom", "ConversionTo", "ConversionNumerator", "ConversionDenominator",
        "Save", "SaveConfirmation", "Saved", "NoChanges", "SaveFailed", "StateChanged",
        "InvalidCode", "InvalidDays", "InvalidConversion", "InvalidGtin", "ListCannotBeEmpty",
        .. OptionKeys
    ];
}
