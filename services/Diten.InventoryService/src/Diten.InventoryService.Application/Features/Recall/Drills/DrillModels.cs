using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Domain.Recall.Enums;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.Recall.Drills;

// RECALL-BUNDLE 1.4.0 (WP-0177-R5) — the drill wire shapes. Null members are omitted on the wire. DrillPlan / DrillRun carry their contract
// members plus additive ones (additionalProperties: true); DrillRunPage gains the optional frequencyWarning (R5-2).
// BL-501: every drill write command declares its central-audit record (ICentralAuditDeclaration, like the other recall commands);
// RecallDrillRepository.InTransactionAsync enlists it in the drill transaction. The drill events / inventory_audit rows stay as they are.

public sealed record RecallDrillPlanDto(
    string PlanId,
    string Code,
    string Name,
    string Frequency,
    int? CustomDays,
    string ScopeKind,
    string NextDueOn,
    RecallUserRefDto Owner,
    bool Active,
    string CreatedAtUtc,
    int Version);

public sealed record RecallDrillStepDto(string Step, string CompletedAtUtc, RecallUserRefDto CompletedBy);

public sealed record RecallDrillSnapshotDto(string SnapshotId, string Purpose, string Sha256, string CanonicalVersion, int NodeCount, int GapCount, bool Truncated);

/// <summary>Step 2 "Kapsam" — what the trace reached (a summary shown to the user; no case scope is written).</summary>
public sealed record RecallDrillScopeSummaryDto(int NodeCount, int LotCount, int StockCount, int ShipmentCount, int GapCount, bool Truncated);

public sealed record RecallDrillPendingResultDto(RecallUserRefDto SetBy, string SetAtUtc, string ExpiresAtUtc);

public sealed record RecallDrillRunDto(
    string RunId,
    string Number,
    string PlanId,
    string PlanCode,
    string Status,
    string SelectionMode,
    RecallLotRefDto Lot,
    int? CandidateCount,
    string StartedAtUtc,
    RecallUserRefDto StartedBy,
    IReadOnlyList<RecallDrillStepDto> Steps,
    string? NextStep,
    string? EndedAtUtc,
    int? DurationMinutes,
    int TargetHours,
    bool? Passed,
    int GapCount,
    RecallDrillSnapshotDto? Snapshot,
    RecallDrillScopeSummaryDto? ScopeSummary,
    IReadOnlyList<string>? Customers,
    RecallDrillPendingResultDto? PendingResult,
    string? ResultNote,
    string? ResultSignatureId,
    RecallUserAtDto? Completed,
    string? CancelReason,
    RecallUserAtDto? Cancelled,
    int Version);

/// <summary>Pack §8.9 frequency band: days since the last Completed run vs the settings' <c>drillFrequencyWarningDays</c>.</summary>
public sealed record RecallDrillFrequencyWarningDto(bool Warning, int? DaysSinceLastCompleted, string? LastCompletedAtUtc, int WarningDays);

public sealed record RecallDrillRunPageDto(IReadOnlyList<RecallDrillRunDto> Items, long Total, int Page, int PageSize, RecallDrillFrequencyWarningDto FrequencyWarning);

/// <summary>The DrillPlanInput body (parsed; ranges are the handler's).</summary>
public sealed record RecallDrillPlanInput(
    string? Name,
    RecallDrillFrequency Frequency,
    int? CustomDays,
    RecallDrillScopeKind ScopeKind,
    DateOnly NextDueOn,
    Guid OwnerUserId,
    bool? Active);

// ── commands / queries ───────────────────────────────────────────────────────────────────────────────────

public sealed record ListRecallDrillPlansQuery : IRequest<Response<IReadOnlyList<RecallDrillPlanDto>>>;

public sealed record CreateRecallDrillPlanCommand(RecallDrillPlanInput Input, string IdempotencyKey) : IRequest<Response<RecallDrillPlanDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("Create", "RecallDrillPlan");
    public Guid? CentralAuditEntityId(object? response) => Id(Data<RecallDrillPlanDto>(response)?.PlanId);
}

public sealed record UpdateRecallDrillPlanCommand(Guid PlanId, RecallDrillPlanInput Input, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<RecallDrillPlanDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("Update", "RecallDrillPlan");
    public Guid? CentralAuditEntityId(object? response) => PlanId;
}

public sealed record ListRecallDrillRunsQuery(string? Status, string? From, string? To, string? Page, string? PageSize) : IRequest<Response<RecallDrillRunPageDto>>;

public sealed record GetRecallDrillRunQuery(Guid RunId) : IRequest<Response<RecallDrillRunDto>>;

/// <summary>startDrill — NO time member: the server stamps <c>StartedAtUtc</c> (pack §8.10).</summary>
public sealed record StartRecallDrillCommand(Guid PlanId, RecallDrillSelectionMode SelectionMode, Guid? LotId, string IdempotencyKey)
    : IRequest<Response<RecallDrillRunDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("Create", "RecallDrillRun");
    public Guid? CentralAuditEntityId(object? response) => Id(Data<RecallDrillRunDto>(response)?.RunId);
}

public sealed record CompleteRecallDrillStepCommand(Guid RunId, RecallDrillStep Step, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<RecallDrillRunDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("Update", "RecallDrillRun");
    public Guid? CentralAuditEntityId(object? response) => RunId;
}

public sealed record PrepareRecallDrillResultCommand(Guid RunId, string? ResultNote, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<RecallPendingActionResponseDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("Update", "RecallDrillRun");
    public Guid? CentralAuditEntityId(object? response) => RunId;
}

public sealed record SignRecallDrillResultCommand(Guid RunId, string? ResultNote, string? SignatureId, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<RecallDrillRunDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("LifecycleTransition", "RecallDrillRun");
    public Guid? CentralAuditEntityId(object? response) => RunId;
}

public sealed record CancelRecallDrillCommand(Guid RunId, string? Reason, string IdempotencyKey, int ExpectedVersion) : IRequest<Response<RecallDrillRunDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("LifecycleTransition", "RecallDrillRun");
    public Guid? CentralAuditEntityId(object? response) => RunId;
}
