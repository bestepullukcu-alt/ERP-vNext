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
    int Version);

public sealed record RecallCasePageDto(IReadOnlyList<RecallCaseRefDto> Items, long Total, int Page, int PageSize, IReadOnlyDictionary<string, long> StatusCounts);

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
    string ComputedAtUtc);

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
    RecallCaseCountsDto Counts);

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
    string? ExclusionReason);

public sealed record RecallScopeViewDto(
    string CaseId,
    int Revision,
    bool Editable,
    int ExcludedCount,
    string Sha256,
    IReadOnlyList<RecallScopeItemDto> Items,
    int Version);

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
