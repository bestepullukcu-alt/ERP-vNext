namespace Diten.InventoryService.Domain.Warehouse.Enums;

// MOD-0178 task enums (pack §4.1–§4.3; WMS draft §3.1–§3.4). Stored by name (InventoryBsonMappings' Domain convention).

public enum WarehouseTaskKind
{
    Putaway,
    Pick
}

/// <summary>Derived from the confirmed quantity (§8.3) — Open → InProgress → Completed; Completed / Cancelled are terminal.</summary>
public enum WarehouseTaskStatus
{
    Open,
    InProgress,
    Completed,
    Cancelled
}

public enum WarehouseTaskOrigin
{
    Manual,
    Receipt,
    Production,
    Cancellation,
    External
}

public enum WarehouseTargetSource
{
    Manual,
    SuggestionApplied
}

public enum WarehouseTaskPriority
{
    Low,
    Normal,
    High
}

public enum ConfirmationStatus
{
    Pending,
    Posted,
    Refused,
    IntegrityError
}

/// <summary>The operator's closure of a Refused confirmation (§4.2 <c>Resolution</c>).</summary>
public enum ConfirmationResolution
{
    SupersededByNewConfirmation,
    ReturnedToSource
}

public enum ConfirmationIntegrityResolution
{
    ResolvedOwnMovement,
    ReturnedToRefused
}

/// <summary>Where a Refused came from (§4.2; independent review 06 F-2) — the defensive sweep reads it, never the error code.</summary>
public enum RefusalSource
{
    PreCheck,
    Ledger4xx,
    Resolve
}

public enum ConfirmationChannel
{
    Ui,
    Api,
    External
}

/// <summary>The task's one badge (§4.1, priority IntegrityError &gt; PendingLong &gt; Pending &gt; Refused &gt; None).</summary>
public enum IntegrationFlag
{
    None,
    PostingPending,
    PostingPendingLong,
    PostingRefused,
    PostingIntegrityError
}

/// <summary>§4.3 — the kinds of the insert-only task timeline <c>wms_task_events</c>.</summary>
public enum WarehouseTaskEventKind
{
    Created,
    Updated,
    Assigned,
    Claimed,
    SuggestionApplied,
    ConfirmationRecorded,
    ConfirmationPosted,
    ConfirmationRefused,
    ConfirmationRetried,
    ConfirmationReconciled,
    ConfirmationIntegrityError,
    IntegrityResolved,
    ConfirmationDeviation,
    ConfirmationSuperseded,
    ConfirmationReturned,
    ClosedShort,
    Completed,
    Cancelled
}
