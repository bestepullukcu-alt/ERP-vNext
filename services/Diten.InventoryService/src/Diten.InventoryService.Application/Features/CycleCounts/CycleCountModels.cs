using Diten.InventoryService.Application.Features.CycleCounting;

namespace Diten.InventoryService.Application.Features.CycleCounts;

// WMS-COUNT-BUNDLE v1 draft §3 — every DTO of the cycle-count surface (Golden Slim: one models file). Empty optional members are not written
// (draft §1 rule 4; the service's serializer skips nulls).

/// <summary>Draft §3.1 <c>CycleCount</c>.</summary>
public sealed record CycleCountDto
{
    public required string CountId { get; init; }
    public required string Number { get; init; }
    public required string WarehouseId { get; init; }
    public required string CountType { get; init; }
    public required bool Blind { get; init; }
    public required IReadOnlyList<string> ScopeLocationIds { get; init; }
    public required IReadOnlyList<string> ScopeSkuIds { get; init; }
    public required string PlannedDate { get; init; }
    public string? AssignedTo { get; init; }
    public string? Note { get; init; }
    public required string Status { get; init; }
    public string? SnapshotAt { get; init; }
    public required IReadOnlyList<string> ScopeBinIds { get; init; }
    public required int LineCount { get; init; }
    public string? RecountOfId { get; init; }
    public required string CreatedBy { get; init; }
    public required string CreatedAt { get; init; }
    public string? UpdatedAt { get; init; }
    public string? SubmittedBy { get; init; }
    public string? SubmittedAt { get; init; }
    public string? CancelledBy { get; init; }
    public string? CancelledAt { get; init; }
    public string? CancelReason { get; init; }
    public required int Version { get; init; }
    public string ContractVersion { get; init; } = CycleCountContract.ContractVersion;
}

public sealed record CycleCountItemRefDto(string ItemId, string SkuId, string SkuLevel);

/// <summary>
/// Draft §3.2 <c>CycleCountLine</c> — on a blind count <see cref="ExpectedQuantity"/>, <see cref="MovementsSinceSnapshot"/>,
/// <see cref="ExpectedAtCount"/> and <see cref="VarianceQuantity"/> are absent before submit, and after it for a caller without
/// <c>cycle-count.variances.read</c> (Q-1 = A; r5 SB1/F-6).
/// </summary>
public sealed record CycleCountLineDto
{
    public required string LineId { get; init; }
    public required string CountId { get; init; }
    public required string WarehouseId { get; init; }
    public required string LocationId { get; init; }
    public required CycleCountItemRefDto Item { get; init; }
    public string? LotId { get; init; }
    public required string StockStatus { get; init; }
    public required string BaseUomId { get; init; }
    public required string Origin { get; init; }
    public required bool SerialControlled { get; init; }
    public string? ExpectedQuantity { get; init; }
    public string? CountedQuantity { get; init; }
    public string? CountedBy { get; init; }
    public string? CountedAt { get; init; }
    public string? MovementsSinceSnapshot { get; init; }
    public string? ExpectedAtCount { get; init; }
    public string? VarianceQuantity { get; init; }
    public bool? MovementDuringCount { get; init; }
    public bool? SnapshotDrift { get; init; }
    public string? RecountOfLineId { get; init; }
    public int? RecountDepth { get; init; }

    /// <summary>DL-3 (draft §3.6–§3.7, additive): the decision, the ledger correction and the integrity resolution.</summary>
    public CountVariances.CycleCountDecisionDto? Decision { get; init; }
    public CountVariances.CycleCountAdjustmentDto? Adjustment { get; init; }
    public CountVariances.CycleCountIntegrityResolutionDto? IntegrityResolution { get; init; }
    public string? ScanRef { get; init; }
    public required string Status { get; init; }
    public required int Version { get; init; }
}

/// <summary>Draft §3.3 <c>CycleCountEvent</c>.</summary>
public sealed record CycleCountEventDto
{
    public required string EventId { get; init; }
    public required string CountId { get; init; }
    public string? LineId { get; init; }
    public required int Seq { get; init; }
    public required string Kind { get; init; }
    public string? ActorUserId { get; init; }
    public required string AtUtc { get; init; }
    public required IReadOnlyDictionary<string, string> Data { get; init; }
}

public sealed record CycleCountPageDto(IReadOnlyList<CycleCountDto> Items, long Total, int Page, int PageSize,
    string ContractVersion = CycleCountContract.ContractVersion);

public sealed record CycleCountLinePageDto(IReadOnlyList<CycleCountLineDto> Items, long Total, int Page, int PageSize,
    string ContractVersion = CycleCountContract.ContractVersion);

public sealed record CycleCountEventPageDto(IReadOnlyList<CycleCountEventDto> Items, long Total, int Page, int PageSize,
    string ContractVersion = CycleCountContract.ContractVersion);

/// <summary>
/// Draft §3.10 <c>CycleCountDashboard</c> (DL-4, pack §8.13, AC-40): counted at read time, never stored. <c>accuracyPercent</c> = exact lines / counted
/// lines × 100 of the period (absent when nothing was counted); <c>manualCountMovements</c> only for one warehouse (pack §2.4 rule).
/// </summary>
public sealed record CycleCountDashboardDto
{
    public string? WarehouseId { get; init; }
    public required int PeriodDays { get; init; }
    public required string PeriodStart { get; init; }
    public required long OpenCounts { get; init; }
    public required long AwaitingApprovalLines { get; init; }
    public required long RefusedAdjustments { get; init; }
    public required long CountedLines { get; init; }
    public required long ExactLines { get; init; }
    public string? AccuracyPercent { get; init; }
    public int? ManualCountMovements { get; init; }
    public string ContractVersion { get; init; } = CycleCountContract.ContractVersion;
}

/// <summary>Draft §3.5 — one row of <c>bulkDeleteCycleCounts</c>.</summary>
public sealed record CycleCountBulkRowDto(string CountId, string Outcome, int? Version = null);

public sealed record BulkDeleteCycleCountsResultDto(IReadOnlyList<CycleCountBulkRowDto> Results, string ContractVersion = CycleCountContract.ContractVersion);

/// <summary>The Draft form (draft §4.1 create / update body without the key): the eight form fields (pack §4.1, form_field_count = 8).</summary>
public sealed record CycleCountFormInput(Guid WarehouseId, string CountType, bool Blind, IReadOnlyList<Guid> ScopeLocationIds, IReadOnlyList<Guid> ScopeSkuIds,
    string PlannedDate, Guid? AssignedTo, string? Note);

/// <summary>One row of the bulk delete body.</summary>
public sealed record CycleCountBulkItemInput(Guid CountId, int Version);

/// <summary>DL-2 <c>recordCycleCountLine</c> body without the key: the counted quantity as sent (a decimal string) and the opaque scan reference.</summary>
public sealed record CycleCountEntryInput(string? CountedQuantity, string? ScanRef);

/// <summary>DL-2 <c>addCycleCountLine</c> body without the key: the found key (bin, SKU, lot — status is AVAILABLE in v1, Q-8 = A) and its quantity.</summary>
public sealed record CycleCountFoundLineInput(Guid LocationId, Guid ItemId, Guid SkuId, string SkuLevel, string? LotId, string? CountedQuantity, string? ScanRef);
