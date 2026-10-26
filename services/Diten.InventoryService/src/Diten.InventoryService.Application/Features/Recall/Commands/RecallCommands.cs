using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;
using Diten.InventoryService.Domain.Enums;
using Diten.InventoryService.Domain.Recall.Enums;
using MediatR;
using static Diten.InventoryService.Application.Common.InventoryCentralAudit;

namespace Diten.InventoryService.Application.Features.Recall.Commands;

// RECALL-BUNDLE v1 §4 writes served by R1. Every command carries the request's idempotency key (header = body, 8–100) and, on a
// mutable record, the If-Match version. The controller parses the body; the handler holds every business rule.
// BL-501: every command also declares its central-audit record (ICentralAuditDeclaration; InventoryCentralAudit). The recall
// case timeline and the inventory audit rows stay as they are.

/// <summary>§4.59 updateSettings (PUT, full replacement; If-Match "0" creates the company's first row). CT-R16: signature by policy, not by default.</summary>
public sealed record UpdateRecallSettingsCommand(RecallSettingsInput Settings, string? Reason, string? SignatureId, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<RecallSettingsDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("Update", "RecallSettings");
    public Guid? CentralAuditEntityId(object? response) => Id(Data<RecallSettingsDto>(response)?.LegalEntityId);
}

/// <summary>R4 prepareSettingsChange (RECALL 1.3.0, CT-R16) — the proposal to sign: kept 15 minutes, the MOD-0022 subject <c>recall.settings-change</c>.</summary>
public sealed record PrepareRecallSettingsChangeCommand(RecallSettingsInput Settings, string? Reason, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<RecallPendingActionResponseDto>>, ICentralAuditDeclaration
{
    // The proposal is written in RecallRepository.InTransactionAsync (enlists the event); the response's CaseId carries the change id.
    public CentralAuditMetadata CentralAudit => ForRecall("Create", "RecallSettingsChange");
    public Guid? CentralAuditEntityId(object? response) => Id(Data<RecallPendingActionResponseDto>(response)?.CaseId);
}

/// <summary>§4.30 openCase — R1: by hand from a root lot (opening from a trace is R2).</summary>
public sealed record OpenRecallCaseCommand(
    Guid RootLotId,
    RecallTraceDirection Direction,
    int Class,
    RecallLevel Level,
    string? Reason,
    string? ReasonReference,
    string IdempotencyKey) : IRequest<Response<RecallCaseDetailDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("Create", "RecallCase");
    public Guid? CentralAuditEntityId(object? response) => Id(Data<RecallCaseDetailDto>(response)?.CaseId);
}

/// <summary>§4.31a addScopeItems — R1 hand entry of Lot / Stock / Shipment items (Draft).</summary>
public sealed record AddRecallScopeItemsCommand(Guid CaseId, IReadOnlyList<RecallScopeItemInput> Items, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<RecallScopeViewDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("Update", "RecallCase");
    public Guid? CentralAuditEntityId(object? response) => CaseId;
}

/// <summary>§4.31 markScopeItems — include / exclude + reason (Draft; CT-R6 subtree).</summary>
public sealed record MarkRecallScopeItemsCommand(Guid CaseId, IReadOnlyList<RecallScopeMarkInput> Marks, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<RecallScopeViewDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("Update", "RecallCase");
    public Guid? CentralAuditEntityId(object? response) => CaseId;
}

/// <summary>§4.32 submitScope — Draft → PendingScopeApproval; the scope is sealed (SHA-256).</summary>
public sealed record SubmitRecallScopeCommand(Guid CaseId, string? Note, string IdempotencyKey, int ExpectedVersion) : IRequest<Response<RecallCaseDetailDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("LifecycleTransition", "RecallCase");
    public Guid? CentralAuditEntityId(object? response) => CaseId;
}

/// <summary>§4.33 returnScope — PendingScopeApproval → Draft.</summary>
public sealed record ReturnRecallScopeCommand(Guid CaseId, string? Reason, string IdempotencyKey, int ExpectedVersion) : IRequest<Response<RecallCaseDetailDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("LifecycleTransition", "RecallCase");
    public Guid? CentralAuditEntityId(object? response) => CaseId;
}

/// <summary>§4.34 prepareCaseAction — the decision the caller is about to sign (15 minutes).</summary>
public sealed record PrepareRecallCaseActionCommand(
    Guid CaseId,
    RecallPendingActionKind Action,
    string? Note,
    string? Summary,
    string? OpenQuantityAcceptance,
    string IdempotencyKey,
    int ExpectedVersion) : IRequest<Response<RecallPendingActionResponseDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("Update", "RecallCase");
    public Guid? CentralAuditEntityId(object? response) => CaseId;
}

/// <summary>§4.35 approveScope — PendingScopeApproval → Active (+ signature; CT-R1; scope re-sealed and compared).</summary>
public sealed record ApproveRecallScopeCommand(Guid CaseId, string? Note, string? SignatureId, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<RecallCaseDetailDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("LifecycleTransition", "RecallCase");
    public Guid? CentralAuditEntityId(object? response) => CaseId;
}

/// <summary>§4.37 cancelCase — Draft | PendingScopeApproval → Cancelled.</summary>
public sealed record CancelRecallCaseCommand(Guid CaseId, string? Reason, string IdempotencyKey, int ExpectedVersion) : IRequest<Response<RecallCaseDetailDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("LifecycleTransition", "RecallCase");
    public Guid? CentralAuditEntityId(object? response) => CaseId;
}

/// <summary>§4.40 confirmSegregation — R1: by in-scope stock node key (the "held" check of a quarantine request is R3).</summary>
public sealed record ConfirmRecallSegregationCommand(Guid CaseId, string? NodeKey, Guid LocationId, decimal Quantity, string? Note, string IdempotencyKey)
    : IRequest<Response<RecallSegregationDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("Create", "RecallSegregation");
    public Guid? CentralAuditEntityId(object? response) => Id(Data<RecallSegregationDto>(response)?.SegregationId);
}

/// <summary>§4.41 recordNotification — KR-F record only (no e-mail).</summary>
public sealed record RecordRecallNotificationCommand(
    Guid CaseId,
    string? CustomerRef,
    DateOnly NotifiedOn,
    RecallNotificationChannel Channel,
    string? Note,
    Guid? SupersedesId,
    string IdempotencyKey) : IRequest<Response<RecallNotificationDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("Create", "RecallNotification");
    public Guid? CentralAuditEntityId(object? response) => Id(Data<RecallNotificationDto>(response)?.NotificationId);
}

/// <summary>§4.42 declareConsumption.</summary>
public sealed record DeclareRecallConsumptionCommand(
    Guid CaseId,
    string? CustomerRef,
    Guid SkuId,
    SkuLevel SkuLevel,
    decimal Quantity,
    DateOnly DeclaredOn,
    string? Evidence,
    Guid? SupersedesId,
    string IdempotencyKey) : IRequest<Response<RecallConsumptionDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("Create", "RecallConsumptionDeclaration");
    public Guid? CentralAuditEntityId(object? response) => Id(Data<RecallConsumptionDto>(response)?.DeclarationId);
}

/// <summary>§4.44 startEffectivenessCheck — Active → EffectivenessCheck.</summary>
public sealed record StartRecallEffectivenessCommand(Guid CaseId, string IdempotencyKey, int ExpectedVersion) : IRequest<Response<RecallCaseDetailDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("LifecycleTransition", "RecallCase");
    public Guid? CentralAuditEntityId(object? response) => CaseId;
}

/// <summary>§4.45 completeEffectivenessCheck (+ signature).</summary>
public sealed record CompleteRecallEffectivenessCommand(Guid CaseId, string? Note, string? SignatureId, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<RecallCaseDetailDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("LifecycleTransition", "RecallCase");
    public Guid? CentralAuditEntityId(object? response) => CaseId;
}

/// <summary>§4.46 closeCase (+ signature; CT-R1; acceptance when open &gt; 0).</summary>
public sealed record CloseRecallCaseCommand(Guid CaseId, string? Summary, string? OpenQuantityAcceptance, string? SignatureId, string IdempotencyKey, int ExpectedVersion)
    : IRequest<Response<RecallCaseDetailDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("LifecycleTransition", "RecallCase");
    public Guid? CentralAuditEntityId(object? response) => CaseId;
}

// ── R2 ──────────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>§4.2 runTrace — a command although it is a GET: every trace writes the audit row recall.trace.run (pack §8.12).</summary>
public sealed record RunRecallTraceCommand(string? RootLotId, string? Direction, string? AsOfUtc) : IRequest<Response<RecallTraceDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("Execute", "Lot");
    public Guid? CentralAuditEntityId(object? response) => Id(RootLotId);
}

/// <summary>§4.3 exportTrace — the trace CSV (audited like a trace run; not an evidence export).</summary>
public sealed record ExportRecallTraceCommand(string? RootLotId, string? Direction, string? Format) : IRequest<Response<RecallTraceCsvDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("Export", "Lot");
    public Guid? CentralAuditEntityId(object? response) => Id(RootLotId);
}

/// <summary>§4.7 getCaseScopeDiff — the live re-trace against the approved snapshot; new nodes are recorded as OutOfScopeFinding events.</summary>
public sealed record DiffRecallScopeCommand(string CaseId) : IRequest<Response<RecallScopeDiffDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("Execute", "RecallCase");
    public Guid? CentralAuditEntityId(object? response) => Id(CaseId);
}


// ── R3 ──────────────────────────────────────────────────────────────────────────────────────────────────────

/// <summary>§4.39 requestQuarantine — live AVAILABLE stock lines of in-scope lots without a request ("Karantina isteği gönder").</summary>
public sealed record RequestRecallQuarantineCommand(Guid CaseId, IReadOnlyList<string> NodeKeys, string IdempotencyKey) : IRequest<Response<IReadOnlyList<RecallQuarantineRequestDto>>>, ICentralAuditDeclaration
{
    // One command can open several requests; the record names the case they belong to.
    public CentralAuditMetadata CentralAudit => ForRecall("Create", "RecallCase");
    public Guid? CentralAuditEntityId(object? response) => CaseId;
}

/// <summary>§4.39a retryQuarantineRequest — "Yeniden dene": BlockedByReservation / NothingAvailable / Failed, key family :a{n} (CT-R2).</summary>
public sealed record RetryRecallQuarantineCommand(Guid CaseId, Guid RequestId, string IdempotencyKey) : IRequest<Response<RecallQuarantineRequestDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("Execute", "RecallQuarantineRequest");
    public Guid? CentralAuditEntityId(object? response) => RequestId;
}

/// <summary>§4.43 linkReturn — KR-H: a CUSTOMER_RETURN of an in-scope lot after approval, into a held status, to a customer of the case.</summary>
public sealed record LinkRecallReturnCommand(Guid CaseId, string? TransactionId, string? CustomerRef, string? ReturnReference, string IdempotencyKey)
    : IRequest<Response<RecallReturnLinkDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("Create", "RecallReturnLink");
    public Guid? CentralAuditEntityId(object? response) => Id(Data<RecallReturnLinkDto>(response)?.LinkId);
}

/// <summary>§4.36 reviseScope — CT-R5: revision r+1 with out-of-scope findings; the delta waits for its own signed approval.</summary>
public sealed record ReviseRecallScopeCommand(Guid CaseId, IReadOnlyList<string> AddNodeKeys, IReadOnlyList<RecallScopeMarkInput> Marks, string? Reason,
    string IdempotencyKey, int ExpectedVersion) : IRequest<Response<RecallCaseDetailDto>>, ICentralAuditDeclaration
{
    public CentralAuditMetadata CentralAudit => ForRecall("Update", "RecallCase");
    public Guid? CentralAuditEntityId(object? response) => CaseId;
}
