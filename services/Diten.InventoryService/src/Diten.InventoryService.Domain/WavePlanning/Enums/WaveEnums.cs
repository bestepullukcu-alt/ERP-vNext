namespace Diten.InventoryService.Domain.WavePlanning.Enums;

/// <summary>MOD-0180 wave type (pack §4.1): a classification and a filter default only — it changes no business rule.</summary>
public enum WaveType
{
    Cargo,
    Customer,
    Region,
    Manual
}

/// <summary>The wave's state machine (pack §8.3). <see cref="Releasing"/> is internal (DL-2). DL-1 writes Draft and Cancelled only.</summary>
public enum WaveStatus
{
    Draft,
    Releasing,
    Released,
    Picking,
    Completed,
    PartiallyCompleted,
    Cancelled
}

/// <summary>
/// A wave membership's state (pack §4.2). DL-1 writes <see cref="Planned"/> and <see cref="Removed"/>; the rest come with DL-2 / DL-3.
/// <see cref="Planned"/> and <see cref="Issued"/> are the ACTIVE states (the partial unique index's filter, D-8).
/// </summary>
public enum WaveOrderState
{
    Planned,
    Issued,
    Withdrawn,
    Excluded,
    Removed,
    OrderCancelled
}

/// <summary>How an order entered the wave (pack §4.2, D-1): picked by hand, or from "Suggest".</summary>
public enum WaveOrderAddedVia
{
    Manual,
    Suggestion
}

/// <summary>The two streams of <c>wms_wave_events</c> (pack §4.3 r3): a wave's timeline, or a warehouse setting's.</summary>
public enum WaveEventStream
{
    Wave,
    Settings
}

/// <summary>
/// The kinds of the insert-only timeline (pack §4.3). DL-1 writes Created, Updated, OrdersAdded, OrdersRemoved, Cancelled (stream Wave) and
/// SettingsUpdated (stream Settings).
/// </summary>
public enum WaveEventKind
{
    Created,
    Updated,
    OrdersAdded,
    OrdersRemoved,
    Cancelled,
    ReleaseStarted,
    Released,
    ReleaseAborted,
    OrderExcluded,
    PickingStarted,
    RemainingRecalled,
    OrderWithdrawn,
    OrderMoved,
    OrderCancelledUpstream,
    Completed,
    PartiallyCompleted,
    ExceptionRaised,
    ExceptionResolved,
    ExceptionIgnored,
    ExceptionCleared,
    SettingsUpdated,
    PendingOrdersIssued,
    PendingOrdersStuckChanged
}

/// <summary>The business limits a wave can exceed (pack §8.5, D-7): a warning, never a refusal.</summary>
public enum WaveLimitKind
{
    MaxOrders,
    MaxLines
}
