namespace Diten.InventoryService.Application.Features.Quarantine.Ports;

/// <summary>How a MOD-0023 call ended (CRM claims mapping, WP-CL-BE-4; pack §8 workflow table).</summary>
public enum WorkflowCallOutcome
{
    Ok,
    TemplateMissing,
    Forbidden,
    Rejected,
    Unavailable,
    NotFound,
    Conflict
}

public sealed record WorkflowDisplay(string Title, string? Subtitle, string SourceModule, string DeepLinkUrl, IReadOnlyList<string> Chips);

public sealed record WorkflowStartRequest(
    string TemplateCode,
    string ObjectType,
    string ObjectId,
    string ObjectRef,
    IReadOnlyList<string> CandidatePrincipalIds,
    string ReasonCode,
    string IdempotencyKey,
    DateTimeOffset? DueAt,
    WorkflowDisplay DisplayContext);

public sealed record WorkflowStartResult(WorkflowCallOutcome Outcome, string? InstanceId, string? ReasonCode, string? Message);

public sealed record WorkflowInstanceState(string InstanceId, string Status, string? Outcome, DateTimeOffset? CompletedAt);

/// <summary>One row of <c>GET instances/{id}/history</c> (action: start / approve / reject / cancel / timeout …).</summary>
public sealed record WorkflowHistoryRow(long SequenceNo, string Action, string? ActorId, DateTimeOffset OccurredAt);

public sealed record WorkflowTaskState(string TaskId, string InstanceId, string Status);

public sealed record WorkflowDefinitionInfo(string TemplateId, string TemplateCode, string Status, string? ActivePublishedVersionId);

public sealed record WorkflowCallResult<T>(WorkflowCallOutcome Outcome, T? Value, string? ReasonCode, string? Message)
{
    public bool IsOk => Outcome == WorkflowCallOutcome.Ok;
}

/// <summary>
/// MOD-0023 Workflow over the Gateway (<c>/api/v1/workflow/*</c>), with the CALLER's bearer token (pack §8; CRM
/// <c>GatewayClaimWorkflowClient</c> pattern). There is deliberately no service-token path: the starter must be the user,
/// or MOD-0023's "submitter cannot approve" is switched off silently, and an approval is never made on a user's behalf.
/// </summary>
public interface IQuarantineWorkflowClient
{
    Task<WorkflowStartResult> StartAsync(WorkflowStartRequest request, CancellationToken cancellationToken = default);

    /// <summary>Instances per object (≤ 100 ids); null when MOD-0023 cannot be read (reconciliation is best effort).</summary>
    Task<IReadOnlyDictionary<string, IReadOnlyList<WorkflowInstanceState>>?> InstancesByObjectsAsync(
        string objectType, IReadOnlyList<string> objectIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowHistoryRow>?> HistoryAsync(string instanceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkflowTaskState>?> TasksAsync(CancellationToken cancellationToken = default);

    Task<WorkflowCallResult<bool>> CancelTaskAsync(string taskId, string reasonCode, string idempotencyKey, string? comment, CancellationToken cancellationToken = default);

    Task<WorkflowCallResult<WorkflowDefinitionInfo>> CreateDefinitionAsync(string templateCode, string name, string description, CancellationToken cancellationToken = default);

    Task<WorkflowCallResult<IReadOnlyList<WorkflowDefinitionInfo>>> DefinitionsAsync(CancellationToken cancellationToken = default);

    /// <summary>The version number of a template version (to report the published version).</summary>
    Task<WorkflowCallResult<int>> VersionNumberAsync(string templateId, string versionId, CancellationToken cancellationToken = default);

    Task<WorkflowCallResult<int>> PublishAsync(
        string templateId, string definitionJson, string schemaVersion, string expressionVersion, string reason, CancellationToken cancellationToken = default);
}

/// <summary>
/// The workflow template every quarantine decision uses (CT-Q1: one template; CT-Q4: approver step meaning APPROVE), shipped
/// with the code and published by the tenant Admin through "Onay şablonunu hazırla" (MOD-0022 CT-D6). Identical to
/// QUARANTINE-BUNDLE 1.0.0 <c>x-workflow-template</c> (contract test). No candidates in the template: 0175 sends the company's
/// approver candidates at start, because template candidates would win. <c>signatureRequired</c> / <c>signatureMeaning</c> are
/// kept by MOD-0023 today (unknown keys tolerated — measured, evidence §2) and enforced once MOD-0022 E4 lands.
/// </summary>
public static class QuarantineWorkflowTemplate
{
    public const string Code = QuarantineContract.WorkflowTemplateCode;
    public const string Name = "Karantina kararı onayı (MOD-0175)";
    public const string Description = "QA approval of a MOD-0175 quarantine / blocked-stock decision (KQ-F).";
    public const string SchemaVersion = "workflow_schema_v1";
    public const string ExpressionVersion = "workflow_expr_v1";
    public const string ObjectType = "quarantine.decision";

    public const string DefinitionJson =
        "{ \"stages\": [ { \"code\": \"QA\", \"steps\": [ { \"code\": \"QA_APPROVAL\", \"name\": \"QA onayı\", \"type\": \"approval\", "
        + "\"assignment\": { \"candidatePrincipalIds\": [] }, "
        + "\"requirements\": { \"commentRequired\": false, \"evidenceRequired\": false, \"signatureRequired\": true, \"signatureMeaning\": \"APPROVE\" }, "
        + "\"sla\": { \"dueInMinutes\": 14400 } } ] } ] }";

    /// <summary>Pack §8: <c>quarantine:quarantine.decision:{decisionId:D}:r1</c> (one round per decision).</summary>
    public static string IdempotencyKey(Guid decisionId) => $"quarantine:{ObjectType}:{decisionId:D}:r1";
}

/// <summary>
/// Pack §8 event path: the inbox of MOD-0023 completion events (<c>quarantine_event_inbox</c>, <c>_id</c> = EventId, a
/// duplicate answers false) and the tenant-level lookup of a decision's company — an event carries the tenant, not the
/// company, and every 0175 read is company-scoped.
/// </summary>
public interface IQuarantineEventInbox
{
    Task<bool> TryInsertAsync(Guid eventId, string eventName, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>The company (legal entity) of a decision of <paramref name="tenantId"/>; null when the tenant has no such decision.</summary>
    Task<Guid?> DecisionCompanyAsync(Guid tenantId, Guid decisionId, CancellationToken cancellationToken = default);
}
