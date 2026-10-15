namespace Diten.Web.Views.MasterDataManagement.Items;

/// <summary>
/// The resource class of the item ACTIVATION APPROVAL's sentences (Resources/Views/MasterDataManagement/Items/
/// ItemRefusals.*.resx, seven languages) — S2's own file, kept as a second localizer at the S2 lane merge so the approval's
/// sentences sit apart from the item screen's (ItemsIndex) and the two never edit the same resx.
/// </summary>
public sealed class ItemRefusals;

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
        // S5b (BL-600): the item form's lists could not be read (GetItemOptionsHandler's own fallback code).
        ["REFERENCE_PROVIDER_UNAVAILABLE"] = "RefusalOptionsUnavailable",
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

    /// <summary>
    /// S5b (BL-600): what the options read (<c>GET /api/mdm/items/options</c>) can be refused with — the reference
    /// provider's answers MDM passes on (IItemReferenceResolver.EnumerateLatestAsync: the Platform client, the
    /// unavailable provider, the handler's untrusted-answer check). Every one is "the lists cannot be loaded": the form
    /// draws its list fields passive with this sentence, never an empty list and never a code input. A
    /// <c>REFERENCE_*</c> code Platform adds later is said the same way (the client passes Platform's REFERENCE_ codes
    /// on). ItemRefusalBridgeTests reads these codes from MDM's source.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> OptionsKeys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["REFERENCE_PROVIDER_UNAVAILABLE"] = "RefusalOptionsUnavailable",
        ["REFERENCE_PROVIDER_TIMEOUT"] = "RefusalOptionsTimeout",
        ["REFERENCE_PROVIDER_CONFIGURATION_INVALID"] = "RefusalOptionsUnavailable",
        ["REFERENCE_RESOLUTION_CONTRACT_INVALID"] = "RefusalOptionsUnavailable",
        ["REFERENCE_BATCH_LIMIT_EXCEEDED"] = "RefusalOptionsUnavailable",
        ["REFERENCE_VALUE_NOT_FOUND"] = "RefusalOptionsUnavailable",
        ["REFERENCE_FORBIDDEN"] = "RefusalOptionsUnavailable",
        ["REFERENCE_UNAUTHENTICATED"] = "ErrorUnauthorized",
        ["ITEM_REFERENCE_PROVIDER_NOT_AVAILABLE"] = "RefusalOptionsUnavailable",
        ["UNTRUSTED_ANSWER"] = "RefusalOptionsUnavailable",
        // S2 FIX1: the Platform client's refusal of MDM's service identity (the worker's read); should it ever reach the
        // options read, it is "the lists cannot be loaded" like every other provider answer.
        ["REFERENCE_SERVICE_IDENTITY_REFUSED"] = "RefusalOptionsUnavailable"
    };

    public const string OptionsCodePrefix = "REFERENCE_";

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

    /// <summary>
    /// MOD-0290-FU04 S2 — the activation approval's answers (submit, reactivation request, withdrawal, the guarded writes
    /// while a submit is in flight, the operator's orphan-code repair) → their sentence in <see cref="ItemRefusals"/>'s
    /// resx (the second localizer; seven languages). ItemRefusalBridgeTests reads these codes from MDM's source.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ApprovalKeys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        // Who may act on the approval.
        ["ITEM_ACTIVATION_FORBIDDEN"] = "RefusalApprovalForbidden",
        ["ITEM_ACTIVATION_WITHDRAWAL_NOT_REQUESTER"] = "RefusalApprovalNotRequester",
        // The approval moved on under the request: reload and act again.
        ["ITEM_ACTIVATION_SUBMIT_ABANDONED"] = "RefusalApprovalChanged",
        ["ITEM_ACTIVATION_DECISION_NOT_APPLICABLE"] = "RefusalApprovalChanged",
        ["ITEM_ACTIVATION_SUBMIT_TARGET_CHANGED"] = "RefusalApprovalChanged",
        ["ITEM_ACTIVATION_STATE_CONFLICT"] = "RefusalApprovalChanged",
        ["ITEM_ACTIVATION_WORKFLOW_STATE_CONFLICT"] = "RefusalApprovalChanged",
        ["ITEM_ACTIVATION_WORKFLOW_BINDING_CONFLICT"] = "RefusalApprovalChanged",
        ["ITEM_ACTIVATION_WITHDRAWAL_OPERATION_CONFLICT"] = "RefusalApprovalChanged",
        ["ITEM_ACTIVATION_WORKFLOW_OPERATION_CONFLICT"] = "RefusalApprovalChanged",
        ["ITEM_ACTIVATION_WORKFLOW_CONCURRENCY_CONFLICT"] = "RefusalApprovalChanged",
        ["ITEM_ACTIVATION_CLOSED_WITHOUT_DECISION"] = "RefusalApprovalChanged",
        ["ITEM_ACTIVATION_RECONCILED_NOTHING_STARTED"] = "RefusalApprovalChanged",
        // Being processed: the same request, later.
        ["ITEM_ACTIVATION_WORKFLOW_BUSY"] = "RefusalApprovalBusy",
        ["ITEM_ACTIVATION_WITHDRAWAL_UNDER_WAY"] = "RefusalApprovalBusy",
        ["ITEM_ACTIVATION_SUBMIT_IN_FLIGHT"] = "RefusalApprovalSubmitInFlight",
        ["ITEM_ACTIVATION_WORKFLOW_IDEMPOTENCY_CONFLICT"] = "RefusalApprovalRepeatDiffers",
        ["ITEM_ACTIVATION_WITHDRAWAL_INVALID"] = "RefusalApprovalWithdrawalInvalid",
        ["ITEM_ACTIVATION_WITHDRAWAL_REFUSED"] = "RefusalApprovalWithdrawalRefused",
        // The approval engine cannot be reached now (503).
        ["ITEM_ACTIVATION_WORKFLOW_STEP_BUDGET_EXCEEDED"] = "RefusalApprovalUnavailable",
        ["ITEM_ACTIVATION_WORKFLOW_RETRY_LIMIT_REACHED"] = "RefusalApprovalUnavailable",
        ["ITEM_ACTIVATION_WORKFLOW_PROVIDER_UNAVAILABLE"] = "RefusalApprovalUnavailable",
        ["ITEM_ACTIVATION_WORKFLOW_RETRY_SCHEDULED"] = "RefusalApprovalUnavailable",
        ["ITEM_ACTIVATION_WORKFLOW_START_FAILED"] = "RefusalApprovalUnavailable",
        ["ITEM_ACTIVATION_WITHDRAWAL_FAILED"] = "RefusalApprovalUnavailable",
        // The approval and the item disagree: nobody at the screen can fix it, an administrator checks.
        ["ITEM_ACTIVATION_WORKFLOW_MANUAL_RECONCILIATION_NEEDED"] = "RefusalApprovalNeedsCheck",
        ["ITEM_ACTIVATION_WORKFLOW_RECONCILIATION_REQUIRED"] = "RefusalApprovalNeedsCheck",
        ["ITEM_ACTIVATION_WORKFLOW_START_PROOF_INVALID"] = "RefusalApprovalNeedsCheck",
        ["ITEM_ACTIVATION_WORKFLOW_LOCAL_PENDING_INCONSISTENT"] = "RefusalApprovalNeedsCheck",
        ["ITEM_ACTIVATION_WORKFLOW_EVIDENCE_CONFLICT"] = "RefusalApprovalNeedsCheck",
        ["ITEM_ACTIVATION_WORKFLOW_APPROVAL_PROOF_INVALID"] = "RefusalApprovalNeedsCheck",
        ["ITEM_ACTIVATION_WORKFLOW_DECISION_INCONSISTENT"] = "RefusalApprovalNeedsCheck",
        ["ITEM_ACTIVATION_DECISION_CONTRACT_INVALID"] = "RefusalApprovalNeedsCheck",
        ["ITEM_ACTIVATION_WORKFLOW_START_INVALID"] = "RefusalApprovalNeedsCheck",
        ["ITEM_ACTIVATION_WORKFLOW_CONFIGURATION_INVALID"] = "RefusalApprovalNeedsCheck",
        ["ITEM_ACTIVATION_WORKFLOW_START_FACTS_INVALID"] = "RefusalApprovalNeedsCheck",
        ["ITEM_ACTIVATION_WORKFLOW_OPERATION_LOST"] = "RefusalApprovalNeedsCheck",
        // S2 lane merge FIX1: the approval store's own contract checks (ItemWorkflowOperationRepository), passed on by the
        // processor's Fail(…, 409).
        ["ITEM_ACTIVATION_WORKFLOW_OPERATION_INVALID"] = "RefusalApprovalNeedsCheck",
        ["ITEM_ACTIVATION_LOCAL_PENDING_CONTRACT_INVALID"] = "RefusalApprovalNeedsCheck",
        ["ITEM_ACTIVATION_WITHDRAWAL_CONTRACT_INVALID"] = "RefusalApprovalNeedsCheck",
        ["ITEM_ACTIVATION_END_CONTRACT_INVALID"] = "RefusalApprovalNeedsCheck",
        // S2 lane merge FIX1: the product-identity family's maker codes (ProductIdentityWorkflowMakerCodes.IsMakerCode) —
        // the engine sent the start back to the person who is present (401 / 403 / 409 / 503). The first-setup faults
        // (no approver on the position, a template not published, an expired session) each say what to do; the sentences
        // are GSKU's own (GskuRefusals), copied per language.
        ["PRODUCT_IDENTITY_WORKFLOW_SESSION_EXPIRED"] = "RefusalApprovalSessionExpired",
        ["PRODUCT_IDENTITY_WORKFLOW_START_PERMISSION_REQUIRED"] = "RefusalApprovalForbidden",
        ["PRODUCT_IDENTITY_WORKFLOW_APPROVER_NOT_ASSIGNED"] = "RefusalApprovalSetupIncomplete",
        ["PRODUCT_IDENTITY_WORKFLOW_DEFINITION_NOT_READY"] = "RefusalApprovalSetupIncomplete",
        ["PRODUCT_IDENTITY_WORKFLOW_SERVICE_UNAVAILABLE_RESUBMIT"] = "RefusalApprovalResubmit",
        ["PRODUCT_IDENTITY_WORKFLOW_MAKER_REPLAY_REQUIRED"] = "RefusalApprovalResubmit",
        ["PRODUCT_IDENTITY_WORKFLOW_APPROVAL_CHANGING"] = "RefusalApprovalBusy",
        // WP-MDM-NO-ELIGIBLE-APPROVER-01: nobody but the maker may approve (segregation of duties).
        ["PRODUCT_IDENTITY_WORKFLOW_NO_ELIGIBLE_APPROVER"] = "RefusalApprovalNoEligibleApprover",
        ["ITEM_ACTIVATION_WORKFLOW_START_REFUSED"] = "RefusalApprovalStartRefused",
        ["ITEM_ACTIVATION_SELF_APPROVAL_IGNORED"] = "RefusalApprovalSelf",
        // The submit freezes the item lists (S2 FIX1): a value went away or changed, or the lists cannot be checked.
        ["ITEM_REFERENCE_VALUE_REMOVED"] = "RefusalReferenceValueChanged",
        ["ITEM_REFERENCE_VALUE_CHANGED"] = "RefusalReferenceValueChanged",
        ["ITEM_REFERENCE_CONTRACT_CONFLICT"] = "RefusalReferenceCheckUnavailable",
        ["ITEM_REFERENCE_PROVIDER_CONFIGURATION_ERROR"] = "RefusalReferenceCheckUnavailable",
        // The operator repair (POST orphan-code-repairs).
        ["ITEM_ORPHAN_CODE_REPAIR_INVALID"] = "RefusalOrphanRepairInvalid",
        // The activation gate (R-04): its field names follow the code and are said with FieldKeys.
        ["ITEM_STOCK_FIELDS_REQUIRED_FOR_ACTIVATION"] = "RefusalStockFieldsRequired"
    };

    /// <summary>The activation gate's field names (MDM's ItemActivationGate.FieldNames, after its code) → their label.</summary>
    public static readonly IReadOnlyDictionary<string, string> FieldKeys = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["lotControlled"] = "FieldLotControlled",
        ["baseUomCode"] = "FieldBaseUom",
        ["storageConditionCodes"] = "FieldStorageConditions",
        ["uomConversions"] = "FieldUomConversions"
    };

    /// <summary>
    /// Upper-case literals of MDM's item code that never reach a reader, each with its reason (the S2 code table): the
    /// bridge test accepts them without a sentence, and fails on any MDM stops writing.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> InternalCodes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["ITEM_ACTIVATION"] = "the flow name (metrics, logs), never an answer",
        ["ITEM_ACTIVATION_WORKFLOW_DECISION_PENDING"] = "the worker's state between polls, never a body",
        ["ITEM_ACTIVATION_WORKFLOW_MAKER_REPLAY_REQUIRED"] = "the operation's state; HTTP carries PRODUCT_IDENTITY_WORKFLOW_MAKER_REPLAY_REQUIRED (409, mapped)",
        ["ITEM_ACTIVATION_WORKFLOW_STUCK_ON_RECORD_ANSWER"] = "the worker's quarantine reason, read by the operator",
        ["ITEM_ACTIVATION_WITHDRAWAL_EVIDENCE_INVALID"] = "the worker's check of the engine's withdrawal answer",
        ["ITEM_ACTIVATION_WITHDRAWN_BY_MAKER"] = "the operation's end reason, written to its record",
        ["ITEM_ORPHAN_CODES_BURNED"] = "the operator repair's audit outcome (200)",
        ["ITEM_ORPHAN_CODES_NONE_BURNED"] = "the operator repair's audit outcome (200)",
        ["ITEM_ORPHAN_CODE_REPAIRED"] = "the operator repair's audit operation",
        ["ITEM_VERSION_CONFLICT"] = "always answered as ITEM_CONCURRENCY_CONFLICT"
    };
}
