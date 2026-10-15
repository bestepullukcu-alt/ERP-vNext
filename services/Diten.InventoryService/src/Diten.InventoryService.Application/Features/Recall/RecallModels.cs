using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Recall.Enums;

namespace Diten.InventoryService.Application.Features.Recall;

// RECALL-BUNDLE v1.0.0 wire shapes served by R1. Null members are omitted on the wire (Program.cs JSON options). Times are UTC
// "yyyy-MM-ddTHH:mm:ssZ", dates "yyyy-MM-dd"; quantities and percents are JSON numbers.

public sealed record RecallUserRefDto(string UserId);

public sealed record RecallUserAtDto(string UserId, string AtUtc);

public sealed record RecallSettingsDto(
    string LegalEntityId,
    bool IsDefault,
    int DrillTargetHours,
    int DrillFrequencyWarningDays,
    IReadOnlyDictionary<string, decimal> EffectivenessTargetPercent,
    int TraceMaxDepth,
    int TraceMaxNodes,
    string? UpdatedAtUtc,
    RecallUserRefDto? UpdatedBy,
    int Version);

public sealed record RecallSettingsHistoryEntryDto(
    string Field,
    string? OldValue,
    string? NewValue,
    string Reason,
    RecallUserRefDto ChangedBy,
    string ChangedAtUtc,
    int SettingsVersion,
    string? SignatureId);

public sealed record RecallSettingsHistoryPageDto(IReadOnlyList<RecallSettingsHistoryEntryDto> Items, long Total, int Page, int PageSize);

public sealed record RecallLotRefDto(string LotId, string LotNumber);

public sealed record RecallItemRefDto(string ItemId, string SkuId, string SkuLevel);

public sealed record RecallQuantityDto(string SkuId, string BaseUomId, decimal Quantity);

public sealed record RecallProgressDto(string SkuId, string BaseUomId, decimal Returned, decimal Shipped, decimal Percent);

public sealed record RecallCaseRefDto(
    string CaseId,
    string Number,
    string Type,
    string Status,
    int Class,
    string Level,
    RecallItemRefDto Item,
    RecallLotRefDto RootLot,
    string OpenedAtUtc,
    RecallUserRefDto Owner,
    IReadOnlyList<RecallQuantityDto> AffectedQuantity,
    IReadOnlyList<RecallProgressDto> Progress,
    int Version,
    string? SortAt = null,
    int? Position = null);

/// <summary>
/// 1.5.0 (WP-0177-R6, CT decision A) — <see cref="Items"/> carries ONLY real cases; the drill runs of the same page travel in
/// <see cref="DrillRows"/> (a drill run is not a case). Every row carries <c>sortAt</c> + <c>position</c> in the merged page.
/// </summary>
public sealed record RecallCasePageDto(
    IReadOnlyList<RecallCaseRefDto> Items,
    long Total,
    int Page,
    int PageSize,
    IReadOnlyDictionary<string, long> StatusCounts,
    IReadOnlyList<RecallDrillCaseRowDto>? DrillRows = null,
    IReadOnlyDictionary<string, long>? DrillStatusCounts = null);

/// <summary>1.5.0 — a drill run as a Vakalar row (pack §8.10): no class, no level (mockup difference D-25).</summary>
public sealed record RecallDrillCaseRowDto(
    string RunId,
    string Number,
    string Type,
    string Status,
    RecallLotRefDto Lot,
    RecallItemRefDto? Item,
    string StartedAtUtc,
    string? EndedAtUtc,
    RecallUserRefDto Owner,
    int? DurationMinutes,
    int TargetHours,
    bool? Passed,
    string SortAt,
    int Position,
    int Version);

public sealed record RecallScopeSealDto(int Revision, string Sha256, string CanonicalVersion, int ItemCount, string ComputedAtUtc);

public sealed record RecallPendingActionDto(string Action, RecallUserRefDto SetBy, string ExpiresAtUtc);

public sealed record RecallEffectivenessRowDto(
    string? CustomerRef,
    string SkuId,
    string BaseUomId,
    decimal Shipped,
    decimal Returned,
    decimal Consumed,
    decimal Open,
    decimal Percent,
    bool Met,
    bool Notified);

public sealed record RecallEffectivenessDto(
    int Class,
    decimal TargetPercent,
    IReadOnlyList<RecallEffectivenessRowDto> Rows,
    IReadOnlyList<RecallEffectivenessRowDto> Totals,
    string ComputedAtUtc,
    string? Source = null);

public sealed record RecallClosureDto(string Summary, string? OpenQuantityAcceptance, RecallUserAtDto ClosedBy, string? SignatureId);

public sealed record RecallCancellationDto(string Reason, RecallUserAtDto CancelledBy);

public sealed record RecallCaseCountsDto(int ScopeItems, int ExcludedItems, int Customers, int Notified, int ConsumptionDeclarations, int Segregations);

public sealed record RecallCaseDetailDto(
    string CaseId,
    string Number,
    string Type,
    string Status,
    int Class,
    string Level,
    RecallItemRefDto Item,
    RecallLotRefDto RootLot,
    string OpenedAtUtc,
    RecallUserRefDto Owner,
    IReadOnlyList<RecallQuantityDto> AffectedQuantity,
    IReadOnlyList<RecallProgressDto> Progress,
    int Version,
    string Reason,
    string? ReasonReference,
    string Direction,
    int ScopeRevision,
    RecallScopeSealDto? SubmittedScope,
    RecallScopeSealDto? ApprovedScope,
    RecallUserAtDto? ScopeSubmittedBy,
    RecallUserAtDto? ScopeApprovedBy,
    string? ScopeApprovalSignatureId,
    string? EffectivenessStartedAtUtc,
    RecallEffectivenessDto? Effectiveness,
    RecallUserAtDto? EffectivenessCompletedBy,
    string? EffectivenessSignatureId,
    RecallClosureDto? Closure,
    RecallCancellationDto? Cancellation,
    RecallPendingActionDto? PendingAction,
    RecallCaseCountsDto Counts,
    RecallSnapshotRefDto? SubmittedSnapshot = null,
    RecallSnapshotRefDto? ApprovedSnapshot = null,
    RecallPendingRevisionDto? PendingRevision = null);

public sealed record RecallScopeItemDto(
    string NodeKey,
    string? ParentKey,
    string Kind,
    bool IsRoot,
    RecallLotRefDto Lot,
    RecallItemRefDto Item,
    string? WarehouseId,
    string? LocationId,
    string? StockStatus,
    string? SourceDocumentId,
    string? CustomerRef,
    decimal? Quantity,
    string? BaseUomId,
    bool Included,
    bool EffectivelyIncluded,
    string? ExclusionReason,
    string? Origin = null,
    bool? New = null);

public sealed record RecallScopeViewDto(
    string CaseId,
    int Revision,
    bool Editable,
    int ExcludedCount,
    string Sha256,
    IReadOnlyList<RecallScopeItemDto> Items,
    int Version,
    RecallSnapshotRefDto? Snapshot = null);

public sealed record RecallTimelineEntryDto(
    long Seq,
    string Kind,
    RecallUserRefDto? Actor,
    string AtUtc,
    string? Text,
    IReadOnlyDictionary<string, string> Refs,
    string? SignatureId);

public sealed record RecallTimelinePageDto(IReadOnlyList<RecallTimelineEntryDto> Items, long Total, int Page, int PageSize);

public sealed record RecallSubjectRefDto(string Module, string SubjectType, string SubjectId);

/// <summary>prepareCaseAction — what the MOD-0022 dialog needs to sign the prepared decision.</summary>
public sealed record RecallPendingActionResponseDto(
    string CaseId,
    string Action,
    bool SignatureRequired,
    string ActionCode,
    string MeaningCode,
    string VisualMode,
    RecallSubjectRefDto Subject,
    string ExpiresAtUtc,
    int Version);

/// <summary>WP-0177-R7a (RECALL 1.6.0) — one ledger movement of a customer shipment (the caller's company only).</summary>
public sealed record RecallCustomerMovementDto(string TransactionId, string? Number, decimal Quantity, string PostingDate);

/// <summary>WP-0177-R7a — <c>CaseCustomerShipment</c>: one Shipment item of the scope with its ledger movements and the live MVP-6 read.</summary>
public sealed record RecallCaseCustomerShipmentDto(
    string NodeKey,
    RecallLotRefDto Lot,
    RecallItemRefDto Item,
    string? SourceDocumentId,
    decimal Quantity,
    string BaseUomId,
    IReadOnlyList<RecallCustomerMovementDto> Movements,
    string Read,
    string? Status,
    string? ActualDeliverAt,
    string? ShipToReference);

/// <summary>WP-0177-R7a — <c>CustomerRow</c>: a customer of the case with its shipments, notification (current + history) and consumptions.</summary>
public sealed record RecallCustomerRowDto(
    string CustomerRef,
    string? Label,
    IReadOnlyList<RecallCaseCustomerShipmentDto> Shipments,
    RecallNotificationDto? Notification,
    IReadOnlyList<RecallNotificationDto> Notifications,
    IReadOnlyList<RecallConsumptionDto> Consumptions);

public sealed record RecallNotificationDto(
    string NotificationId,
    string CaseId,
    string CustomerRef,
    string NotifiedOn,
    string Channel,
    string? Note,
    string? SupersedesId,
    RecallUserAtDto RecordedBy);

public sealed record RecallConsumptionDto(
    string DeclarationId,
    string CaseId,
    string CustomerRef,
    string SkuId,
    string SkuLevel,
    string BaseUomId,
    decimal Quantity,
    string DeclaredOn,
    string Evidence,
    string? SupersedesId,
    RecallUserAtDto RecordedBy);

public sealed record RecallSegregationDto(
    string SegregationId,
    string CaseId,
    string NodeKey,
    string? WarehouseId,
    string LocationId,
    decimal Quantity,
    string? Note,
    RecallUserAtDto ConfirmedBy);

public sealed record RecallLotCaseDto(string? CaseId, string Number, string? Status, bool SameCompany);

public sealed record RecallLotStatusDto(string LotId, bool UnderRecall, IReadOnlyList<RecallLotCaseDto> Cases);

// ── inputs (parsed by the controller; every business rule is the handler's) ───────────────────────────────

/// <summary>A settings save as the handler receives it (every writable member present — PUT is a full replacement).</summary>
public sealed record RecallSettingsInput(
    int DrillTargetHours,
    int DrillFrequencyWarningDays,
    IReadOnlyDictionary<string, decimal> EffectivenessTargetPercent,
    int TraceMaxDepth,
    int TraceMaxNodes);

/// <summary>One hand-entered scope item (R1; R2 fills the same items from the trace).</summary>
public sealed record RecallScopeItemInput(
    RecallScopeItemKind Kind,
    Guid LotId,
    string? ParentKey,
    Guid? WarehouseId,
    Guid? LocationId,
    StockStatus? StockStatus,
    string? SourceDocumentId,
    string? CustomerRef,
    decimal? Quantity,
    string? BaseUomId);

public sealed record RecallScopeMarkInput(string NodeKey, bool Included, string? ExclusionReason);

// ── R2 (RECALL-BUNDLE 1.1.0): trace, snapshots, scope diff ──────────────────────────────────────────────────

public sealed record RecallSnapshotRefDto(
    string SnapshotId,
    int Revision,
    string Purpose,
    string Sha256,
    string CanonicalVersion,
    int NodeCount,
    int GapCount,
    bool Truncated,
    string ComputedAtUtc);

public sealed record RecallTraceLimitsDto(int MaxDepth, int MaxNodes);

public sealed record RecallTraceMovementDto(string TransactionId, string? TransactionNumber, string MovementType, string PostingDate, string SourceModule, string? SourceDocumentId);

public sealed record RecallTraceShipmentDto(string Read, string? Status, string? ActualDeliverAt, string? ShipToReference);

public sealed record RecallTraceNodeDto(
    string NodeKey,
    string? ParentKey,
    int Level,
    string Side,
    string Kind,
    RecallLotRefDto? Lot,
    RecallItemRefDto? Item,
    bool Untracked,
    string? WarehouseId,
    string? LocationId,
    string? StockStatus,
    decimal? Quantity,
    string? BaseUomId,
    RecallTraceMovementDto? Movement,
    RecallTraceShipmentDto? Shipment,
    string? CustomerRef,
    IReadOnlyList<string> Serials,
    string? GapCode,
    long? Count,
    string? Text);

public sealed record RecallTraceEdgeDto(string From, string To, string Kind, string? TransformationReference, string? QuantityConsumed, string? UomId, bool Cycle);

public sealed record RecallTraceGapDto(
    string Code,
    string NodeKey,
    string Text,
    string? TransactionId,
    string? TransactionNumber,
    string? SkuId,
    decimal? Quantity,
    string? BaseUomId,
    string? Date,
    long? Count);

public sealed record RecallTraceQuantityDto(string SkuId, string BaseUomId, decimal Quantity, int Count);

public sealed record RecallTraceSummaryDto(
    int FinishedGoodLots,
    IReadOnlyList<RecallTraceQuantityDto> Stock,
    IReadOnlyList<RecallTraceQuantityDto> Shipped,
    int InTransitShipments,
    int GapCount,
    long OtherCompanyActivityCount);

public sealed record RecallTraceDto(
    string RootLotId,
    string RootLotNumber,
    string Direction,
    string AsOfUtc,
    string ComputedAtUtc,
    long DurationMs,
    bool Truncated,
    RecallTraceLimitsDto Limits,
    IReadOnlyList<RecallTraceNodeDto> Nodes,
    IReadOnlyList<RecallTraceEdgeDto> Edges,
    IReadOnlyList<RecallTraceGapDto> Gaps,
    RecallTraceSummaryDto Summary,
    string CanonicalVersion,
    string Sha256);

public sealed record RecallScopeDiffDto(
    string ApprovedSha256,
    string LiveSha256,
    IReadOnlyList<RecallTraceNodeDto> Added,
    IReadOnlyList<string> Removed,
    string ComputedAtUtc);

/// <summary>exportTrace — the CSV text and its file name.</summary>
public sealed record RecallTraceCsvDto(string FileName, string Content);


// ── R3 (RECALL-BUNDLE 1.2.0): quarantine requests, stock, returns, scope revision ───────────────────────────

/// <summary>CT-R5 — the revision waiting for the signed approval of its delta (the case stays Active).</summary>
public sealed record RecallPendingRevisionDto(int Revision, string Reason, IReadOnlyList<string> AddedNodeKeys, RecallUserAtDto RevisedBy);

/// <summary>A MOD-0175 hold read at display time (never copied).</summary>
public sealed record RecallHoldDto(string HoldId, string HoldNumber, string Status, string ReviewState, decimal Quantity);

public sealed record RecallReservationLineDto(string ReservationId, string? HolderModule, string? SourceDocumentId, string? RemainingQuantity);

/// <summary>Pack §8.4 KR-B — one request (RECALL-BUNDLE <c>QuarantineRequest</c>; extra members additive).</summary>
public sealed record RecallQuarantineRequestDto(
    string RequestId,
    string Status,
    string? NotNeededReason,
    IReadOnlyList<string> HoldIds,
    int Attempts,
    string NodeKey,
    int Revision,
    RecallLotRefDto Lot,
    string? WarehouseId,
    string? LocationId,
    decimal SnapshotQuantity,
    decimal? PlannedQuantity,
    decimal HeldQuantity,
    string? BaseUomId,
    int RetryNumber,
    string? ErrorCode,
    string? ErrorMessage,
    IReadOnlyList<RecallReservationLineDto> Reservations,
    IReadOnlyList<RecallHoldDto> Holds,
    string RequestedAtUtc,
    string? LastAttemptAtUtc);

/// <summary>"Eldeki stok" — a live stock line of an in-scope lot in the caller's company (RECALL-BUNDLE <c>StockLine</c>).</summary>
public sealed record RecallStockLineDto(
    string NodeKey,
    RecallLotRefDto Lot,
    RecallItemRefDto Item,
    string? WarehouseId,
    string? LocationId,
    string? StockStatus,
    decimal Quantity,
    string BaseUomId,
    RecallQuarantineRequestDto? Request,
    decimal SegregatedQuantity);

/// <summary>WP-0177-R7b (RECALL 1.7.0) — the subject of an evidence pack.</summary>
public sealed record RecallEvidenceSubjectDto(string Kind, string? CaseId, string? RunId, string? From, string? To, string Label);

public sealed record RecallEvidenceTableDto(string Key, IReadOnlyList<string> Columns, IReadOnlyList<IReadOnlyList<string>> Rows);

public sealed record RecallEvidenceSectionDto(bool Applicable, string? Note, IReadOnlyList<RecallEvidenceTableDto> Tables);

/// <summary><c>EvidencePreview</c> — the seven sections by their contract keys (1_summary … 7_signatures).</summary>
public sealed record RecallEvidencePreviewDto(
    RecallEvidenceSubjectDto Subject, string LegalEntityId, string GeneratedAtUtc, string Language, IReadOnlyDictionary<string, RecallEvidenceSectionDto> Sections);

/// <summary><c>EvidenceExport</c> — sha256 is of the produced (= stored) bytes.</summary>
public sealed record RecallEvidenceExportDto(
    string ExportId, RecallEvidenceSubjectDto Subject, string Format, string FileName, string Sha256, long SizeBytes, RecallUserAtDto ExportedBy,
    IReadOnlyList<string> Sections, string? DmsRef = null);

public sealed record RecallEvidenceExportPageDto(IReadOnlyList<RecallEvidenceExportDto> Items, long Total, int Page, int PageSize);

public sealed record RecallEvidenceFileDto(byte[] Content, string ContentType, string FileName, string Sha256);

public sealed record RecallReturnLinkDto(
    string LinkId,
    string Source,
    string TransactionId,
    string? TransactionNumber,
    RecallLotRefDto Lot,
    string SkuId,
    decimal Quantity,
    string BaseUomId,
    string CustomerRef,
    string? ReturnReference,
    string? ToStockStatus,
    string PostedAtUtc,
    RecallUserAtDto LinkedBy,
    string? HoldId = null,
    string? HoldNumber = null);

public sealed record RecallReturnCandidateDto(
    string TransactionId,
    string? TransactionNumber,
    RecallLotRefDto Lot,
    string SkuId,
    decimal Quantity,
    string BaseUomId,
    string? ToStockStatus,
    string PostedAtUtc,
    bool Eligible,
    string? IneligibleReason);

public sealed record RecallReturnsDto(IReadOnlyList<RecallReturnLinkDto> Links, IReadOnlyList<RecallReturnCandidateDto> Candidates);
