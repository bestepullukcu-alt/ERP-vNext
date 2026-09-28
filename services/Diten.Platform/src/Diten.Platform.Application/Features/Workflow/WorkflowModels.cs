using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;

namespace Diten.Platform.Application.Features.Workflow;

// MOD-0023 — Workflow Config / Approval Templates. Permission CONSTANTS only are defined here
// (lowercase dotted, PKS-001). The permission seed/grant is owned by MOD-0018 / Diten.AuthService and is
// a separate task — nothing here writes to AuthService.
public static class WorkflowPermissions
{
    public const string DefinitionsView = "platform.workflow.definitions.view";
    public const string DefinitionsManage = "platform.workflow.definitions.manage";
    public const string DefinitionsPublish = "platform.workflow.definitions.publish";
    public const string InstancesStart = "platform.workflow.instances.start";
    public const string InstancesView = "platform.workflow.instances.view";
    public const string TasksApprove = "platform.workflow.tasks.approve";
    public const string TasksReject = "platform.workflow.tasks.reject";
    public const string TasksDelegate = "platform.workflow.tasks.delegate";
    public const string TasksRequestInfo = "platform.workflow.tasks.request-info";
    public const string TasksCancel = "platform.workflow.tasks.cancel";
    public const string EscalationsManage = "platform.workflow.escalations.manage";
    public const string EscalationsRun = "platform.workflow.escalations.run";
    public const string TransitionsEvaluate = "platform.workflow.transitions.evaluate";
}

public static class WorkflowReasonCodes
{
    public const string ValidationFailed = "VALIDATION_FAILED";
    public const string Conflict = "CONFLICT";
    public const string NotFound = "NOT_FOUND";
    public const string NotFoundNonLeakage = "NOT_FOUND_NON_LEAKAGE";
    public const string PermissionDenied = "PERM_DENIED";
    public const string DuplicateTemplateCode = "DUPLICATE_TEMPLATE_CODE";
    public const string WorkflowTemplateNotFound = "WORKFLOW_TEMPLATE_NOT_FOUND";
    public const string WorkflowTemplateVersionNotFound = "WORKFLOW_TEMPLATE_VERSION_NOT_FOUND";
    public const string WorkflowTemplateVersionImmutable = "WORKFLOW_TEMPLATE_VERSION_IMMUTABLE";
    public const string WorkflowTemplatePublishConflict = "WORKFLOW_TEMPLATE_PUBLISH_CONFLICT";
    public const string WorkflowTemplateConcurrencyConflict = "WORKFLOW_TEMPLATE_CONCURRENCY_CONFLICT";
    public const string WorkflowTemplateNotPublished = "WORKFLOW_TEMPLATE_NOT_PUBLISHED";
    public const string WorkflowTemplateNoActiveVersion = "WORKFLOW_TEMPLATE_NO_ACTIVE_VERSION";
    public const string WorkflowInstanceStartConflict = "WORKFLOW_INSTANCE_START_CONFLICT";
    public const string WorkflowAssignmentCandidatesRequired = "WORKFLOW_ASSIGNMENT_CANDIDATES_REQUIRED";
    public const string WorkflowTaskNotFound = "WORKFLOW_TASK_NOT_FOUND";
    public const string WorkflowInstanceNotFound = "WORKFLOW_INSTANCE_NOT_FOUND";
    public const string WorkflowAssignmentSnapshotNotFound = "WORKFLOW_ASSIGNMENT_SNAPSHOT_NOT_FOUND";
    public const string WorkflowActorDenied = "WORKFLOW_ACTOR_DENIED";
    public const string SodViolation = "SOD_VIOLATION";
    public const string WorkflowTaskInvalidState = "WORKFLOW_TASK_INVALID_STATE";
    public const string WorkflowTransitionConflict = "WORKFLOW_TRANSITION_CONFLICT";
    public const string WorkflowTransitionIdempotent = "WORKFLOW_TRANSITION_IDEMPOTENT";
    public const string WorkflowDelegatePrincipalRequired = "WORKFLOW_DELEGATE_PRINCIPAL_REQUIRED";
    public const string WorkflowDelegateSameActorInvalid = "WORKFLOW_DELEGATE_SAME_ACTOR_INVALID";
    public const string WorkflowNoInstance = "WORKFLOW_NO_INSTANCE";
    public const string WorkflowPendingApproval = "WORKFLOW_PENDING_APPROVAL";
    public const string WorkflowWaitingEvidence = "WORKFLOW_WAITING_EVIDENCE";
    public const string WorkflowApproved = "WORKFLOW_APPROVED";
    public const string WorkflowRejected = "WORKFLOW_REJECTED";
    public const string WorkflowCancelled = "WORKFLOW_CANCELLED";
    public const string WorkflowNotTerminalApproved = "WORKFLOW_NOT_TERMINAL_APPROVED";
    public const string WorkflowSlaRuleNotFound = "WORKFLOW_SLA_RULE_NOT_FOUND";
    public const string WorkflowSlaTemplateNotFound = "WORKFLOW_SLA_TEMPLATE_NOT_FOUND";
    public const string WorkflowSlaRuleConflict = "WORKFLOW_SLA_RULE_CONFLICT";
    public const string WorkflowEscalationProcessed = "WORKFLOW_ESCALATION_PROCESSED";
    public const string WorkflowEscalationIdempotent = "WORKFLOW_ESCALATION_IDEMPOTENT";
    public const string WorkflowTimeoutProcessed = "WORKFLOW_TIMEOUT_PROCESSED";
    public const string WorkflowNoOverdueTasks = "WORKFLOW_NO_OVERDUE_TASKS";

    /// <summary>BL-422 — an escalation run carried NowUtc; runs are evaluated against the server clock only.</summary>
    public const string WorkflowEscalationClockNotAccepted = "WORKFLOW_ESCALATION_CLOCK_NOT_ACCEPTED";

    /// <summary>WP-CL-BE-3 — the Platform transaction that carries a terminal transition and its completion event
    /// could not run (e.g. a MongoDB deployment without transactions). Nothing was written.</summary>
    public const string WorkflowTransactionUnavailable = "WORKFLOW_TRANSACTION_UNAVAILABLE";

    /// <summary>WP-CL-BE-3 — batch status read asked for more object ids than the ceiling.</summary>
    public const string WorkflowBatchLimitExceeded = "WORKFLOW_BATCH_LIMIT_EXCEEDED";
}

/// <summary>
/// WP-CL-BE-3 — terminal outcomes as they travel to other services (completion event + batch read). Lower-case,
/// stable, independent of the enum's C# spelling.
/// </summary>
public static class WorkflowOutcomes
{
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Cancelled = "cancelled";
    public const string TimedOut = "timed-out";

    /// <summary>The outcome of a TERMINAL instance status; null while the instance is still running.</summary>
    public static string? For(WorkflowInstanceStatus status) => status switch
    {
        WorkflowInstanceStatus.Completed or WorkflowInstanceStatus.Approved => Approved,
        WorkflowInstanceStatus.Rejected => Rejected,
        WorkflowInstanceStatus.Cancelled => Cancelled,
        WorkflowInstanceStatus.TimedOut => TimedOut,
        _ => null
    };
}

/// <summary>
/// WP-CL-BE-3 — how an approval started by another service reads in WorkCenterNext. Optional on start; snapshotted on
/// the instance and never changed afterwards. Display only: it drives no decision.
/// <list type="bullet">
/// <item><c>Title</c> ≤200, <c>Subtitle</c> ≤300, <c>SourceModule</c> ≤64 (e.g. "crm").</item>
/// <item><c>DeepLinkUrl</c> ≤500 and an APP-RELATIVE path only ("/Crm/Claims/Detail/…"): an absolute URL, a
/// protocol-relative "//host" or a <c>javascript:</c> link is refused with 400.</item>
/// <item><c>Chips</c> ≤5 short labels (≤32 each), e.g. "TR", "v1.0".</item>
/// </list>
/// </summary>
public sealed record WorkflowDisplayContext(
    string? Title = null,
    string? Subtitle = null,
    string? SourceModule = null,
    string? DeepLinkUrl = null,
    IReadOnlyList<string>? Chips = null)
{
    public const int MaxTitle = 200;
    public const int MaxSubtitle = 300;
    public const int MaxSourceModule = 64;
    public const int MaxDeepLinkUrl = 500;
    public const int MaxChips = 5;
    public const int MaxChip = 32;

    /// <summary>App-relative path: starts with a single "/", no scheme, no backslash, no control characters.</summary>
    public static bool IsRelativePath(string? url) =>
        url is not null
        && url.Length > 0
        && url[0] == '/'
        && !url.StartsWith("//", StringComparison.Ordinal)
        && !url.Contains('\\')
        && !url.Contains("://", StringComparison.Ordinal)
        && !url.Contains(':', StringComparison.Ordinal) // javascript:, data:, mailto: … even after a leading slash
        && !url.Any(char.IsControl);

    public WorkflowDisplayContextSnapshot ToSnapshot() => new()
    {
        Title = Clean(Title),
        Subtitle = Clean(Subtitle),
        SourceModule = Clean(SourceModule),
        DeepLinkUrl = Clean(DeepLinkUrl),
        Chips = (Chips ?? []).Select(Clean).OfType<string>().ToList()
    };

    public static WorkflowDisplayContext? From(WorkflowDisplayContextSnapshot? snapshot) => snapshot is null
        ? null
        : new WorkflowDisplayContext(snapshot.Title, snapshot.Subtitle, snapshot.SourceModule, snapshot.DeepLinkUrl,
            snapshot.Chips.ToList());

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

/// <summary>WP-CL-BE-3 — one instance in the batch status read (newest first per object).</summary>
public sealed record WorkflowInstanceStatusDto(
    Guid WorkflowInstanceId,
    string Status,
    string? Outcome,
    string CurrentStageCode,
    string CurrentStepCode,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt);

/// <summary>WP-CL-BE-3 — every instance of one object (empty list when none).</summary>
public sealed record WorkflowObjectInstancesDto(string ObjectId, IReadOnlyList<WorkflowInstanceStatusDto> Instances);

public enum WorkflowTransitionGateDecision
{
    Allowed = 0,
    Blocked = 1,
    NotApplicable = 2
}

public enum WorkflowTransitionGateStatus
{
    NoWorkflow = 0,
    PendingApproval = 1,
    WaitingEvidence = 2,
    Approved = 3,
    Rejected = 4,
    Cancelled = 5,
    NotTerminalApproved = 6
}

// Request payload for creating a workflow definition. TenantId is intentionally absent — it is never
// accepted from the client and is always resolved from the server-side tenant context.
public sealed record CreateWorkflowDefinitionRequest(
    string TemplateCode,
    string Name,
    string? Description);

public sealed record WorkflowDefinitionDetailDto(
    Guid Id,
    string TemplateCode,
    string Name,
    string? Description,
    string Status,
    Guid? ActivePublishedVersionId,
    Guid? CurrentVersionId,
    DateTimeOffset CreatedAt);

public sealed record WorkflowDefinitionListItemDto(
    Guid Id,
    string TemplateCode,
    string Name,
    string Status,
    DateTimeOffset CreatedAt);

public sealed record PublishWorkflowDefinitionRequest(
    string DefinitionJson,
    string SchemaVersion,
    string ExpressionVersion,
    int? ExpectedTemplateVersion,
    string? ExpectedRowVersion,
    string? PublishReason);

public sealed record PublishWorkflowDefinitionResponse(
    Guid TemplateId,
    Guid TemplateVersionId,
    int VersionNumber,
    bool IsImmutable,
    string Status,
    DateTime? PublishedAt,
    string? PublishedBy,
    string? CorrelationId);

public sealed record WorkflowDefinitionVersionDto(
    Guid Id,
    Guid TemplateId,
    int VersionNumber,
    string DefinitionJson,
    string SchemaVersion,
    string ExpressionVersion,
    string Status,
    bool IsImmutable,
    DateTime? PublishedAt,
    string? PublishedBy,
    string? ConcurrencyToken,
    DateTimeOffset CreatedAt);

public sealed record StartWorkflowInstanceRequest(
    Guid? TemplateId,
    string? TemplateCode,
    string ObjectType,
    string ObjectId,
    string? ObjectRef,
    IReadOnlyList<string> CandidatePrincipalIds,
    string? ReasonCode,
    string? IdempotencyKey,
    bool CommentRequired,
    bool EvidenceRequired,
    DateTimeOffset? DueAt,
    // WP-CL-BE-3 — optional, trailing: a pre-existing caller binds exactly as before. See WorkflowDisplayContext.
    WorkflowDisplayContext? DisplayContext = null);

public sealed record StartWorkflowInstanceResponse(
    Guid WorkflowInstanceId,
    Guid TemplateId,
    Guid TemplateVersionId,
    Guid ApprovalTaskId,
    Guid AssignmentSnapshotId,
    string ObjectRef,
    string Status,
    string CurrentStage,
    string CurrentStep,
    DateTimeOffset? StartedAt,
    DateTimeOffset? DueAt,
    string? CorrelationId);

public sealed record WorkflowInstanceDto(
    Guid Id,
    Guid TemplateId,
    Guid? TemplateVersionId,
    string ObjectType,
    string ObjectId,
    string ObjectRef,
    string Status,
    string CurrentStage,
    string CurrentStep,
    DateTimeOffset? StartedAt,
    DateTimeOffset? DueAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? LastTransitionAt,
    string? CorrelationId,
    // WP-CL-BE-3 — additive, trailing.
    string? TemplateCode = null,
    string? Outcome = null,
    WorkflowDisplayContext? DisplayContext = null);

public sealed record WorkflowTaskDto(
    Guid Id,
    Guid WorkflowInstanceId,
    string StageCode,
    string StepCode,
    string Status,
    Guid? AssignmentSnapshotId,
    string? AssigneeRef,
    bool CommentRequired,
    bool EvidenceRequired,
    DateTimeOffset? DueAt,
    DateTimeOffset? CompletedAt,
    string? ActionedBy,
    string? ActionReasonCode);

/// <summary>
/// WP-CL-BE-3a — one row of an instance's transition history (<c>GET instances/{id}/history</c>), in
/// <see cref="SequenceNo"/> order. <see cref="Action"/> is the lower-case kebab transition (start / approve / reject /
/// delegate / request-info / cancel / escalate / timeout). From/To stage+step come from the approval task the row
/// acted on; <see cref="StepName"/> from the pinned template version. <see cref="Comment"/> is the actor's comment /
/// rejection reason text — returned ONLY here, never on events or logs.
/// </summary>
public sealed record WorkflowInstanceHistoryEntryDto(
    long SequenceNo,
    string Action,
    string? ActorId,
    string? ActorDisplay,
    string? FromStageCode,
    string? FromStepCode,
    string? ToStageCode,
    string? ToStepCode,
    string? StepName,
    string? Comment,
    string? ReasonCode,
    DateTimeOffset OccurredAt);

public sealed record ApproveWorkflowTaskRequest(
    string ActorId,
    string ReasonCode,
    string IdempotencyKey,
    string? Comment,
    string? EvidenceRef);

public sealed record RejectWorkflowTaskRequest(
    string ActorId,
    string ReasonCode,
    string IdempotencyKey,
    string? Comment,
    string? EvidenceRef);

public sealed record WorkflowTaskTransitionResponse(
    Guid WorkflowInstanceId,
    Guid ApprovalTaskId,
    string PreviousTaskStatus,
    string NewTaskStatus,
    string PreviousInstanceStatus,
    string NewInstanceStatus,
    string Action,
    bool IsIdempotent,
    Guid TransitionLogId,
    string? CorrelationId);

public sealed record DelegateWorkflowTaskRequest(
    string ActorId,
    string DelegatePrincipalId,
    string ReasonCode,
    string IdempotencyKey,
    string? Comment);

public sealed record RequestInfoWorkflowTaskRequest(
    string ActorId,
    string? TargetPrincipalId,
    string ReasonCode,
    string IdempotencyKey,
    string? Comment,
    string? EvidenceRef);

public sealed record CancelWorkflowTaskRequest(
    string ActorId,
    string ReasonCode,
    string IdempotencyKey,
    string? Comment);

public sealed record EvaluateWorkflowTransitionGateRequest(
    string ObjectType,
    string ObjectId,
    string ObjectRef,
    string RequestedTransition,
    string RequestedTargetState,
    string ActorId,
    string? ReasonCode);

public sealed record EvaluateWorkflowTransitionGateResponse(
    WorkflowTransitionGateDecision Decision,
    WorkflowTransitionGateStatus GateStatus,
    string ObjectType,
    string ObjectId,
    string ObjectRef,
    Guid? WorkflowInstanceId,
    Guid? WorkflowTemplateId,
    Guid? WorkflowTemplateVersionId,
    Guid? ActiveTaskId,
    string? BlockingReasonCode,
    string? BlockingMessage,
    string? CorrelationId);

public sealed record CreateSlaEscalationRuleRequest(
    Guid TemplateId,
    string StageCode,
    string StepCode,
    int DueInMinutes,
    int EscalateAfterMinutes,
    int? TimeoutAfterMinutes,
    IReadOnlyList<string> EscalationPrincipalIds);

public sealed record SlaEscalationRuleDto(
    Guid Id,
    Guid TemplateId,
    string StageCode,
    string StepCode,
    int DueInMinutes,
    int EscalateAfterMinutes,
    int? TimeoutAfterMinutes,
    IReadOnlyList<string> EscalationPrincipalIds,
    bool IsActive,
    string RuleVersion,
    DateTimeOffset CreatedAt);

/// <summary>
/// MOD-0023 manual escalation run. <c>NowUtc</c> is NOT an input (BL-422): a run is always evaluated against the server
/// clock, and a request that carries a value is refused 400 with
/// <see cref="WorkflowReasonCodes.WorkflowEscalationClockNotAccepted"/>. The member stays on the contract so a client
/// still sending it is told, instead of being silently ignored; the recurring sweep passes <c>null</c>.
/// </summary>
public sealed record RunWorkflowEscalationsRequest(
    DateTimeOffset? NowUtc,
    int? MaxItems,
    string? IdempotencyKey);

public sealed record RunWorkflowEscalationsResponse(
    int EvaluatedCount,
    int EscalatedCount,
    int TimedOutCount,
    int SkippedCount,
    IReadOnlyList<WorkflowEscalationResultDto> Results,
    string? CorrelationId);

public sealed record WorkflowEscalationResultDto(
    Guid WorkflowInstanceId,
    Guid ApprovalTaskId,
    string Action,
    string PreviousTaskStatus,
    string NewTaskStatus,
    string PreviousInstanceStatus,
    string NewInstanceStatus,
    string ReasonCode,
    bool IsIdempotent,
    Guid? TransitionLogId);

public static class WorkflowDefinitionMapper
{
    public static WorkflowDefinitionDetailDto ToDetail(WorkflowTemplate template) => new(
        template.Id,
        template.TemplateCode,
        template.Name,
        template.Description,
        template.Status.ToString(),
        template.ActivePublishedVersionId,
        template.CurrentVersionId,
        template.CreatedAt);

    public static WorkflowDefinitionListItemDto ToListItem(WorkflowTemplate template) => new(
        template.Id,
        template.TemplateCode,
        template.Name,
        template.Status.ToString(),
        template.CreatedAt);

    public static WorkflowDefinitionVersionDto ToVersion(WorkflowTemplateVersion version) => new(
        version.Id,
        version.TemplateId,
        version.VersionNumber,
        version.DefinitionJson,
        version.SchemaVersion,
        version.ExpressionVersion,
        version.Status.ToString(),
        version.IsImmutable,
        version.PublishedAt,
        version.PublishedBy,
        version.ConcurrencyToken,
        version.CreatedAt);

    public static WorkflowInstanceDto ToInstance(WorkflowInstance instance) => new(
        instance.Id,
        instance.TemplateId == Guid.Empty ? instance.WorkflowTemplateId : instance.TemplateId,
        instance.TemplateVersionId,
        instance.ObjectType,
        instance.ObjectId,
        instance.ObjectRef,
        instance.Status.ToString(),
        instance.CurrentStage,
        instance.CurrentStep,
        instance.StartedAt,
        instance.DueAt,
        instance.CompletedAt,
        instance.LastTransitionAt,
        instance.CorrelationId,
        instance.TemplateCode,
        WorkflowOutcomes.For(instance.Status),
        WorkflowDisplayContext.From(instance.DisplayContext));

    public static WorkflowTaskDto ToTask(ApprovalTask task) => new(
        task.Id,
        task.WorkflowInstanceId,
        task.StageCode,
        task.StepCode,
        task.Status.ToString(),
        task.AssignmentSnapshotId,
        task.AssigneeRef,
        task.CommentRequired,
        task.EvidenceRequired,
        task.DueAt,
        task.CompletedAt,
        task.ActionedBy,
        task.ActionReasonCode);

    public static SlaEscalationRuleDto ToSlaRule(SlaEscalationRule rule) => new(
        rule.Id,
        rule.TemplateId,
        rule.StageCode,
        rule.StepCode,
        rule.DueInMinutes,
        rule.EscalateAfterMinutes,
        rule.TimeoutAfterMinutes,
        rule.EscalationPrincipalIds,
        rule.IsActive,
        rule.RuleVersion,
        rule.CreatedAt);
}
