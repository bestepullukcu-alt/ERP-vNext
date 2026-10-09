using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Quarantine.Enums;
using Diten.InventoryService.Domain.Quarantine.Rules;
using MediatR;

namespace Diten.InventoryService.Application.Features.Quarantine.Commands;

// QUARANTINE-BUNDLE v1 commands, already shape-checked by the Api request reader (unknown / scope members, types). The
// business rules (pack §12) run in the handlers. Tenant, company and actor come from the resolved scope, never a body.

/// <summary>One stock row of a no-workflow action (contract §4.4 / §4.15 bucket; <c>lotId</c> names the lot of the row).</summary>
public sealed record BucketInput(Guid ItemId, Guid SkuId, SkuLevel SkuLevel, string WarehouseId, string LocationId, decimal Quantity, string? LotId);

/// <summary>§4.4 quarantineStock — "Karantinaya Al" (no workflow, safety direction).</summary>
public sealed record QuarantineStockCommand(
    string Mode, string? LotId, IReadOnlyList<BucketInput>? Buckets, string? Reason, string? SignatureId, string IdempotencyKey)
    : IRequest<Response<IReadOnlyList<HoldRefDto>>>;

/// <summary>§4.15 blockStock — "Stoğu bloke et" (no workflow, safety direction).</summary>
public sealed record BlockStockCommand(
    IReadOnlyList<BucketInput> Buckets, string? BlockReasonCode, string? Note, string? Reason, string? SignatureId, string IdempotencyKey)
    : IRequest<Response<IReadOnlyList<BlockedStockRefDto>>>;

/// <summary>§4.5 startReview — "İncelemeye al".</summary>
public sealed record StartReviewCommand(Guid HoldId, string? Note, string IdempotencyKey, int ExpectedVersion) : IRequest<Response<HoldRefDto>>;

/// <summary>§4.14 completeBlockedReview — "İncelemeyi tamamla".</summary>
public sealed record CompleteBlockedReviewCommand(Guid BlockedId, string? Reason, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<BlockedStockRefDto>>;

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
    string IdempotencyKey) : IRequest<Response<DecisionDto>>;

/// <summary>§4.7 submitDecision — sends the decision for approval (Q1: the approval port).</summary>
public sealed record SubmitDecisionCommand(Guid DecisionId, string? SignatureId, string IdempotencyKey) : IRequest<Response<DecisionDto>>;

/// <summary>§4.11 reapplyDecision — "Yeniden uygula" after ApplyFailed (same ledger keys, CT-Q9).</summary>
public sealed record ReapplyDecisionCommand(Guid DecisionId) : IRequest<Response<DecisionDto>>;

/// <summary>§4.17 saveSettings (the resolved company).</summary>
public sealed record SaveSettingsCommand(
    int? WaitTargetDays,
    bool? FourEyes,
    IReadOnlyList<string>? ApproverCandidates,
    IReadOnlyDictionary<string, string>? SampleLocations,
    bool? LotReleaseRequiresApproval,
    int? ReconcileAfterSeconds,
    int ExpectedVersion) : IRequest<Response<SettingsDto>>;

/// <summary>§4.19 createBlockReason.</summary>
public sealed record CreateBlockReasonCommand(string? Code, IReadOnlyDictionary<string, string>? Names, bool IsActive) : IRequest<Response<BlockReasonDto>>;

/// <summary>§4.19 updateBlockReason (names / active; the code never changes).</summary>
public sealed record UpdateBlockReasonCommand(string Code, IReadOnlyDictionary<string, string>? Names, bool? IsActive) : IRequest<Response<BlockReasonDto>>;
