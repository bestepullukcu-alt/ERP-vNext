using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Rules;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.Quarantine.Commands;

// QUARANTINE-BUNDLE v1 commands, already shape-checked by the Api request reader (unknown / scope members, types). The
// business rules (pack §12) run in the handlers. Tenant, company and actor come from the resolved scope, never a body.

/// <summary>One stock row of a no-workflow action (contract §4.4 / §4.15 bucket; <c>lotId</c> names the lot of the row).</summary>
public sealed record BucketInput(Guid ItemId, Guid SkuId, SkuLevel SkuLevel, string WarehouseId, string LocationId, decimal Quantity, string? LotId);

/// <summary>
/// QUARANTINE 1.3.0 (MOD-0177 CT-R3): the module that asked for the hold, in-process only (not a wire member) — the hold gets
/// <c>Source = Recall</c> and <c>sourceDocument = { module, type, id, number }</c>, so <c>listHoldsBySource</c> answers "which holds did this case cause".
/// </summary>
public sealed record HoldOrigin(string Module, string Type, string Id, string Number);

/// <summary>§4.4 quarantineStock — "Karantinaya Al" (no workflow, safety direction). <paramref name="Origin"/>: set only by an in-process caller (CT-R3).</summary>
public sealed record QuarantineStockCommand(
    string Mode, string? LotId, IReadOnlyList<BucketInput>? Buckets, string? Reason, string? SignatureId, string IdempotencyKey, HoldOrigin? Origin = null)
    : IRequest<Response<IReadOnlyList<HoldRefDto>>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForQuarantine("Create", "QuarantineHold");
    public Guid? CentralAuditEntityId(object? response) => Id(Data<IReadOnlyList<HoldRefDto>>(response)?.FirstOrDefault()?.HoldId);
}

/// <summary>§4.15 blockStock — "Stoğu bloke et" (no workflow, safety direction).</summary>
public sealed record BlockStockCommand(
    IReadOnlyList<BucketInput> Buckets, string? BlockReasonCode, string? Note, string? Reason, string? SignatureId, string IdempotencyKey)
    : IRequest<Response<IReadOnlyList<BlockedStockRefDto>>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForQuarantine("Create", "BlockedStock");
    public Guid? CentralAuditEntityId(object? response) => Id(Data<IReadOnlyList<BlockedStockRefDto>>(response)?.FirstOrDefault()?.BlockedId);
}

/// <summary>
/// §4.3 updateHold (FU-0175-Q5-01) — analysis note / attachment references while the hold is open. <see cref="NoteGiven"/>
/// tells "analysisNote absent" (keep) from "analysisNote: empty" (clear); <see cref="Attachments"/> null = keep.
/// </summary>
public sealed record UpdateHoldCommand(
    Guid HoldId, bool NoteGiven, string? AnalysisNote, IReadOnlyList<AttachmentDto>? Attachments, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<HoldDetailDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForQuarantine("Update", "QuarantineHold");
    public Guid? CentralAuditEntityId(object? response) => HoldId;
}

/// <summary>§4.5 startReview — "İncelemeye al".</summary>
public sealed record StartReviewCommand(Guid HoldId, string? Note, string IdempotencyKey, int ExpectedVersion) : IRequest<Response<HoldRefDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForQuarantine("LifecycleTransition", "QuarantineHold");
    public Guid? CentralAuditEntityId(object? response) => HoldId;
}

/// <summary>§4.14 completeBlockedReview — "İncelemeyi tamamla".</summary>
public sealed record CompleteBlockedReviewCommand(Guid BlockedId, string? Reason, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<BlockedStockRefDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForQuarantine("LifecycleTransition", "BlockedStock");
    public Guid? CentralAuditEntityId(object? response) => BlockedId;
}

/// <summary>§4.6 createDecision — a Draft; nothing moves.</summary>
public sealed record CreateDecisionCommand(
    DecisionKind Kind,
    Guid TargetId,
    IReadOnlyList<RequestedLine> Lines,
    string? BlockReasonCode,
    string? Reason,
    bool CoaChecked,
    DateOnly? NewRetestDate,
    RequestedDisposition? Disposition,
    string IdempotencyKey) : IRequest<Response<DecisionDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForQuarantine("Create", "QuarantineDecision");
    public Guid? CentralAuditEntityId(object? response) => Id(Data<DecisionDto>(response)?.DecisionId);
}

/// <summary>§4.7 submitDecision — sends the decision for approval (Q1: the approval port).</summary>
public sealed record SubmitDecisionCommand(Guid DecisionId, string? SignatureId, string IdempotencyKey) : IRequest<Response<DecisionDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForQuarantine("LifecycleTransition", "QuarantineDecision");
    public Guid? CentralAuditEntityId(object? response) => DecisionId;
}

/// <summary>§4.11 reapplyDecision — "Yeniden uygula" after ApplyFailed (same ledger keys, CT-Q9).</summary>
public sealed record ReapplyDecisionCommand(Guid DecisionId) : IRequest<Response<DecisionDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForQuarantine("Execute", "QuarantineDecision");
    public Guid? CentralAuditEntityId(object? response) => DecisionId;
}

/// <summary>§4.17 saveSettings (the resolved company).</summary>
public sealed record SaveSettingsCommand(
    int? WaitTargetDays,
    bool? FourEyes,
    IReadOnlyList<string>? ApproverCandidates,
    IReadOnlyDictionary<string, string>? SampleLocations,
    bool? LotReleaseRequiresApproval,
    int? ReconcileAfterSeconds,
    int ExpectedVersion) : IRequest<Response<SettingsDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForQuarantine("Update", "QuarantineSettings");
    public Guid? CentralAuditEntityId(object? response) => Id(Data<SettingsDto>(response)?.LegalEntityId);
}

/// <summary>§4.19 createBlockReason.</summary>
public sealed record CreateBlockReasonCommand(string? Code, IReadOnlyDictionary<string, string>? Names, bool IsActive) : IRequest<Response<BlockReasonDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForQuarantine("Create", "BlockReason");
    public Guid? CentralAuditEntityId(object? response) => null;
}

/// <summary>§4.19 updateBlockReason (names / active; the code never changes).</summary>
public sealed record UpdateBlockReasonCommand(string Code, IReadOnlyDictionary<string, string>? Names, bool? IsActive) : IRequest<Response<BlockReasonDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForQuarantine("Update", "BlockReason");
    public Guid? CentralAuditEntityId(object? response) => null;
}
