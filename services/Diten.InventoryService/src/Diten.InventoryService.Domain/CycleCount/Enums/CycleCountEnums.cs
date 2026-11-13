namespace Diten.InventoryService.Domain.CycleCounting.Enums;

/// <summary>MOD-0181 count type (pack §4.1, Q-1 = A): the selected scope, or every stockable node of the warehouse. No <c>Spot</c> type.</summary>
public enum CycleCountType
{
    Cycle,
    Full
}

/// <summary>The count's state machine (pack §8.3).</summary>
public enum CycleCountStatus
{
    Draft,
    Released,
    Counting,
    Submitted,
    Closed,
    Cancelled
}

/// <summary>A line's state (pack §8.3 "Satır"). DL-1 writes <see cref="Open"/> only (the snapshot lines); the rest come with DL-2 / DL-3.</summary>
public enum CycleCountLineStatus
{
    Open,
    Counted,
    NoVariance,
    AwaitingApproval,
    Approved,
    Rejected,
    Recounted
}

/// <summary>Where a line comes from (pack §4.2): the release snapshot, stock found while counting (DL-2), or a recount's selected key (DL-3).</summary>
public enum CycleCountLineOrigin
{
    Snapshot,
    Found,
    Recount
}

/// <summary>The kinds of the insert-only count timeline (pack §4.3). DL-1 writes Created, Updated, Released and Cancelled.</summary>
public enum CycleCountEventKind
{
    Created,
    Updated,
    Released,
    Cancelled,
    LineCounted,
    LineAdded,
    Submitted,
    VarianceComputed,
    Approved,
    Rejected,
    RecountRequested,
    AdjustmentPending,
    AdjustmentPosted,
    AdjustmentRefused,
    AdjustmentIntegrityError,
    IntegrityResolved,
    Closed
}
