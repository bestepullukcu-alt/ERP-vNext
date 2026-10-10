namespace Diten.Web.Views.MasterDataManagement.StockBehaviour;

/// <summary>
/// MOD-0290-FU04 S5 (O-12) — the words of the "Stock behaviour" section and its edit offcanvas, shared by the GSKU,
/// Finished Good and LSKU screens (Resources/Views/MasterDataManagement/StockBehaviour/StockBehaviourText.*.resx, seven
/// languages). The refusal sentences stay in the GSKU code bridge (GskuRefusals).
/// </summary>
public sealed class StockBehaviourText
{
    /// <summary>The words the browser module (StockBehaviour/stock-behaviour.js) reads from #stock-behaviour-l10n.</summary>
    public static readonly IReadOnlyList<string> ScriptKeys =
    [
        "FillIn", "Yes", "No", "NotSet", "SourceGsku", "LockedReason", "AddOnlyReason", "NotEditable",
        "BaseUom", "ShelfLife", "StorageConditions", "LotControlled", "SerialControlled", "Gtin", "StockableFlag",
        "MissingCaseBox", "MissingGskuBaseUom", "MissingGskuStockable", "DerivedPackConversion",
        "RemoveConversion", "ConversionFrom", "ConversionTo", "ConversionNumerator", "ConversionDenominator",
        "Save", "SaveConfirmation", "Saved", "NoChanges", "SaveFailed", "StateChanged",
        "InvalidCode", "InvalidDays", "InvalidConversion", "InvalidGtin", "ListCannotBeEmpty"
    ];
}
