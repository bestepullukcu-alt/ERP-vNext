namespace Diten.Web.Views.MasterDataManagement.Items;

/// <summary>The resource class of the item refusal sentences (Resources/Views/MasterDataManagement/Items/ItemRefusals.*.resx).</summary>
public sealed class ItemRefusals;

/// <summary>
/// MOD-0290-FU04 S2 — the item activation gate's refusal (ITEM_STOCK_FIELDS_REQUIRED_FOR_ACTIVATION) and the names of the
/// fields it lists, bound to the sentence the reader sees in seven languages. The item screen itself is S5; the guard test
/// (ItemRefusalBridgeTests) holds MDM's gate code and field names, this map and the seven resx files in lock-step.
/// </summary>
public static class ItemRefusalSentences
{
    public static readonly IReadOnlyDictionary<string, string> Keys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ITEM_STOCK_FIELDS_REQUIRED_FOR_ACTIVATION"] = "RefusalStockFieldsRequired"
    };

    // The refusal's detail entries (MDM's ItemActivationGate.FieldNames) → their label.
    public static readonly IReadOnlyDictionary<string, string> FieldKeys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["lotControlled"] = "FieldLotControlled",
        ["baseUomCode"] = "FieldBaseUom",
        ["storageConditionCodes"] = "FieldStorageConditions",
        ["uomConversions"] = "FieldUomConversions"
    };
}
