namespace Diten.MdmService.Application.Features.ItemMaster;

// MOD-0290-FU04 §13 / §18: the item's named refusals (the Web bridge to a sentence in seven languages is S5).
public static class ItemErrors
{
    public const string NotFound = "ITEM_NOT_FOUND";
    public const string FieldForbidden = "ITEM_FIELD_FORBIDDEN";
    public const string NameConflict = "ITEM_NAME_CONFLICT";
    public const string ReferenceValueInvalid = "ITEM_REFERENCE_VALUE_INVALID";
    public const string ReferenceListUnavailable = "ITEM_REFERENCE_LIST_UNAVAILABLE";
    public const string StorageConditionConflict = "ITEM_STORAGE_CONDITION_CONFLICT";
    public const string UomConversionInvalid = "UOM_CONVERSION_INVALID";
    public const string StockBehaviourIncomplete = "STOCK_BEHAVIOUR_INCOMPLETE";
    public const string ConcurrencyConflict = "ITEM_CONCURRENCY_CONFLICT";
    public const string CriticalFieldLocked = "ITEM_CRITICAL_FIELD_LOCKED";
    public const string TransitionInvalid = "ITEM_LIFECYCLE_TRANSITION_INVALID";
    // FIX1 M10 (CT decision): a Deactivated item is not edited (§8.1 / §14: only Draft and Active).
    public const string DeactivatedNotEditable = "ITEM_DEACTIVATED_NOT_EDITABLE";
    public const string CreationCommandTombstoned = "CREATION_COMMAND_TOMBSTONED";
    public const string RetireRequiresDeactivation = "ITEM_RETIRE_REQUIRES_DEACTIVATION";
    public const string RetireStockNotZero = "ITEM_RETIRE_STOCK_NOT_ZERO";
    public const string RetireStockCheckUnavailable = "ITEM_RETIRE_STOCK_CHECK_UNAVAILABLE";
    // S1: the activation approval (MOD-0023) is S2. Until then a submit / reactivation request writes nothing — never a
    // PendingActivation nobody owns (§8.4 B).
    public const string ActivationApprovalUnavailable = "ITEM_ACTIVATION_APPROVAL_UNAVAILABLE";
    public const string BindingReconciliationRequired = "ITEM_BINDING_RECONCILIATION_REQUIRED";
    public const string CodeBurnRefused = "ITEM_CODE_BURN_REFUSED";
    public const string StoreUnavailable = "ITEM_STORE_UNAVAILABLE";
    public const string IdempotencyKeyInvalid = "IDEMPOTENCY_KEY_INVALID";
    public const string IdempotencyKeyConflict = "IDEMPOTENCY_KEY_CONFLICT";
}
