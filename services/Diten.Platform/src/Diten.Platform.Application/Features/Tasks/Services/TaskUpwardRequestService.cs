using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// BL-023 PART B — the UPWARD WORK REQUEST handoff to MOD-0023.
///
/// <para><b>What it is for.</b> A subordinate cannot instruct their own manager. Work that flows up the
/// reporting chain is a REQUEST the manager may refuse, not an order that simply appears in their list — which
/// is how SAP and Oracle both model it. MOD-0024 detects the direction
/// (<see cref="ITaskAssignmentDirection"/>) and hands the question over; it never answers it.</para>
///
/// <para><b>Why this is not a new concept in MOD-0023.</b> MOD-0024 already runs TWO distinct flows through the
/// same engine — <c>task</c> (approval) and <c>task-review</c> (review), each with its own object type, object
/// ref and template code. The engine stores ObjectType as free text and validates it against no allow-list
/// (<c>StartWorkflowInstanceValidator</c>: NotEmpty + MaximumLength(128)), which is exactly the extension point
/// <see cref="TaskReviewService"/>'s own summary documents. A third question is the established pattern, not an
/// invention: none of MOD-0023's files are modified.</para>
///
/// <para><b>Binding A — MOD-0024 decides nothing.</b> This class STARTS an instance and stores the link. Accept
/// and reject happen inside MOD-0023, and the outcome is read back through the same
/// <c>TaskApprovalView.Resolve</c> path a rejected approval already travels (rejected ⇒ the task reads
/// Cancelled). There is deliberately no local <c>if (accepted)</c> branch anywhere in this file — a test asserts
/// that against the source, because a local decision is the mistake this project keeps re-making.</para>
/// </summary>
public interface ITaskUpwardRequestService
{
    /// <summary>
    /// Open the request for a task whose assignee is above the requester. Returns the instance id, or null when
    /// it could not be started — in which case the caller keeps the task and leaves the request unopened rather
    /// than losing the user's work.
    /// </summary>
    Task<Guid?> TryStartRequestAsync(TaskItem task, CancellationToken ct);
}

public sealed class TaskUpwardRequestOptions
{
    public const string SectionName = "Tasks:UpwardRequest";

    /// <summary>
    /// The workflow template MOD-0024 starts for an upward work request. A tenant that designs its own flow
    /// points this at their template code; the default is only a fallback so the behaviour works on day one.
    /// </summary>
    public string TemplateCode { get; set; } = "task-upward-request";

    public string TemplateName { get; set; } = "Task work request";
}

/// <inheritdoc cref="ITaskUpwardRequestService"/>
public sealed class TaskUpwardRequestService : ITaskUpwardRequestService
{
    /// <summary>
    /// The object type MOD-0024 presents to the workflow engine for an upward WORK REQUEST.
    ///
    /// <para>Deliberately neither <c>task</c> nor <c>task-review</c>: the approval gate and the review gate each
    /// key off their own object type, and sharing one would make a work request read as a decision on a
    /// different question entirely. Same separation, same reason, as review's.</para>
    /// </summary>
    public const string RequestObjectType = "task-request";

    /// <summary>Mirrors the other two in shape, and differs in the one segment that keeps histories disjoint.</summary>
    public static string BuildObjectRef(Guid taskId) => $"tasks|{RequestObjectType}|{taskId}";

    private readonly IMediator _mediator;
    private readonly IWorkflowTemplateRepository _templates;
    private readonly ITenantContext _tenantContext;
    private readonly TaskUpwardRequestOptions _options;
    private readonly ILogger<TaskUpwardRequestService> _logger;

    public TaskUpwardRequestService(
        IMediator mediator,
        IWorkflowTemplateRepository templates,
        ITenantContext tenantContext,
        IOptions<TaskUpwardRequestOptions> options,
        ILogger<TaskUpwardRequestService> logger)
    {
        _mediator = mediator;
        _templates = templates;
        _tenantContext = tenantContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<Guid?> TryStartRequestAsync(TaskItem task, CancellationToken ct)
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
                ObjectType: RequestObjectType,
                ObjectId: task.Id.ToString(),
                ObjectRef: BuildObjectRef(task.Id),
                // The person the work is being asked OF. A candidate hint on the same terms as approval's
                // manager: MOD-0023/MOD-0018 resolve who may actually decide, and MOD-0024 never names a decider.
                CandidatePrincipalIds: task.AssigneeUserId is { } assignee && assignee != Guid.Empty
                    ? [assignee.ToString()]
                    : [],
                ReasonCode: null,
                /*
                 * Idempotent per TASK, like approval and unlike review. A refused review sends work back to be
                 * redone and legitimately needs a second round; a refused work REQUEST is simply refused — the
                 * requester would raise a new task, which carries a new id and therefore a new key.
                 */
                IdempotencyKey: $"task-request:{_tenantContext.TenantId}:{task.Id}",
                CommentRequired: false,
                EvidenceRequired: false,
                DueAt: task.DueAt);

            var response = await _mediator.Send(
                new StartWorkflowInstanceCommand(request, CorrelationId()), ct);

            if (!response.IsSuccessful || response.Data is null)
            {
                _logger.LogWarning(
                    "Could not open the upward work request for {TaskId}: {ReasonCode} {Message}. The task is "
                    + "kept; the request stays unopened rather than the work being lost.",
                    task.Id, response.ReasonCode, string.Join("; ", response.Errors));
                return null;
            }

            return response.Data.WorkflowInstanceId;
        }
        catch (Exception ex)
        {
            // The task must survive a workflow outage, for the same reason approval's start does.
            _logger.LogError(ex, "Opening the upward work request for {TaskId} threw; the task is kept.", task.Id);
            return null;
        }
    }

    /// <summary>
    /// Find (or install) the tenant's work-request template, through the SAME lazy, idempotent path approval and
    /// review use — no new flow is designed here, only the same single-step shape under its own code.
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
                Description: "Default single-step upward work request, installed by MOD-0024. "
                             + "Replace or re-point it in the Workflow Designer to change the flow."),
            CorrelationId()), ct);

        if (!created.IsSuccessful || created.Data is null)
        {
            // A concurrent installer most likely won the race on the unique template code — adopt its template.
            var winner = await _templates.GetByTemplateCodeAsync(_options.TemplateCode, ct);
            if (winner is null)
            {
                _logger.LogWarning(
                    "Could not install the work-request template ({TemplateCode}): {ReasonCode}",
                    _options.TemplateCode, created.ReasonCode);
                return null;
            }

            return winner.ActivePublishedVersionId is not null ? winner.Id : await PublishAsync(winner.Id, ct);
        }

        return await PublishAsync(created.Data.Id, ct);
    }

    private async Task<Guid?> PublishAsync(Guid templateId, CancellationToken ct)
    {
        const string definitionJson = """
        {
          "name": "Task work request",
          "steps": [
            {
              "code": "request",
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
                PublishReason: "MOD-0024 default upward work request"),
            CorrelationId()), ct);

        if (published.IsSuccessful)
        {
            return templateId;
        }

        var reloaded = await _templates.GetByIdAsync(templateId, ct);
        if (reloaded?.ActivePublishedVersionId is not null)
        {
            return templateId;
        }

        _logger.LogWarning(
            "Could not publish the work-request template {TemplateId}: {ReasonCode}",
            templateId, published.ReasonCode);
        return null;
    }

    private string CorrelationId() => $"task-request:{Guid.NewGuid():N}";
}
