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
}

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
    DateTimeOffset? DueAt);

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
    string? CorrelationId);

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
        instance.CorrelationId);

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
