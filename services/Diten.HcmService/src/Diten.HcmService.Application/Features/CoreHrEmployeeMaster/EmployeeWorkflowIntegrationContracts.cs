namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster;

public interface IWorkflowStartClient
{
    Task<WorkflowStartClientResult> StartAsync(WorkflowStartClientRequest request, CancellationToken cancellationToken);
}

public sealed record WorkflowApprovalDecisionRecordedMessage(
    Guid TenantId,
    Guid WorkflowInstanceId,
    string EventName,
    string Decision,
    string? ReasonCode,
    Guid DecisionBy,
    string ReplayKey,
    int DecisionVersion,
    string IdempotencyKey,
    IReadOnlyDictionary<string, string> Metadata);

public sealed record WorkflowStartClientRequest(
    string DefinitionKey,
    string SubjectModule,
    string SubjectType,
    Guid SubjectId,
    string BusinessKey,
    string RequestedDecisionMode,
    WorkflowCallbackContract Callback,
    IReadOnlyDictionary<string, string> Metadata,
    string IdempotencyKey);

public sealed record WorkflowCallbackContract(
    string Type,
    string EventName);

public sealed record WorkflowStartClientResult(
    bool IsSuccessful,
    int StatusCode,
    Guid? WorkflowInstanceId,
    string? DefinitionKey,
    int? DefinitionVersion,
    string? Status,
    string? ETag,
    string? ReasonCode)
{
    public static WorkflowStartClientResult Success(
        int statusCode,
        Guid workflowInstanceId,
        string definitionKey,
        int definitionVersion,
        string status,
        string? etag)
        => new(true, statusCode, workflowInstanceId, definitionKey, definitionVersion, status, etag, null);

    public static WorkflowStartClientResult Failed(int statusCode, string reasonCode)
        => new(false, statusCode, null, null, null, null, null, reasonCode);
}

public sealed record WorkflowDecisionConsumptionResponse(
    Guid DraftSessionId,
    Guid WorkflowInstanceId,
    string Decision,
    string WorkflowStatus,
    Guid? EmployeeId,
    Guid? EmploymentRecordId,
    Guid? StatusHistoryId,
    Guid? LifecycleEventId,
    bool Replayed,
    IReadOnlyList<string> BlockingReasons);

public static class WorkflowApprovalDecisionConsumptionRules
{
    public const string ExpectedEventName = "workflow.approval_decision.recorded";
    public const string RequiredSubjectModule = "MOD-0251";
    public const string RequiredSubjectType = "employee_draft";

    public static IReadOnlyList<string> ValidateEnvelope(WorkflowApprovalDecisionRecordedMessage message)
    {
        var errors = new List<string>();

        if (message.TenantId == Guid.Empty)
        {
            errors.Add("tenant_required");
        }

        if (message.WorkflowInstanceId == Guid.Empty)
        {
            errors.Add("workflow_instance_required");
        }

        if (!string.Equals(message.EventName, ExpectedEventName, StringComparison.Ordinal))
        {
            errors.Add("unexpected_workflow_event");
        }

        if (string.IsNullOrWhiteSpace(message.IdempotencyKey))
        {
            errors.Add("idempotency_key_required");
        }

        if (string.IsNullOrWhiteSpace(message.ReplayKey))
        {
            errors.Add("replay_key_required");
        }

        if (message.DecisionVersion <= 0)
        {
            errors.Add("decision_version_required");
        }

        if (message.DecisionBy == Guid.Empty)
        {
            errors.Add("decision_by_required");
        }

        if (!string.Equals(message.Decision, "approved", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(message.Decision, "rejected", StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("unsupported_decision");
        }

        if (!message.Metadata.TryGetValue("subjectModule", out var subjectModule)
            || !string.Equals(subjectModule, RequiredSubjectModule, StringComparison.Ordinal))
        {
            errors.Add("subject_module_mismatch");
        }

        if (!message.Metadata.TryGetValue("subjectType", out var subjectType)
            || !string.Equals(subjectType, RequiredSubjectType, StringComparison.Ordinal))
        {
            errors.Add("subject_type_mismatch");
        }

        if (!message.Metadata.TryGetValue("draftSessionId", out var draftSessionId)
            || !Guid.TryParse(draftSessionId, out _))
        {
            errors.Add("draft_session_required");
        }

        if (!message.Metadata.TryGetValue("subjectId", out var subjectId)
            || !Guid.TryParse(subjectId, out _))
        {
            errors.Add("subject_id_required");
        }

        if (!message.Metadata.TryGetValue("businessKey", out var businessKey)
            || string.IsNullOrWhiteSpace(businessKey))
        {
            errors.Add("business_key_required");
        }

        return errors;
    }
}

public static class EmployeeActivationAuditReadiness
{
    public const string RequiredAuditOwner = "MOD-0021";
    public const string RequiredAppendContract = "governed_audit_append";
    public const bool GovernedAuditAppendReady = false;
    public const string Blocker = "MOD-0251 lifecycle activation is not enabled under the current approved draft/reference-validation scope.";
}
