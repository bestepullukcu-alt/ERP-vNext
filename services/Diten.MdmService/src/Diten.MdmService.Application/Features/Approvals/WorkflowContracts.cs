namespace Diten.MdmService.Application.Features.Approvals;

// WP-VW-W4 (W4-BE-b1) — MDM's view of the MOD-0023 cross-service contract, copied from the CrmService
// (ClaimReviewCore.cs: IClaimWorkflowClient + its records; KnowledgePathReviewCore.cs: IWorkflowDecisionClient,
// WorkflowHistoryEntry, WorkflowDecisionResult). One interface here: MDM has a single approvable family.

public enum WorkflowCallOutcome
{
    Ok,
    TemplateMissing,
    Forbidden,
    Unavailable,
    Rejected,
    NotFound
}

/// <summary>What the Work Center shows on the task (title, subtitle, chips, deep link back into the MDM screen).</summary>
public sealed record WorkflowDisplayContext(
    string Title, string? Subtitle, string SourceModule, string DeepLinkUrl, IReadOnlyList<string> Chips);

public sealed record WorkflowStartRequest(
    string TemplateCode,
    string ObjectType,
    string ObjectId,
    string ObjectRef,
    string IdempotencyKey,
    WorkflowDisplayContext DisplayContext,
    string ReasonCode);

public sealed record WorkflowStartResult(WorkflowCallOutcome Outcome, Guid? WorkflowInstanceId, string? Detail);

/// <summary>One MOD-0023 instance as the batch status read reports it.</summary>
public sealed record WorkflowInstanceState(Guid WorkflowInstanceId, string Status, string? Outcome, DateTimeOffset? CompletedAt);

/// <summary>One MOD-0023 approval task (a step row).</summary>
public sealed record WorkflowTaskState(
    Guid TaskId,
    Guid WorkflowInstanceId,
    string StageCode,
    string StepCode,
    string Status,
    string? AssigneeRef,
    string? ActionedBy,
    string? ActionReasonCode,
    DateTimeOffset? DueAt,
    DateTimeOffset? CompletedAt)
{
    /// <summary>A task MOD-0023 still waits on.</summary>
    public bool IsOpen => Status is "WaitingApproval" or "WaitingEvidence" or "Escalated";
}

/// <summary>One transition row of a MOD-0023 instance (<c>instances/{id}/history</c>).</summary>
public sealed record WorkflowHistoryEntry(
    long SequenceNo,
    string Action,
    string? ActorId,
    string? ActorDisplay,
    string? StepCode,
    string? StepName,
    string? Comment,
    string? ReasonCode,
    DateTimeOffset OccurredAt);

public sealed record WorkflowDecisionResult(WorkflowCallOutcome Outcome, string? Detail);

/// <summary>
/// MOD-0023 reached through the Gateway with the CALLER's <c>Authorization</c> + <c>X-Tenant-Id</c>. Never a service token:
/// a service identity would become the submitter and silently switch MOD-0023's separation of duties off.
/// </summary>
public interface IMdmWorkflowClient
{
    Task<WorkflowStartResult> StartAsync(WorkflowStartRequest request, CancellationToken ct);

    /// <summary>Instances per object id (≤ 100 ids per call). Null when MOD-0023 cannot be reached.</summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<WorkflowInstanceState>>?> GetInstancesByObjectsAsync(
        string objectType, IReadOnlyList<string> objectIds, CancellationToken ct);

    /// <summary>The approval tasks of these instances (the tenant task list, filtered). Null when unreachable.</summary>
    Task<IReadOnlyList<WorkflowTaskState>?> GetTasksAsync(IReadOnlyCollection<Guid> workflowInstanceIds, CancellationToken ct);

    Task<WorkflowCallOutcome> CancelTaskAsync(Guid taskId, string actorId, string reasonCode, string idempotencyKey, CancellationToken ct);

    /// <summary>The caller's actionable tasks (candidate or assignee). Null when unreachable.</summary>
    Task<IReadOnlyList<WorkflowTaskState>?> GetMyTasksAsync(CancellationToken ct);

    Task<WorkflowDecisionResult> DecideTaskAsync(Guid taskId, bool approve, string actorId, string reasonCode,
        string idempotencyKey, string? comment, CancellationToken ct);

    /// <summary>The instance's transition history. Null when unreachable.</summary>
    Task<IReadOnlyList<WorkflowHistoryEntry>?> GetInstanceHistoryAsync(Guid workflowInstanceId, CancellationToken ct);
}

/// <summary>Who performs the current request (the caller principal's <c>sub</c>), or null without a usable identity.</summary>
public interface IMdmActorContext
{
    string? ActorName { get; }
}

public enum WorkflowApplyResult
{
    Applied,
    NotFound,
    NoMatchingOpenRound,
    UnknownOutcome
}
