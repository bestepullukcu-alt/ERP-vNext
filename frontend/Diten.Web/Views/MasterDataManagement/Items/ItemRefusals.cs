namespace Diten.Web.Views.MasterDataManagement.Items;

/// <summary>
/// MOD-0290-FU04 S5 (§18): every code an item endpoint of MDM can answer with, bound to the sentence the reader sees
/// (ItemsIndex resx, seven languages). A code in none of these lists is said with the status's general sentence, logged,
/// and flagged for the browser console (it is a missing mapping). ItemRefusalBridgeTests holds MDM's item code, this map
/// and the seven resx files in lock-step.
/// </summary>
public static class ItemRefusalSentences
{
    public static readonly IReadOnlyDictionary<string, string> Keys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // The request's own fields (MDM's validators, 400).
        ["ITEM_NAME_INVALID"] = "RefusalNameInvalid",
        ["ITEM_DESCRIPTION_INVALID"] = "RefusalDescriptionInvalid",
        ["ITEM_MATERIAL_TYPE_REQUIRED"] = "RefusalMaterialTypeRequired",
        ["ITEM_MATERIAL_TYPE_INVALID"] = "RefusalMaterialTypeInvalid",
        ["ITEM_BASE_UOM_REQUIRED"] = "RefusalBaseUomRequired",
        ["ITEM_BASE_UOM_INVALID"] = "RefusalBaseUomInvalid",
        ["ITEM_LOT_CONTROLLED_REQUIRED"] = "RefusalFlagsRequired",
        ["ITEM_STOCKABLE_REQUIRED"] = "RefusalFlagsRequired",
        ["ITEM_SHELF_LIFE_INVALID"] = "RefusalShelfLifeInvalid",
        ["ITEM_RETEST_INVALID"] = "RefusalRetestInvalid",
        ["ITEM_MIN_REMAINING_SHELF_LIFE_INVALID"] = "RefusalMinRemainingInvalid",
        ["ITEM_STORAGE_CONDITION_INVALID"] = "RefusalStorageInvalid",
        ["ITEM_REASON_REQUIRED"] = "RefusalReasonRequired",
        ["ITEM_FIELD_FORBIDDEN"] = "RefusalInvalidRequest",
        ["UNKNOWN_WRITE_FIELD_FORBIDDEN"] = "RefusalInvalidRequest",
        ["ITEM_REQUEST_REQUIRED"] = "RefusalInvalidRequest",
        ["EXPECTED_VERSION_REQUIRED"] = "RefusalInvalidRequest",
        ["IDEMPOTENCY_KEY_INVALID"] = "RefusalInvalidRequest",
        ["INVALID_PARAMETER"] = "RefusalListParameter",
        // The item's rules (404 / 409 / 422).
        ["ITEM_NOT_FOUND"] = "RefusalNotFound",
        ["ITEM_NAME_CONFLICT"] = "RefusalNameConflict",
        ["ITEM_REFERENCE_VALUE_INVALID"] = "RefusalReferenceValueInvalid",
        ["ITEM_STORAGE_CONDITION_CONFLICT"] = "RefusalStorageConflict",
        ["UOM_CONVERSION_INVALID"] = "RefusalUomConversion",
        ["STOCK_BEHAVIOUR_INCOMPLETE"] = "RefusalStockBehaviourIncomplete",
        ["ITEM_CONCURRENCY_CONFLICT"] = "RefusalChangedElsewhere",
        ["ITEM_CRITICAL_FIELD_LOCKED"] = "RefusalCriticalFieldLocked",
        ["ITEM_LIFECYCLE_TRANSITION_INVALID"] = "RefusalTransitionInvalid",
        ["ITEM_DEACTIVATED_NOT_EDITABLE"] = "RefusalDeactivatedNotEditable",
        ["ITEM_RETIRE_REQUIRES_DEACTIVATION"] = "RefusalRetireRequiresDeactivation",
        ["ITEM_RETIRE_STOCK_NOT_ZERO"] = "RefusalRetireStockNotZero",
        // The same key again with other facts, or a spent key: the form is started again.
        ["CREATION_COMMAND_TOMBSTONED"] = "RefusalRepeatDiffers",
        ["IDEMPOTENCY_KEY_CONFLICT"] = "RefusalRepeatDiffers",
        ["RESERVATION_IDEMPOTENCY_KEY_TOMBSTONED"] = "RefusalRepeatDiffers",
        // A service the action needs (503).
        ["ITEM_REFERENCE_LIST_UNAVAILABLE"] = "RefusalReferenceListUnavailable",
        ["ITEM_RETIRE_STOCK_CHECK_UNAVAILABLE"] = "RefusalRetireStockCheckUnavailable",
        ["ITEM_ACTIVATION_APPROVAL_UNAVAILABLE"] = "RefusalActivationApprovalUnavailable",
        ["ITEM_STORE_UNAVAILABLE"] = "RefusalServiceUnavailable",
        ["ITEM_WRITE_OUTCOME_AMBIGUOUS"] = "RefusalServiceUnavailable",
        ["CODE_RESERVATION_BURN_TRANSACTION_UNAVAILABLE"] = "RefusalServiceUnavailable",
        ["AUDIT_INTENT_CAPACITY_EXCEEDED"] = "RefusalServiceUnavailable",
        // The creation is written, its code still being bound (202): the same form, saved again, finishes it.
        ["ITEM_BINDING_RECONCILIATION_REQUIRED"] = "RefusalCreatePending",
        // The code ledger and the store disagreed with this attempt; a fresh attempt is the way on.
        ["CODE_RESERVATION_REQUIRED"] = "RefusalTryAgain",
        ["CODE_RESERVATION_BURNED"] = "RefusalTryAgain",
        ["CODE_RESERVATION_MISMATCH"] = "RefusalTryAgain",
        ["CODE_RESERVATION_ALREADY_TERMINAL"] = "RefusalTryAgain",
        ["IDENTITY_RESERVATION_CONFLICT"] = "RefusalTryAgain",
        ["CONCURRENCY_CONFLICT"] = "RefusalTryAgain",
        ["ITEM_DUPLICATE_CONFLICT"] = "RefusalTryAgain",
        ["ITEM_INSERT_CONFLICT_RETRY"] = "RefusalTryAgain",
        // Internal disagreements nobody at the screen can fix.
        ["ITEM_CODE_BURN_REFUSED"] = "RefusalInternal",
        ["ITEM_CONTRACT_INVALID"] = "RefusalInternal",
        ["ITEM_BINDING_INVARIANT_VIOLATION"] = "RefusalInternal",
        ["ITEM_WRITE_FAILED"] = "RefusalInternal"
    };

    /// <summary>
    /// The code-reservation ledger's answers an item creation passes on (consume / confirm / burn, ICodeReservationRepository
    /// shared by every code-bearing kind). They live outside the item files, so the bridge test reads them from the ledger.
    /// </summary>
    public static readonly IReadOnlyList<string> CodeReservationCodes =
    [
        "CODE_RESERVATION_REQUIRED", "CODE_RESERVATION_MISMATCH", "CODE_RESERVATION_ALREADY_TERMINAL",
        "IDENTITY_RESERVATION_CONFLICT", "CONCURRENCY_CONFLICT", "AUDIT_INTENT_CAPACITY_EXCEEDED",
        "CODE_RESERVATION_BURN_TRANSACTION_UNAVAILABLE"
    ];

    /// <summary>
    /// Codes MDM answers as <c>[code, detail]</c>: the ONE item after them is their detail, whatever its shape
    /// (SAME_UOM looks like a code; "item-uom:XYZ" does not). A known detail picks its own sentence; any other detail
    /// leaves the code's sentence.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> DetailBearing =
        new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal)
        {
            ["UOM_CONVERSION_INVALID"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["UOM_REQUIRED"] = "RefusalUomConversionUnitMissing",
                ["SAME_UOM"] = "RefusalUomConversionSameUnit",
                ["FACTOR_NOT_A_POSITIVE_DECIMAL"] = "RefusalUomConversionFactor",
                ["PAIR_REPEATED"] = "RefusalUomConversionPairRepeated"
            },
            ["ITEM_STORAGE_CONDITION_CONFLICT"] = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["MORE_THAN_ONE_TEMPERATURE"] = "RefusalStorageTwoTemperatures",
                ["REPEATED"] = "RefusalStorageRepeated"
            },
            // The provider's / stock service's cause is logged by MDM; the reader gets the code's sentence.
            ["ITEM_REFERENCE_LIST_UNAVAILABLE"] = new Dictionary<string, string>(StringComparer.Ordinal),
            ["ITEM_RETIRE_STOCK_CHECK_UNAVAILABLE"] = new Dictionary<string, string>(StringComparer.Ordinal),
            // "set:value[:NOT_SELECTABLE],…" and the locked field's name are read by the controller (named sentences).
            ["ITEM_REFERENCE_VALUE_INVALID"] = new Dictionary<string, string>(StringComparer.Ordinal),
            ["ITEM_CRITICAL_FIELD_LOCKED"] = new Dictionary<string, string>(StringComparer.Ordinal)
        };

    /// <summary>The details MDM's item code writes after a detail-bearing code (scanned with it; never a code).</summary>
    public static readonly IReadOnlySet<string> KnownDetails = new HashSet<string>(StringComparer.Ordinal)
    {
        "UOM_REQUIRED", "SAME_UOM", "FACTOR_NOT_A_POSITIVE_DECIMAL", "PAIR_REPEATED", "MORE_THAN_ONE_TEMPERATURE",
        "UNTRUSTED_ANSWER", "ANSWER_INCOMPLETE", "STOCK_CHECK_UNAVAILABLE", "STOCK_CHECK_BUDGET_EXHAUSTED",
        "STOCK_CHECK_FAILED", "STOCK_CHECK_NO_ANSWER", "INVENTORY_TENANT_WIDE_QUERY_NOT_AVAILABLE"
    };

    /// <summary>The six material types (MDM ItemRules, pack §1) — a closed set; labels by stable code (O-22).</summary>
    public static readonly IReadOnlyList<string> MaterialTypes =
    [
        "ACTIVE_INGREDIENT", "EXCIPIENT", "RAW_MATERIAL", "PACKAGING_PRIMARY", "PACKAGING_SECONDARY", "INTERMEDIATE"
    ];

    public static string MaterialTypeKey(string code) => "MaterialType_" + code;

    /// <summary>MDM's ItemLifecycleStatus (numeric on the wire) → its name; the name is the filter value and the label key.</summary>
    public static readonly IReadOnlyDictionary<int, string> LifecycleStatuses = new Dictionary<int, string>
    {
        [1] = "Draft", [2] = "PendingActivation", [3] = "Active", [4] = "Deactivated", [5] = "Retired"
    };

    public static string LifecycleKey(string name) => "Lifecycle" + name;

    /// <summary>The locked fields ItemRules.LockedFieldChanged can name → their label keys.</summary>
    public static readonly IReadOnlyDictionary<string, string> LockedFieldLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["MaterialType"] = "MaterialType",
        ["BaseUomCode"] = "BaseUomCode",
        ["LotControlled"] = "LotControlled",
        ["Stockable"] = "Stockable",
        ["ShelfLifeDays"] = "ShelfLifeDays",
        ["RetestDays"] = "RetestDays",
        ["MinRemainingShelfLifeDays"] = "MinRemainingShelfLifeDays",
        ["StorageConditionCodes"] = "StorageConditionCodes",
        ["UomConversions"] = "UomConversions"
    };

    /// <summary>The MOD-0048 item lists' set codes → their label keys (ITEM_REFERENCE_VALUE_INVALID's detail).</summary>
    public static readonly IReadOnlyDictionary<string, string> ReferenceSetLabels = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["material-type"] = "MaterialType",
        ["item-uom"] = "UnitCode",
        ["storage-condition"] = "StorageConditionCodes"
    };
}
