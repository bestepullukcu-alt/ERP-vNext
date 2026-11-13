using System.Text.Json;
using Diten.InventoryService.Application.Features.CountVariances;
using Diten.InventoryService.Application.Features.CycleCounts;
using Diten.InventoryService.Domain.CycleCounting.Entities;
using Diten.InventoryService.Domain.CycleCounting.Enums;
using Diten.InventoryService.Domain.CycleCounting.Rules;
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
        RecountOfLineId = l.RecountOfLineId?.ToString("D"),
        RecountDepth = l.Origin == CycleCountLineOrigin.Recount ? l.RecountDepth : null,
        Decision = l.Decision is { } d ? Decision(d) : null,
        Adjustment = l.Adjustment is { } a ? Adjustment(a, showExpected) : null,
        IntegrityResolution = l.IntegrityResolution is { } r ? new CycleCountIntegrityResolutionDto
        {
            Kind = r.Kind.ToString(), By = r.By.ToString("D"), At = CycleCountSupport.Wire(r.At), Reason = r.Reason,
            HolderTransactionId = r.HolderTransactionId?.ToString("D")
        } : null,
        ScanRef = l.ScanRef,
        Status = l.Status.ToString(),
        Version = l.Version
    };

    public static CycleCountDecisionDto Decision(CycleCountDecision d) => new()
    {
        Kind = d.Kind.ToString(), Source = d.Source.ToString(), By = d.By?.ToString("D"), At = CycleCountSupport.Wire(d.At), Reason = d.Reason
    };

    /// <summary>The correction; its quantity follows the blind rule (it IS the variance's size — r5 SB1/F-6).</summary>
    public static CycleCountAdjustmentDto Adjustment(CycleCountAdjustment a, bool showExpected) => new()
    {
        MovementType = WireNames.ToWire(a.MovementType),
        Quantity = showExpected ? InventoryQuantity.ToWire(a.Quantity) : null,
        IdempotencyKey = a.IdempotencyKey,
        Status = a.Status.ToString(),
        TransactionId = a.TransactionId?.ToString("D"),
        TransactionNumber = a.TransactionNumber,
        RefusalCode = a.RefusalCode,
        RefusalDetails = a.RefusalDetailsJson is { Length: > 0 } json ? JsonDocument.Parse(json).RootElement.Clone() : null,
        RefusalStatus = a.RefusalStatus,
        IntegrityReason = a.IntegrityReason,
        HolderTransactionId = a.HolderTransactionId?.ToString("D"),
        TransactionDate = CycleCountSupport.Wire(a.TransactionDate),
        PostingDate = CycleCountSupport.Wire(a.PostingDate),
        Attempts = a.Attempts,
        FirstAttemptAt = CycleCountSupport.Wire(a.FirstAttemptAt),
        LastAttemptAt = CycleCountSupport.Wire(a.LastAttemptAt)
    };

    /// <summary>Pack §4.4: the stored settings, or the defaults (version 0, <c>isDefault</c>).</summary>
    public static CycleCountSettingsDto Settings(CycleCountSettings? s) => new()
    {
        ToleranceAbsolute = InventoryQuantity.ToWire(s?.ToleranceAbsolute ?? CycleCountPolicy.Default.ToleranceAbsolute),
        TolerancePercent = InventoryQuantity.ToWire(s?.TolerancePercent ?? CycleCountPolicy.Default.TolerancePercent),
        MaxRecounts = s?.MaxRecounts ?? CycleCountPolicy.Default.MaxRecounts,
        MaxLinesPerCount = s?.MaxLinesPerCount ?? CycleCountPolicy.Default.MaxLinesPerCount,
        IsDefault = s is null,
        UpdatedBy = s?.UpdatedBy?.ToString("D"),
        UpdatedAt = CycleCountSupport.Wire(s?.UpdatedAt),
        Version = s?.Version ?? 0
    };

    /// <summary>
    /// The event data members that carry an expected, movement or variance quantity (pack §4.3 F-13 / r5 SB1/F-6): written by the submit's
    /// <c>VarianceComputed</c> rows; projected out of a blind count's timeline exactly as the line fields are (<see cref="ShowsExpected"/>).
    /// </summary>
    public static readonly IReadOnlySet<string> ExpectedDataKeys =
        new HashSet<string>(StringComparer.Ordinal) { "expectedQuantity", "movementsSinceSnapshot", "expectedAtCount", "varianceQuantity", "adjustmentQuantity" };

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
