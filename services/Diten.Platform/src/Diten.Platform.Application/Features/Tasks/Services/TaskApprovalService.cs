using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// MOD-0024 Phase 3 — the approval handoff to MOD-0023 (pack §12 K2, charter DCP-004 §10.4 Binding A).
///
/// <para><b>The rule this exists to keep.</b> MOD-0024 owns no approval engine, no approval status and no SLA.
/// It starts a MOD-0023 instance, keeps the instance id as the ONLY link, and asks MOD-0023 for the state whenever
/// it needs it. There is deliberately no "awaiting approval" field on <see cref="TaskItem"/>: the approval state
/// belongs to MOD-0023, and only its OUTCOME lands in MOD-0024's own lifecycle (rejected → Cancelled).</para>
///
/// <para>Everything here goes through MOD-0023's own commands and repositories — none of its files are modified.</para>
/// </summary>
public interface ITaskApprovalService
{
    /// <summary>
    /// Start the approval for a task, installing the default template first if this tenant has none yet.
    /// Returns the instance id, or null when it could not be started — in which case the caller keeps the task and
    /// leaves <c>ApprovalRequired</c> true, so the fail-closed gate holds `start` shut until it is retried.
    /// </summary>
    Task<Guid?> TryStartApprovalAsync(TaskItem task, CancellationToken ct);

    /// <summary>Cancel a task's approval — used when the requirement is switched back off.</summary>
    Task CancelApprovalAsync(TaskItem task, CancellationToken ct);

    /// <summary>
    /// Approval state for MANY tasks in ONE read. Per-task reads would be an N+1 over a projected page, and the
    /// projection asks for every approval-gated task it renders.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, TaskApprovalState>> GetStatesAsync(
        IReadOnlyCollection<Guid> workflowInstanceIds,
        CancellationToken ct);
}

/// <summary>
/// The approval outcome as MOD-0024 needs to read it. Deliberately NOT stored on the task — it is a view of
/// MOD-0023's state at read time.
/// </summary>
public sealed record TaskApprovalState(bool IsPending, bool IsApproved, bool IsRejected);

/// <summary>Operator-overridable settings for the approval handoff.</summary>
public sealed class TaskApprovalOptions
{
    public const string SectionName = "Tasks:Approval";

    /// <summary>
    /// The workflow template MOD-0024 starts for task approval. A tenant that designs its own flow in the Workflow
    /// Designer points this at their template code; the default below is only a fallback so the toggle works on
    /// day one.
    /// </summary>
    public string TemplateCode { get; set; } = "task-approval";

    public string TemplateName { get; set; } = "Task approval";
}

public sealed class TaskApprovalService : ITaskApprovalService
{
    /// <summary>The object type MOD-0024 presents to the workflow engine; also the gate's ObjectType.</summary>
    public const string ApprovalObjectType = "task";

    private readonly IMediator _mediator;
    private readonly IWorkflowTemplateRepository _templates;
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IApprovalTaskRepository _approvalTasks;
    private readonly ITenantContext _tenantContext;
    private readonly Contracts.ICurrentUserContext _currentUser;
    private readonly TaskApprovalOptions _options;
    private readonly ILogger<TaskApprovalService> _logger;

    public TaskApprovalService(
        IMediator mediator,
        IWorkflowTemplateRepository templates,
        IWorkflowInstanceRepository instances,
        IApprovalTaskRepository approvalTasks,
        ITenantContext tenantContext,
        Contracts.ICurrentUserContext currentUser,
        IOptions<TaskApprovalOptions> options,
        ILogger<TaskApprovalService> logger)
    {
        _mediator = mediator;
        _templates = templates;
        _instances = instances;
        _approvalTasks = approvalTasks;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Guid?> TryStartApprovalAsync(TaskItem task, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);

        try
        {
            var templateId = await EnsureTemplateAsync(ct);
            if (templateId is null)
            {
                return null;
            }

            var request = new Workflow.StartWorkflowInstanceRequest(
                TemplateId: templateId,
                TemplateCode: null,
                ObjectType: ApprovalObjectType,
                ObjectId: task.Id.ToString(),
                ObjectRef: BuildObjectRef(task.Id),
                // The chosen manager is a CANDIDATE hint only — MOD-0023 resolves who may actually approve
                // (RuntimeAssignmentSnapshot). MOD-0024 never decides authority.
                CandidatePrincipalIds: task.ApprovalManagerUserId is { } manager && manager != Guid.Empty
                    ? [manager.ToString()]
                    : [],
                ReasonCode: null,
                // Idempotency per task: a retried start must not open a second approval for the same work.
                IdempotencyKey: $"task-approval:{_tenantContext.TenantId}:{task.Id}",
                CommentRequired: false,
                EvidenceRequired: false,
                DueAt: task.DueAt);

            var response = await _mediator.Send(
                new StartWorkflowInstanceCommand(request, CorrelationId()), ct);

            if (!response.IsSuccessful || response.Data is null)
            {
                _logger.LogWarning(
                    "Could not start task approval for {TaskId}: {ReasonCode} {Message}. The task is kept and "
                    + "`start` stays gated until the approval is retried.",
                    task.Id, response.ReasonCode, string.Join("; ", response.Errors));
                return null;
            }

            return response.Data.WorkflowInstanceId;
        }
        catch (Exception ex)
        {
            // The task must survive a workflow outage: losing work the user already typed is not an improvement
            // over a task that cannot start yet. The fail-closed gate keeps it shut.
            _logger.LogError(ex, "Starting task approval for {TaskId} threw; the task is kept, ungated start is not.", task.Id);
            return null;
        }
    }

    public async Task CancelApprovalAsync(TaskItem task, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);
        if (task.WorkflowInstanceId is null)
        {
            return;
        }

        try
        {
            // Cancel through MOD-0023's OWN command so its audit trail and state machine stay authoritative;
            // MOD-0024 never edits workflow state directly.
            var pending = (await _approvalTasks.ListByInstanceIdAsync(task.WorkflowInstanceId.Value, ct))
                .Where(t => t.Status is ApprovalTaskStatus.WaitingApproval or ApprovalTaskStatus.WaitingEvidence)
                .ToList();

            foreach (var approvalTask in pending)
            {
                await _mediator.Send(new CancelWorkflowTaskCommand(
                    approvalTask.Id,
                    new Workflow.CancelWorkflowTaskRequest(
                        ActorId: _currentUser.UserId.ToString(),
                        ReasonCode: "APPROVAL_NO_LONGER_REQUIRED",
                        // Idempotent per approval task: a retried cancel must not double-cancel.
                        IdempotencyKey: $"task-approval-cancel:{approvalTask.Id}",
                        Comment: null),
                    CorrelationId()), ct);
            }
        }
        catch (Exception ex)
        {
            // Best effort: the requirement has already been switched off on the task, and leaving a stale
            // approval open is a lesser evil than failing the user's edit.
            _logger.LogWarning(ex, "Could not cancel the approval for task {TaskId}; it may remain open.", task.Id);
        }
    }

    public async Task<IReadOnlyDictionary<Guid, TaskApprovalState>> GetStatesAsync(
        IReadOnlyCollection<Guid> workflowInstanceIds,
        CancellationToken ct)
    {
        if (workflowInstanceIds is null || workflowInstanceIds.Count == 0)
        {
            return new Dictionary<Guid, TaskApprovalState>();
        }

        var wanted = workflowInstanceIds.Where(id => id != Guid.Empty).ToHashSet();
        if (wanted.Count == 0)
        {
            return new Dictionary<Guid, TaskApprovalState>();
        }

        // ONE read, then filter in memory. IWorkflowInstanceRepository has no by-ids batch method and it is
        // MOD-0023's file, so the batching is owned here rather than by widening their interface. If this ever
        // becomes hot, the right fix is a by-ids read on their side — not a per-task loop here.
        var instances = await _instances.GetAllForTenantAsync(ct);

        return instances
            .Where(instance => wanted.Contains(instance.Id))
            .ToDictionary(
                instance => instance.Id,
                instance => new TaskApprovalState(
                    // Anything that is not a decision keeps the task gated. Escalated/TimedOut are explicitly
                    // NOT approvals — treating them as such would let unapproved work start.
                    IsPending: instance.Status is WorkflowInstanceStatus.Pending
                        or WorkflowInstanceStatus.Active
                        or WorkflowInstanceStatus.Escalated
                        or WorkflowInstanceStatus.TimedOut,
                    IsApproved: instance.Status is WorkflowInstanceStatus.Approved,
                    IsRejected: instance.Status is WorkflowInstanceStatus.Rejected));
    }

    /// <summary>MOD-0024's object reference in the workflow engine's vocabulary.</summary>
    public static string BuildObjectRef(Guid taskId) => $"tasks|{ApprovalObjectType}|{taskId}";

    /// <summary>
    /// Find (or install) the tenant's task-approval template.
    ///
    /// <para>LAZY on purpose: a tenant that never uses approval never gets one, and there is no startup worker to
    /// race with (a lesson from the manifest/permission ordering incident). IDEMPOTENT: the code is looked up
    /// first, and if two concurrent requests both try to create, the second finds the winner's template and uses
    /// it rather than producing a duplicate.</para>
    ///
    /// <para>An instance needs a PUBLISHED version (<c>WORKFLOW_TEMPLATE_NO_ACTIVE_VERSION</c>), so installation
    /// creates AND publishes in one go.</para>
    /// </summary>
    private async Task<Guid?> EnsureTemplateAsync(CancellationToken ct)
    {
        var existing = await _templates.GetByTemplateCodeAsync(_options.TemplateCode, ct);
        if (existing is not null)
        {
            return existing.ActivePublishedVersionId is not null
                ? existing.Id
                : await PublishAsync(existing.Id, ct);
        }

        var created = await _mediator.Send(new CreateWorkflowDefinitionCommand(
            new Workflow.CreateWorkflowDefinitionRequest(
                TemplateCode: _options.TemplateCode,
                Name: _options.TemplateName,
                Description: "Default single-step task approval, installed by MOD-0024. "
                             + "Replace or re-point it in the Workflow Designer to change the flow."),
            CorrelationId()), ct);

        if (!created.IsSuccessful || created.Data is null)
        {
            // Most likely a concurrent installer won the race on the unique template code — adopt its template
            // instead of failing, which is what makes this idempotent under concurrency.
            var winner = await _templates.GetByTemplateCodeAsync(_options.TemplateCode, ct);
            if (winner is null)
            {
                _logger.LogWarning(
                    "Could not install the task-approval template ({TemplateCode}): {ReasonCode}",
                    _options.TemplateCode, created.ReasonCode);
                return null;
            }

            return winner.ActivePublishedVersionId is not null ? winner.Id : await PublishAsync(winner.Id, ct);
        }

        return await PublishAsync(created.Data.Id, ct);
    }

    private async Task<Guid?> PublishAsync(Guid templateId, CancellationToken ct)
    {
        // A single approval step. The engine only requires parseable JSON today, but the shape is written out so a
        // designer opening it sees an intelligible flow rather than an empty object.
        const string definitionJson = """
        {
          "name": "Task approval",
          "steps": [
            {
              "code": "approve",
              "type": "approval",
              "assignment": { "mode": "candidates" },
              "onApproved": "complete",
              "onRejected": "reject"
            }
          ]
        }
        """;

        var published = await _mediator.Send(new PublishWorkflowDefinitionCommand(
            templateId,
            new Workflow.PublishWorkflowDefinitionRequest(
                DefinitionJson: definitionJson,
                SchemaVersion: "1.0",
                ExpressionVersion: "1.0",
                ExpectedTemplateVersion: null,
                ExpectedRowVersion: null,
                PublishReason: "MOD-0024 default task approval"),
            CorrelationId()), ct);

        if (published.IsSuccessful)
        {
            return templateId;
        }

        // A concurrent publisher may have got there first, which is a success for our purposes.
        var reloaded = await _templates.GetByIdAsync(templateId, ct);
        if (reloaded?.ActivePublishedVersionId is not null)
        {
            return templateId;
        }

        _logger.LogWarning(
            "Could not publish the task-approval template {TemplateId}: {ReasonCode}",
            templateId, published.ReasonCode);
        return null;
    }

    private string CorrelationId() => $"task-approval:{Guid.NewGuid():N}";
}
