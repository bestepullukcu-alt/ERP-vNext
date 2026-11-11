using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.Enums;

namespace Diten.InventoryService.Domain.CycleCounting.Rules;

/// <summary>
/// MOD-0181 pure rules (pack §4.1, §8.2, §8.3, §12): the state machine's DL-1 transitions, the scope bounds and the snapshot window. No I/O.
/// </summary>
public static class CycleCountRules
{
    /// <summary>Pack §4.1: a <c>Cycle</c> count names 1..200 scope nodes; a <c>Full</c> count none.</summary>
    public const int MaxScopeLocations = 200;

    /// <summary>Pack §4.1: 0..200 SKUs (empty = every SKU of the scope).</summary>
    public const int MaxScopeSkus = 200;

    public const int MaxNoteLength = 500;

    /// <summary>Pack §12: a cancel / bulk-delete / reject / resolve / recount reason is 1..500 characters after trim.</summary>
    public const int MaxReasonLength = 500;

    /// <summary>Q-24 = A: the default line limit of a count (a company setting from DL-3).</summary>
    public const int DefaultMaxLinesPerCount = 2000;

    /// <summary>Pack §8.2 rule 4: the snapshot window W — the movement-id read covers <c>CreatedAt ≥ SnapshotAt − W</c>.</summary>
    public static readonly TimeSpan SnapshotWindow = TimeSpan.FromMinutes(10);

    /// <summary>Pack §4.1: the node types a <c>Cycle</c> scope may name.</summary>
    public static readonly IReadOnlySet<LocationType> ScopeNodeTypes = new HashSet<LocationType> { LocationType.StorageLocation, LocationType.Zone, LocationType.Bin };

    /// <summary>Pack §8.2 rule 2: the statuses whose bins are taken (a second count may not claim them).</summary>
    public static readonly IReadOnlySet<CycleCountStatus> OpenStatuses =
        new HashSet<CycleCountStatus> { CycleCountStatus.Released, CycleCountStatus.Counting, CycleCountStatus.Submitted };

    /// <summary>Draft edits (update) and the release start only from Draft (pack §8.3).</summary>
    public static bool IsDraft(CycleCount count) => count.Status == CycleCountStatus.Draft;

    /// <summary>Pack §8.3: cancel from Draft (soft delete) or from Released / Counting (cancel state, kept); never from Submitted and later.</summary>
    public static bool CanCancel(CycleCount count) => count.Status is CycleCountStatus.Draft or CycleCountStatus.Released or CycleCountStatus.Counting;

    /// <summary>Pack §8.3: a Draft cancel deletes softly; a later one only changes the state.</summary>
    public static bool CancelDeletes(CycleCount count) => count.Status == CycleCountStatus.Draft;

    /// <summary>A trimmed reason of 1..500 characters, or null.</summary>
    public static string? Reason(string? raw)
    {
        var reason = raw?.Trim();
        return string.IsNullOrEmpty(reason) || reason.Length > MaxReasonLength ? null : reason;
    }

    /// <summary>The window start of the snapshot's movement-id read.</summary>
    public static DateTimeOffset WindowStart(DateTimeOffset snapshotAt) => snapshotAt - SnapshotWindow;
}
