using Diten.InventoryService.Application.Features.CycleCounts;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.Rules;

namespace Diten.InventoryService.Application.Features.CycleCounting.Services;

/// <summary>
/// Stored record → wire (draft §3). The blind rule lives HERE, once (pack §8.2 rule 5, Q-1 = A): a blind count's expected, movement and variance
/// quantities are not written in any answer — lines and timeline data — before submit, whatever the caller's keys; after submit only a
/// <c>cycle-count.variances.read</c> holder sees them (r5, SB1/F-6).
/// </summary>
public static class CycleCountReads
{
    public static CycleCountDto Dto(CycleCount c) => new()
    {
        CountId = c.Id.ToString("D"),
        Number = c.Number,
        WarehouseId = c.WarehouseId.ToString("D"),
        CountType = c.CountType.ToString(),
        Blind = c.Blind,
        ScopeLocationIds = c.ScopeLocationIds.Select(x => x.ToString("D")).ToList(),
        ScopeSkuIds = c.ScopeSkuIds.Select(x => x.ToString("D")).ToList(),
        PlannedDate = c.PlannedDate,
        AssignedTo = c.AssignedTo?.ToString("D"),
        Note = c.Note,
        Status = c.Status.ToString(),
        SnapshotAt = CycleCountSupport.Wire(c.SnapshotAt),
        ScopeBinIds = c.ScopeBinIds.Select(x => x.ToString("D")).ToList(),
        LineCount = c.LineCount,
        RecountOfId = c.RecountOfId?.ToString("D"),
        CreatedBy = c.CreatedBy.ToString("D"),
        CreatedAt = CycleCountSupport.Wire(c.CreatedAt),
        UpdatedAt = CycleCountSupport.Wire(c.UpdatedAt),
        SubmittedBy = c.SubmittedBy?.ToString("D"),
        SubmittedAt = CycleCountSupport.Wire(c.SubmittedAt),
        CancelledBy = c.CancelledBy?.ToString("D"),
        CancelledAt = CycleCountSupport.Wire(c.CancelledAt),
        CancelReason = c.CancelReason,
        Version = c.Version
    };

    /// <summary>Pack §8.2 rule 5: does this answer carry the expected quantity of <paramref name="count"/>'s lines?</summary>
    public static bool ShowsExpected(CycleCount count, bool callerReadsVariances)
        => !count.Blind || (count.Status is CycleCountStatus.Submitted or CycleCountStatus.Closed && callerReadsVariances);

    public static CycleCountLineDto Line(CycleCountLine l, bool showExpected) => new()
    {
        LineId = l.Id.ToString("D"),
        CountId = l.CountId.ToString("D"),
        WarehouseId = l.WarehouseId.ToString("D"),
        LocationId = l.LocationId.ToString("D"),
        Item = new CycleCountItemRefDto(l.ItemId.ToString("D"), l.SkuId.ToString("D"), WireNames.ToWire(l.SkuLevel)),
        LotId = l.LotId,
        StockStatus = WireNames.ToWire(l.StockStatus),
        BaseUomId = l.BaseUomId,
        Origin = l.Origin.ToString(),
        SerialControlled = l.SerialControlled,
        ExpectedQuantity = showExpected ? InventoryQuantity.ToWire(l.ExpectedQuantity) : null,
        CountedQuantity = l.CountedQuantity is { } counted ? InventoryQuantity.ToWire(counted) : null,
        CountedBy = l.CountedBy?.ToString("D"),
        CountedAt = CycleCountSupport.Wire(l.CountedAt),
        MovementsSinceSnapshot = showExpected && l.MovementsSinceSnapshot is { } since ? InventoryQuantity.ToWire(since) : null,
        ExpectedAtCount = showExpected && l.ExpectedAtCount is { } atCount ? InventoryQuantity.ToWire(atCount) : null,
        VarianceQuantity = showExpected && l.VarianceQuantity is { } variance ? InventoryQuantity.ToWire(variance) : null,
        MovementDuringCount = l.VarianceQuantity is null ? null : l.MovementDuringCount,
        SnapshotDrift = l.VarianceQuantity is null ? null : l.SnapshotDrift,
        ScanRef = l.ScanRef,
        Status = l.Status.ToString(),
        Version = l.Version
    };

    /// <summary>
    /// The event data members that carry an expected, movement or variance quantity (pack §4.3 F-13 / r5 SB1/F-6): written by the submit's
    /// <c>VarianceComputed</c> rows; projected out of a blind count's timeline exactly as the line fields are (<see cref="ShowsExpected"/>).
    /// </summary>
    public static readonly IReadOnlySet<string> ExpectedDataKeys =
        new HashSet<string>(StringComparer.Ordinal) { "expectedQuantity", "movementsSinceSnapshot", "expectedAtCount", "varianceQuantity" };

    public static CycleCountEventDto Event(CycleCountEvent e, bool showExpected) => new()
    {
        EventId = e.Id.ToString("D"),
        CountId = e.CountId.ToString("D"),
        LineId = e.LineId?.ToString("D"),
        Seq = e.Seq,
        Kind = e.Kind.ToString(),
        ActorUserId = e.ActorUserId?.ToString("D"),
        AtUtc = CycleCountSupport.Wire(e.At),
        Data = e.Data.Where(kv => kv.Value is not null && (showExpected || !ExpectedDataKeys.Contains(kv.Key)))
            .ToDictionary(kv => kv.Key, kv => kv.Value!, StringComparer.Ordinal)
    };
}
