namespace Diten.Web.Models.Warehouse;

/// <summary>
/// The domain words the two S1 screens hand to their scripts (pack §11.4 <c>Warehouse.*</c>): ONE list per screen, read by the screen's
/// <c>_IndexL10n</c> partial (→ <c>window.L10n.Words</c>) and by the resx parity test (every key in all seven languages). A key the script
/// reads that is not here would print its own name; a key here that a language lacks fails the test.
/// </summary>
public static class WarehouseVocabulary
{
    // static initializers run in text order: declared before PutawayKeys, which reads it (DL-6)
    /// <summary><c>WarehouseTaskOrigin</c>'s seven values (r24, son bakış 02 F-3): S1's five + S2's Outbound (pick) and ShortDispatch (putaway).</summary>
    public static readonly string[] Origins = ["Manual", "Receipt", "Production", "Cancellation", "External", "Outbound", "ShortDispatch"];

    private static readonly string[] TaskStatuses = ["Open", "InProgress", "Completed", "Cancelled", "InFlight"];
    private static readonly string[] Flags = ["PostingPending", "PostingPendingLong", "PostingRefused", "PostingIntegrityError"];
    private static readonly string[] Priorities = ["Low", "Normal", "High"];
    private static readonly string[] ConfirmationStatuses = ["Pending", "Posted", "Refused", "IntegrityError"];
    private static readonly string[] Resolutions = ["SupersededByNewConfirmation", "ReturnedToSource", "ResolvedPosted", "ResolvedRefused"];
    private static readonly string[] TaskActions = ["Confirm", "Retry", "Returned", "ResolveIntegrity", "CloseShort", "Cancel", "Claim", "Edit", "Assign", "ApplySuggestion"];
    private static readonly string[] DisabledReasons =
        ["TaskTerminal", "ConfirmationPending", "IntegrityError", "AlreadyAssigned", "HasPostedConfirmations", "SerialControlled", "NoPostedConfirmation",
         "NoPendingConfirmation", "NoRefusedConfirmation", "NoIntegrityError", "StorageConditionsUnknown"];
    private static readonly string[] CandidateReasons = ["LotUnderRecall", "StockHeld", "LotBlocked", "LotExpired", "StorageConditionsUnknown"];
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
        .. PutawayPageWords,
        // DL-6 (pack §11.4 r24): the putaway QuickView shows the task's origin — Cancellation / ShortDispatch put-backs are told apart
        .. Origins.Select(o => "Warehouse.Origin." + o)
    ];

    // ── DL-6 — the S2 screens (pack §11.4 S2 / S3 r20 — 7 languages) ──

    /// <summary>The six visible outbound statuses (§8.18).</summary>
    public static readonly string[] OutboundStatuses = ["Allocated", "Picked", "Packed", "ReadyToShip", "Shipped", "Cancelled"];

    private static readonly string[] PackageStatuses = ["Open", "Closed", "Cancelled"];

    private static readonly string[] PickActions = ["Confirm", "Retry", "Returned", "ResolveIntegrity", "CloseShort", "Claim", "Assign"];

    private static readonly string[] PickDisabledReasons =
        ["TaskTerminal", "ConfirmationPending", "IntegrityError", "AlreadyAssigned", "SerialControlled", "NoPendingConfirmation", "NoRefusedConfirmation", "NoIntegrityError"];

    private static readonly string[] PickSaved = ["Confirmed", "Retried", "Claimed", "Returned", "Resolved", "ClosedShort"];

    private static readonly string[] PickPageWords =
        ["NamesUnavailable", "ValidationQuantity", "ReasonRequired", "AssigneeRequired", "ConfirmationSeq", "ConfirmationAttempts", "ConfirmationLastAttempt",
         "ConfirmationMovement", "ConfirmationBy", "RemainingHint", "SystemActor", "AssignResults", "UnknownUser", "Warehouse.Pick.PackLink"];

    private static readonly string[] OutboundEventKinds =
        ["Created", "AllocationFailed", "Allocated", "Updated", "PickCompleted", "PackageCreated", "PackageUpdated", "PackageClosed", "PackageCancelled", "Packed",
         "Released", "Shipped", "ReservationsReleased", "ReservationReleaseFailed", "Cancelled"];

    private static readonly string[] OutboundActions = ["Edit", "Pack", "Release", "SyncDispatch", "Cancel", "Preview"];

    private static readonly string[] LineFailures = ["InsufficientQuantity", "BinCapacityShort"];

    private static readonly string[] ReleaseStates = ["None", "Pending", "Released"];

    private static readonly string[] OutboundPageWords =
        ["ItemLevelBadge", "NothingPicked", "NoChanges", "ReasonRequired", "SystemActor", "UnknownUser", "Warehouse.Outbound.Disabled.ItemLevelNotPublishable",
         "Warehouse.Outbound.Action.SyncDispatch.Confirm", "ReleaseReasonLabel", "CancelReasonLine", "ShipmentLine", "ShortDispatchLine", "PackageContentLine",
         "AllocSeq", "AllocLot", "AllocFefoRank", "AllocQuantity", "AllocPicked", "AllocPacked", "AllocBins", "AllocRelease",
         "LineOrderLine", "LineProduct", "LineSku", "LineQuantity", "LineLot", "LineReason", "LineRemove", "LineFefoFirst", "LineAllocatable", "LotOption",
         "PreviewAllocation", "AllocationLineFailed", "DeviationNeedsReason", "DeviationOptional", "DeviationBlocked", "SkuUnitUnavailable", "ValidationWarehouse",
         "ValidationShipTo", "ValidationLines", "ValidationLine",
         // WP-0178-E3-FIX-01: the Details dialogs are window.showConfirm now (their words come from the script), the preview names the chosen lot,
         // a Staging refusal points at its field
         "FieldReason", "ReasonHint", "ReleaseReasonsIntro", "Warehouse.Outbound.Action.Cancel.Confirm", "Warehouse.Outbound.Action.Release.Confirm",
         "PreviewChosenLot", "StagingNotAllowed"];

    private static readonly string[] PackingPageWords =
        ["FormTitleCreate", "FormTitleEdit", "NotPicked", "PackedDone", "ValidationContents", "ValidationDimensions", "PackageContentLine", "PackageLeftHint",
         "FieldWeight", "FieldDimensions", "QuantityDecrease", "QuantityIncrease", "Warehouse.Package.Action.Close.Confirm"];

    /// <summary>Every word the pick screen's script reads (→ <c>window.L10n.Words</c>).</summary>
    public static IReadOnlyList<string> PickKeys { get; } =
    [
        .. TaskStatuses.Select(s => "Warehouse.Status." + s),
        .. Flags.Select(f => "Warehouse.Flag." + f),
        .. ConfirmationStatuses.Select(s => "Warehouse.ConfirmationStatus." + s),
        .. Resolutions.Select(r => "Warehouse.Resolution." + r),
        .. PickActions.Select(a => "Warehouse.Action." + a),
        "Warehouse.Action.Returned.Confirm", "Warehouse.Action.ResolveIntegrity.Confirm", "Warehouse.Action.ResolveIntegrity.Reason", "Warehouse.Action.CloseShort.Confirm",
        .. PickDisabledReasons.Select(r => "Warehouse.Disabled." + r),
        .. EventKinds.Select(k => "Warehouse.Event." + k),
        .. AssignOutcomes.Select(o => "Warehouse.BulkOutcome." + o),
        .. PickSaved.Select(s => "Warehouse.Saved." + s),
        .. Origins.Select(o => "Warehouse.Origin." + o),
        .. OutboundStatuses.Select(s => "Warehouse.Outbound.Status." + s),
        .. WarehouseErrorMessages.AllKeys,
        .. WarehouseErrorMessages.S2Keys,
        .. PickPageWords
    ];

    /// <summary>Every word the outbound screens' scripts read — list, form, Details (→ <c>window.L10n.Words</c>).</summary>
    public static IReadOnlyList<string> OutboundKeys { get; } =
    [
        .. OutboundStatuses.Select(s => "Warehouse.Outbound.Status." + s),
        .. OutboundActions.Select(a => "Warehouse.Outbound.Action." + a),
        .. OutboundEventKinds.Select(k => "Warehouse.Outbound.Event." + k),
        .. LineFailures.Select(f => "Warehouse.Outbound.LineFailure." + f),
        .. ReleaseStates.Select(r => "Warehouse.Outbound.ReleaseState." + r),
        .. TaskStatuses.Select(s => "Warehouse.Status." + s),
        .. PackageStatuses.Select(s => "Warehouse.Package.Status." + s),
        .. WarehouseErrorMessages.AllKeys,
        .. WarehouseErrorMessages.S2Keys,
        .. OutboundPageWords
    ];

    /// <summary>Every word the Packing page's script reads (→ <c>window.L10n.Words</c>).</summary>
    public static IReadOnlyList<string> PackingKeys { get; } =
    [
        .. OutboundStatuses.Select(s => "Warehouse.Outbound.Status." + s),
        .. PackageStatuses.Select(s => "Warehouse.Package.Status." + s),
        "Warehouse.Package.Action.Create", "Warehouse.Package.Action.Edit", "Warehouse.Package.Action.Close",
        .. WarehouseErrorMessages.AllKeys,
        .. WarehouseErrorMessages.S2Keys,
        .. PackingPageWords
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

    // ── DL-7 — the S3 monitoring screens (pack §8.23, §11.4 S2 / S3 r20 — 7 languages) ──

    /// <summary>§8.23 — the nine exception kinds of v1 (WMS draft §9 names).</summary>
    public static readonly string[] ExceptionKinds =
    [
        "PostingRefused", "PostingPendingLong", "PostingIntegrityError", "DispatchNotSeen", "ReservationReleasePending", "ShortDispatchUnresolved",
        "AllocationOrphanSuspected", "ReservedLotUnderRecall", "AllocationStuck"
    ];

    /// <summary>§8.23 (r25, son bakış 03 F-1): why a ShortDispatchUnresolved difference was not computed — two closed values.</summary>
    public static readonly string[] DeferredBy = ["PendingConfirmation", "UnsyncedReadyToShip"];

    /// <summary>§8.21 — the explanatory reason of an unresolved short-dispatch row.</summary>
    public static readonly string[] ShortDispatchReasons = ["LotMissing", "NotFromStaging", "OverIssued"];

    /// <summary>§8.23 Activity Trace — the four sources (the <c>kind</c> filter).</summary>
    public static readonly string[] ActivitySources = ["PutawayTask", "PickTask", "Outbound", "Movement"];

    /// <summary>§8.23 Integration Monitor — the four cards (r24, son bakış 02 F-9).</summary>
    public static readonly string[] IntegrationCards = ["EventQueue", "AwaitingDispatch", "LastSync", "PendingConfirmations"];

    /// <summary>The movement types MOD-0178 posts (putaway, pick and put-back are LOCATION_TRANSFERs — pack §8.3, §8.19).</summary>
    public static readonly string[] ActivityMovementTypes = ["LOCATION_TRANSFER"];

    private static readonly string[] ExceptionTaskKinds = ["Putaway", "Pick"];

    private static readonly string[] ExceptionPageWords =
        ["NamesUnavailable", "ActionOpen", "SectionDetails", "SectionStagingDifference", "FieldKind", "FieldRecord", "FieldWarehouse", "FieldOpened", "FieldCode",
         "FieldStatus", "FieldReason", "FieldExpected", "FieldIssued", "FieldSku", "FieldLot", "FieldAllocSeq", "FieldPendingReleases", "FieldReservationKeys",
         "FieldQuantity", "FieldSeq", "FieldReadyAt", "FieldCreatedAt", "FieldOutboundStatus", "FieldDeferredBy", "ColDiffLot", "ColDiffOnHand", "ColDiffBound",
         "ColDiffDifference", "NoLot", "SummaryPosting", "SummaryReady", "SummaryStuck", "SummaryRelease", "SummaryOrphan", "SummaryRecall", "SummaryShort",
         "SummaryDeferred"];

    private static readonly string[] ActivityPageWords =
        ["NamesUnavailable", "ActionOpen", "SectionDetails", "SectionData", "FieldTime", "FieldSource", "FieldEvent", "FieldRecord", "FieldWarehouse", "FieldActor",
         "FieldSeq", "SystemActor", "UnknownUser", "RangeRequired", "RangeTooLong", "RangeOrder", "RangeHint"];

    /// <summary>Every word the exception box's script reads (→ <c>window.L10n.Words</c>).</summary>
    public static IReadOnlyList<string> ExceptionKeys { get; } =
    [
        .. ExceptionKinds.Select(k => "Warehouse.Exception.Kind." + k),
        .. ExceptionKinds.Select(k => "Warehouse.Exception.Hint." + k),
        "Warehouse.Exception.Status.Open",
        .. DeferredBy.Select(d => "Warehouse.Exception.DeferredBy." + d),
        .. ShortDispatchReasons.Select(r => "Warehouse.Exception.Reason." + r),
        .. ExceptionTaskKinds.Select(k => "Warehouse.Exception.TaskKind." + k),
        .. OutboundStatuses.Select(s => "Warehouse.Outbound.Status." + s),
        .. WarehouseErrorMessages.AllKeys,
        .. ExceptionPageWords
    ];

    /// <summary>Every word the activity trace's script reads (→ <c>window.L10n.Words</c>).</summary>
    public static IReadOnlyList<string> ActivityKeys { get; } =
    [
        .. ActivitySources.Select(s => "Warehouse.Activity.Source." + s),
        .. EventKinds.Select(k => "Warehouse.Event." + k),
        .. OutboundEventKinds.Select(k => "Warehouse.Outbound.Event." + k),
        .. ActivityMovementTypes.Select(m => "Warehouse.Activity.Movement." + m),
        .. WarehouseErrorMessages.AllKeys,
        .. ActivityPageWords
    ];

    /// <summary>Every word the integration monitor draws (server-side cards; the script only turns the UTC times into the reader's local time).</summary>
    public static IReadOnlyList<string> IntegrationKeys { get; } =
    [
        .. IntegrationCards.Select(c => "Warehouse.Integration.Card." + c),
        .. IntegrationCards.Select(c => "Warehouse.Integration.Hint." + c),
        "Warehouse.Integration.Pending", "Warehouse.Integration.Delivered", "Warehouse.Integration.OldestPendingAge", "Warehouse.Integration.Count",
        "Warehouse.Integration.OldestReadyAt", "Warehouse.Integration.LastShippedAt", "Warehouse.Integration.Putaway", "Warehouse.Integration.Pick",
        "Warehouse.Integration.Total", "Warehouse.Integration.Never", "Warehouse.Integration.NoneWaiting", "Warehouse.Integration.AgeMinutes",
        "Warehouse.Integration.AgeHours", "Warehouse.Integration.AgeDays", "Warehouse.Integration.AsOf", "Warehouse.Integration.Refresh",
        .. WarehouseErrorMessages.AllKeys
    ];
}
