using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Tasks;
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
    /// D2-FIX5 item 3 — a live approval under the task's object reference (with an open engine task) is adopted rather than
    /// started again, a closed one is REPLACED by a new round whose key carries its id (the first approval keeps the task's own
    /// key), and a start the engine answers with a closed instance is not linked: null.
    /// </summary>
    Task<Guid?> TryStartApprovalAsync(TaskItem task, CancellationToken ct);

    /// <summary>
    /// Cancel a task's approval — used when the requirement is switched back off. True when nothing is left open
    /// (cancelled now, or nothing was open); false when an open approval task could NOT be cancelled — the caller then
    /// keeps the instance link, or the approval would stay decidable with nothing reading it (D2-FIX1 A1).
    /// D2-FIX3 item 8 — a task with NO link is looked up by its object reference (a start whose link write fell over);
    /// an instance found that way is set as the task's link before it is withdrawn.
    /// </summary>
    Task<bool> CancelApprovalAsync(TaskItem task, CancellationToken ct);

    /// <summary>
    /// D2-FIX4 items 1 and 4 — the live approval the task does NOT point at, read-only: the LATEST instance under the task's
    /// object reference when it is still open and is not the task's link; null otherwise. A withdraw always looks at it
    /// (<see cref="CancelApprovalAsync"/>); the edit that switches the requirement off asks for it BEFORE its write, so the
    /// link is stored in the SAME conditional write as the flag change — never in a second write that can be lost after the
    /// flag is already off (nothing would look for the instance again). Throws when the engine cannot be read: the caller
    /// must not write on a guess. The default (in-memory doubles) finds nothing.
    /// </summary>
    Task<Guid?> FindUnlinkedOpenInstanceAsync(TaskItem task, CancellationToken ct) => Task.FromResult<Guid?>(null);

    /// <summary>
    /// Approval state for MANY tasks in ONE read. Per-task reads would be an N+1 over a projected page, and the
    /// projection asks for every approval-gated task it renders.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, TaskApprovalState>> GetStatesAsync(
        IReadOnlyCollection<Guid> workflowInstanceIds,
        CancellationToken ct);
}

/// <summary>
/// The ONE place that turns MOD-0023's reported state into the two flags MOD-0024 renders from. The provider, the
/// list handler and the detail handler all read it, so the fail-closed rule cannot drift between the Task Center
/// and the task list — it did not drift yet only because rejection had not been added to any of them.
/// </summary>
public static class TaskApprovalView
{
    /// <summary>
    /// <paramref name="outstanding"/> — MOD-0023 still owes a decision, so the task waits and cannot start.
    /// Fail-closed: approval required with no instance (a failed start) or an unreadable instance counts as
    /// outstanding, because reporting it as approved would make unapproved work look startable.
    /// <paramref name="rejected"/> — the work was refused; the task reads as Cancelled and offers no action.
    /// </summary>
    public static (bool Outstanding, bool Rejected) Resolve(
        TaskItem task,
        IReadOnlyDictionary<Guid, TaskApprovalState> states)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(states);

        // No approval asked for and no link held, or the task is already closed: nobody is owed anything.
        // D2-FIX3 item 3 — "asked for" is the gate's own rule (TaskWorkflowLinkGate): the requirement OR a held link. Read
        // on the flag alone, a task whose withdraw failed showed `start` enabled and the server refused it as pending.
        if (!TaskWorkflowLinkGate.AsksApproval(task) || task.Lifecycle is TaskLifecycle.Done or TaskLifecycle.Cancelled)
        {
            return (false, false);
        }

        if (!task.ApprovalRequired)
        {
            // The requirement is withdrawn but the link is held: the instance holds the task until MOD-0023 reports it
            // closed — exactly when the transition gate stops holding it. A refusal no longer kills work nobody requires
            // approval for, so it is never read as Rejected here.
            states.TryGetValue(task.WorkflowInstanceId!.Value, out var held);
            return (Outstanding: TaskWorkflowLinkGate.Holds(held, required: false), Rejected: false);
        }

        if (task.WorkflowInstanceId is not { } instanceId || !states.TryGetValue(instanceId, out var state))
        {
            return (true, false);
        }

        return (Outstanding: !state.IsApproved && !state.IsRejected, Rejected: state.IsRejected);
    }
}

/// <summary>
/// The approval outcome as MOD-0024 needs to read it. Deliberately NOT stored on the task — it is a view of
/// MOD-0023's state at read time.
/// <para><paramref name="IsCancelled"/> — D2-FIX3: MOD-0023 reports the instance CANCELLED (withdrawn). Closed like a
/// refusal for a task whose requirement is withdrawn (<see cref="TaskWorkflowLinkGate.Holds(TaskApprovalState?, bool)"/>);
/// for a review it means nobody is reviewing the work any more (<see cref="TaskReviewView"/>).</para>
/// <para><paramref name="IsTimedOut"/> — D2-FIX4 item 2: the SLA sweep timed the instance out. Terminal (nobody is deciding it
/// any more), though <paramref name="IsPending"/> still reads true for it (it is not a decision, so it never releases a
/// gate). For a review it is "closed without a verdict": the work goes back to its holder (<see cref="TaskReviewView"/>).</para>
/// </summary>
public sealed record TaskApprovalState(
    bool IsPending, bool IsApproved, bool IsRejected, bool IsCancelled = false, bool IsTimedOut = false);

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

    public Task<Guid?> TryStartApprovalAsync(TaskItem task, CancellationToken ct) =>
        TryStartApprovalAsync(task, maySupersede: true, ct);

    private async Task<Guid?> TryStartApprovalAsync(TaskItem task, bool maySupersede, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);

        try
        {
            /*
             * D2-FIX5 item 3 — THE KEY NAMES THE ROUND BEING REPLACED, as a review's does. It used to be one key per task: every
             * start replayed the FIRST instance the engine ever opened for the task, whatever became of it. A task whose
             * approval was withdrawn (cancelled) or timed out and was then asked for again got that dead instance back and
             * was "successfully" linked to it: Resolve reads a cancelled or timed-out approval as not yet decided, so the
             * task waited for a decision nobody could make, for good.
             *
             * The latest instance under the task's object reference is the engine's record of the chain:
             *   • none — the first approval: the key is the task's own, EXACTLY as it always was, so every instance already
             *     stored (each was started under it) stays reachable and nothing is re-keyed;
             *   • still OPEN — the approval the task already has (a start whose link was never stored): adopted, not started a
             *     second time (one live approval per task) — but only when the engine holds an open task for it (see the
             *     review's StartReviewAsync: an instance whose start never completed is neither adopted nor replaced);
             *   • closed (cancelled, timed out, refused, approved) — the new round replaces IT: the key carries its id, so the
             *     same round always produces the same key (a crash between the start and the link write still resolves to one
             *     instance) and a later round produces a different one.
             */
            var latest = await _instances.GetLatestByObjectRefAsync(
                BuildObjectRef(task.Id), ApprovalObjectType, task.Id.ToString(), ct);
            string? halfStartKey = null;
            if (latest is not null && IsOpen(latest.Status))
            {
                if ((await OpenTasksAsync(latest.Id, ct)).Count > 0)
                {
                    return latest.Id;
                }

                // WP-WF-ENGINE-RECOVERY-01 FIX1 item 3 — a half start is completed: the start below is replayed under the
                // round's own key and the same facts, the engine writes the missing task (BL-549) and the round is linked.
                // FIX2 item 5 — changed facts (another manager, a new due date) or another person get the engine's 409; the
                // half start is then superseded (closed) and a new round started, below.
                if (string.IsNullOrWhiteSpace(latest.IdempotencyKey))
                {
                    _logger.LogWarning(
                        "The live approval {InstanceId} of task {TaskId} has no open task in the engine and no key to complete it "
                        + "with: it is neither adopted nor replaced.", latest.Id, task.Id);
                    return null;
                }

                halfStartKey = latest.IdempotencyKey;
            }

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
                // Idempotent per ROUND (D2-FIX5 item 3): a retried start must not open a second approval for the same work, and
                // a new round after a closed one must not replay it. The first round's key is the task's own, unchanged.
                IdempotencyKey: halfStartKey
                    ?? $"task-approval:{_tenantContext.TenantId}:{task.Id}" + (latest is null ? string.Empty : $":{latest.Id}"),
                CommentRequired: false,
                EvidenceRequired: false,
                DueAt: task.DueAt);

            var response = await _mediator.Send(
                new StartWorkflowInstanceCommand(request, CorrelationId()), ct);

            if (halfStartKey is not null && maySupersede && IsRefusedReplay(response))
            {
                return await SupersedeAndRestartAsync(task, latest!.Id, ct);
            }

            if (!response.IsSuccessful || response.Data is null)
            {
                _logger.LogWarning(
                    "Could not start task approval for {TaskId}: {ReasonCode} {Message}. The task is kept and "
                    + "`start` stays gated until the approval is retried.",
                    task.Id, response.ReasonCode, string.Join("; ", response.Errors));
                return null;
            }

            /*
             * D2-FIX5 item 3 — NEVER LINK A ROUND THE ENGINE HAS ALREADY CLOSED. A key that resolves to an existing instance
             * answers with that instance whatever its status; an approval that just opened is Pending or Active (never
             * Escalated, Rejected, TimedOut or Cancelled), so any other status is a replay of a dead round and the task would
             * wait on it for ever. Not linked: the task is kept, `start` stays gated, and the edit can be repeated.
             */
            if (Enum.TryParse<WorkflowInstanceStatus>(response.Data.Status, out var opened) && !IsOpen(opened))
            {
                _logger.LogWarning(
                    "Starting task approval for {TaskId} answered a {Status} instance {InstanceId}; it is not linked.",
                    task.Id, response.Data.Status, response.Data.WorkflowInstanceId);
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

    /// <summary>The engine refused the replay of a half start's key: the facts or the person changed (409 start-conflict).</summary>
    internal static bool IsRefusedReplay(Common.Response<Workflow.StartWorkflowInstanceResponse> response) =>
        !response.IsSuccessful
        && response.StatusCode == 409
        && response.ReasonCode == WorkflowReasonCodes.WorkflowInstanceStartConflict;

    /// <summary>
    /// FIX2 item 5 — CLOSE AND RESTART. The half start the edited request cannot complete is closed through the engine's taskless
    /// close (named reason, log, event, audit row); the latest round is then closed, so the start below keys on it and opens a
    /// new round with the facts as they are now. Once only: a second refusal is reported, never looped on.
    /// </summary>
    private async Task<Guid?> SupersedeAndRestartAsync(TaskItem task, Guid halfStartId, CancellationToken ct)
    {
        var closed = await _mediator.Send(new SupersedeWorkflowHalfStartCommand(halfStartId, CorrelationId()), ct);
        if (!closed.IsSuccessful)
        {
            _logger.LogWarning(
                "workflow.half_start.supersede_failed Task={TaskId} Instance={InstanceId} Reason={ReasonCode}: the half start "
                + "could not be closed; the task is kept and `start` stays gated.",
                task.Id, halfStartId, closed.ReasonCode);
            return null;
        }

        _logger.LogInformation(
            "workflow.half_start.superseded Task={TaskId} Instance={InstanceId}: its facts or its starter changed, so it was "
            + "closed and a new approval round is started.", task.Id, halfStartId);
        return await TryStartApprovalAsync(task, maySupersede: false, ct);
    }

    public async Task<bool> CancelApprovalAsync(TaskItem task, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);

        try
        {
            /*
             * D2-FIX3 item 8 — NO LINK IS NOT "NOTHING RUNNING". A start whose link write fell over (UpdateTaskItemHandler
             * logs it and moves on) leaves a live instance the task does not point at; asked with no link, this used to
             * answer "nothing open" and the approver kept a decision nobody reads. The instance is found the way the gate
             * finds it — the LATEST one under the task's own object reference.
             *
             * D2-FIX4 item 1 — AND THE LATEST IS ASKED WHATEVER THE LINK SAYS. A link that is held can still be STALE: it
             * names an earlier instance while a later one (the same object reference) is the live one the gate reads. The
             * withdraw used to look only at the link, found it closed, and answered "nothing open" — the later instance
             * stayed decidable with nobody reading it. Now the linked instance AND the latest under the reference are both
             * looked at; a latest that differs from the link and is still open is withdrawn too, and the link moves to it.
             */
            var linked = task.WorkflowInstanceId;
            var latest = (await _instances.GetLatestByObjectRefAsync(
                BuildObjectRef(task.Id), ApprovalObjectType, task.Id.ToString(), ct))?.Id;
            var instanceIds = new List<Guid>(2);
            if (linked is { } linkedId)
            {
                instanceIds.Add(linkedId);
            }

            if (latest is { } latestId && latestId != linked)
            {
                instanceIds.Add(latestId);
            }

            foreach (var instanceId in instanceIds)
            {
                // Cancel through MOD-0023's OWN command so its audit trail and state machine stay authoritative;
                // MOD-0024 never edits workflow state directly.
                // D2-FIX1 A1 — every task still waiting on somebody, ESCALATED included: an escalated approval is just as
                // open as a waiting one (the timesheet withdraw's rule, F8).
                var pending = await OpenTasksAsync(instanceId, ct);

                // One that is not the task's link and is still OPEN is ADOPTED as the link, before its withdraw: a withdraw
                // that fails leaves a link a retry can follow (the caller decides whether to store it). One already closed
                // is not — nothing needs withdrawing, and it was never this task's live approval.
                if (instanceId != task.WorkflowInstanceId && pending.Count > 0)
                {
                    task.WorkflowInstanceId = instanceId;
                }

                foreach (var approvalTask in pending)
                {
                    var result = await _mediator.Send(new CancelWorkflowTaskCommand(
                        approvalTask.Id,
                        new Workflow.CancelWorkflowTaskRequest(
                            ActorId: _currentUser.UserId.ToString(),
                            ReasonCode: "APPROVAL_NO_LONGER_REQUIRED",
                            // Idempotent per approval task: a retried cancel must not double-cancel.
                            IdempotencyKey: $"task-approval-cancel:{approvalTask.Id}",
                            Comment: null),
                        CorrelationId(),
                        // D2-FIX1 A1 — Tasks OWNS the object (as the timesheet does): its own withdraw may call off an
                        // approval that was ever escalated. The public cancel endpoint never sets this.
                        AllowEscalated: true), ct);

                    if (!result.IsSuccessful)
                    {
                        _logger.LogWarning(
                            "Could not cancel approval task {ApprovalTaskId} for task {TaskId} ({ReasonCode}); the link is kept.",
                            approvalTask.Id, task.Id, result.ReasonCode);
                        return false;
                    }
                }
            }

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not cancel the approval for task {TaskId}; the link is kept.", task.Id);
            return false;
        }
    }

    public async Task<Guid?> FindUnlinkedOpenInstanceAsync(TaskItem task, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);

        var latest = (await _instances.GetLatestByObjectRefAsync(
            BuildObjectRef(task.Id), ApprovalObjectType, task.Id.ToString(), ct))?.Id;
        return latest is { } id && id != task.WorkflowInstanceId && (await OpenTasksAsync(id, ct)).Count > 0 ? id : null;
    }

    /// <summary>An instance somebody can still decide: opening (Pending), running (Active) or escalated.</summary>
    private static bool IsOpen(WorkflowInstanceStatus status) =>
        status is WorkflowInstanceStatus.Pending or WorkflowInstanceStatus.Active or WorkflowInstanceStatus.Escalated;

    /// <summary>The approval tasks of an instance that are still waiting on somebody — what "open" means for a withdraw.</summary>
    private async Task<List<ApprovalTask>> OpenTasksAsync(Guid instanceId, CancellationToken ct) =>
        (await _approvalTasks.ListByInstanceIdAsync(instanceId, ct))
            .Where(t => t.Status is ApprovalTaskStatus.WaitingApproval or ApprovalTaskStatus.WaitingEvidence
                or ApprovalTaskStatus.Escalated)
            .ToList();

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
                    // B1 — MOD-0023 closes an approved instance as Completed; Approved alone missed every real one.
                    IsApproved: WorkflowInstanceOutcome.IsApproved(instance.Status),
                    IsRejected: WorkflowInstanceOutcome.IsRejected(instance.Status),
                    IsCancelled: instance.Status == WorkflowInstanceStatus.Cancelled,
                    IsTimedOut: instance.Status == WorkflowInstanceStatus.TimedOut));
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
