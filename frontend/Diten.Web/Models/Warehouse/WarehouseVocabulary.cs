namespace Diten.Web.Models.Warehouse;

/// <summary>
/// The domain words the two S1 screens hand to their scripts (pack §11.4 <c>Warehouse.*</c>): ONE list per screen, read by the screen's
/// <c>_IndexL10n</c> partial (→ <c>window.L10n.Words</c>) and by the resx parity test (every key in all seven languages). A key the script
/// reads that is not here would print its own name; a key here that a language lacks fails the test.
/// </summary>
public static class WarehouseVocabulary
{
    private static readonly string[] TaskStatuses = ["Open", "InProgress", "Completed", "Cancelled", "InFlight"];
    private static readonly string[] Flags = ["PostingPending", "PostingPendingLong", "PostingRefused", "PostingIntegrityError"];
    private static readonly string[] Priorities = ["Low", "Normal", "High"];
    private static readonly string[] ConfirmationStatuses = ["Pending", "Posted", "Refused", "IntegrityError"];
    private static readonly string[] Resolutions = ["SupersededByNewConfirmation", "ReturnedToSource", "ResolvedPosted", "ResolvedRefused"];
    private static readonly string[] TaskActions = ["Confirm", "Retry", "Returned", "ResolveIntegrity", "CloseShort", "Cancel", "Claim", "Edit", "Assign", "ApplySuggestion"];
    private static readonly string[] DisabledReasons =
        ["TaskTerminal", "ConfirmationPending", "IntegrityError", "AlreadyAssigned", "HasPostedConfirmations", "SerialControlled", "NoPostedConfirmation",
         "NoPendingConfirmation", "NoRefusedConfirmation", "NoIntegrityError"];
    private static readonly string[] CandidateReasons = ["LotUnderRecall", "StockHeld", "LotBlocked", "LotExpired"];
    private static readonly string[] BlockingKinds = ["RecallCase", "QuarantineHold", "BlockedStock", "LotStatus"];
    private static readonly string[] EventKinds =
        ["Created", "Updated", "Assigned", "Claimed", "SuggestionApplied", "ConfirmationRecorded", "ConfirmationPosted", "ConfirmationRefused", "ConfirmationRetried",
         "ConfirmationReconciled", "ConfirmationIntegrityError", "IntegrityResolved", "ConfirmationDeviation", "ConfirmationSuperseded", "ConfirmationReturned",
         "ClosedShort", "Completed", "Cancelled"];
    private static readonly string[] AssignOutcomes = ["Assigned", "Conflict", "NotFound", "InvalidTransition", "PendingConfirmation"];
    private static readonly string[] TaskSaved = ["Created", "Updated", "Confirmed", "Retried", "Claimed", "Returned", "Resolved", "ClosedShort", "Cancelled"];

    /// <summary>The reason-dialog texts of the four reasoned actions (pack §11.4 + inceleme 03).</summary>
    private static readonly string[] ReasonTexts =
        ["Warehouse.Action.Returned.Confirm", "Warehouse.Action.ResolveIntegrity.Confirm", "Warehouse.Action.ResolveIntegrity.Reason",
         "Warehouse.Action.CloseShort.Confirm", "Warehouse.Action.Cancel.Confirm"];

    /// <summary>The page words index.js reads besides the <c>Warehouse.*</c> vocabulary.</summary>
    private static readonly string[] PutawayPageWords =
        ["FormTitleCreate", "FormTitleEdit", "NamesUnavailable", "ValidationQuantity", "ValidationSource", "BinNotFound", "BinLookupUnavailable",
         "DeviationRequired", "ReasonRequired", "AssigneeRequired", "NoChanges", "ConfirmationSeq", "ConfirmationAttempts", "ConfirmationLastAttempt",
         "ConfirmationMovement", "ConfirmationBy", "RemainingHint", "SystemActor", "AssignResults", "LockedSource", "BlockingDocument", "SourceOption",
         "AddNewPutawayTask", "UnknownUser"];

    private static readonly string[] Roles = ["Receiving", "Staging"];
    private static readonly string[] StorageConditions = ["AMBIENT_15_25", "COOL_2_8", "FROZEN_BELOW_MINUS_20", "PROTECT_FROM_LIGHT", "PROTECT_FROM_MOISTURE"];
    private static readonly string[] LocationTypes = ["Warehouse", "StorageLocation", "Zone", "Bin"];
    private static readonly string[] RoleActions = ["EditRole", "DeleteRole", "BulkDelete"];
    private static readonly string[] RoleSaved = ["RoleCreated", "RoleUpdated", "RoleDeleted"];
    private static readonly string[] DeleteOutcomes = ["Deleted", "Conflict", "NotFound"];
    private static readonly string[] RolePageWords =
        ["FormTitleCreate", "FormTitleEdit", "ValidationLocation", "DeleteConfirm", "BulkDeleteConfirm", "BulkDeleteResult", "InheritedBadge", "InheritedFrom",
         "NoConditions", "NoRoles", "AddNewWarehouseLocationRole"];

    /// <summary>Every word the putaway screen's script reads (→ <c>window.L10n.Words</c>).</summary>
    public static IReadOnlyList<string> PutawayKeys { get; } =
    [
        .. TaskStatuses.Select(s => "Warehouse.Status." + s),
        .. Flags.Select(f => "Warehouse.Flag." + f),
        .. Priorities.Select(p => "Warehouse.Priority." + p),
        .. ConfirmationStatuses.Select(s => "Warehouse.ConfirmationStatus." + s),
        .. Resolutions.Select(r => "Warehouse.Resolution." + r),
        .. TaskActions.Select(a => "Warehouse.Action." + a),
        .. ReasonTexts,
        .. DisabledReasons.Select(r => "Warehouse.Disabled." + r),
        .. CandidateReasons.Select(r => "Warehouse.Candidate." + r),
        .. BlockingKinds.Select(k => "Warehouse.Blocking." + k),
        .. EventKinds.Select(k => "Warehouse.Event." + k),
        .. AssignOutcomes.Select(o => "Warehouse.BulkOutcome." + o),
        .. TaskSaved.Select(s => "Warehouse.Saved." + s),
        .. WarehouseErrorMessages.AllKeys,
        .. PutawayPageWords
    ];

    /// <summary>Every word the location-role screen's script reads (→ <c>window.L10n.Words</c>).</summary>
    public static IReadOnlyList<string> RoleKeys { get; } =
    [
        .. Roles.Select(r => "Warehouse.Role." + r),
        .. StorageConditions.Select(c => "Warehouse.StorageCondition." + c),
        .. LocationTypes.Select(t => "Warehouse.LocationType." + t),
        .. RoleActions.Select(a => "Warehouse.Action." + a),
        .. RoleSaved.Select(s => "Warehouse.Saved." + s),
        .. DeleteOutcomes.Select(o => "Warehouse.BulkOutcome." + o),
        .. WarehouseErrorMessages.AllKeys,
        .. RolePageWords
    ];
}
