using Diten.Platform.Application.Common;
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

public interface ITaskReviewService
{
    /// <summary>
    /// Start the review for a task. Returns the instance id, or null when it could not be started — in which case
    /// the caller keeps the task un-submitted, so nothing claims to be under review that is not.
    /// D2-FIX5 item 2 — a live review is adopted only when the engine holds an open task for it; one whose start never
    /// completed is neither adopted nor replaced (null, with the engine's start-conflict code as the reason).
    /// </summary>
    Task<Guid?> TryStartReviewAsync(TaskItem task, CancellationToken ct);

    /// <summary>
    /// D2-FIX1 — the same start, and when it was refused, the ENGINE's reason (e.g. nobody but the submitter could
    /// review it). The submit path uses this so "no eligible reviewer" is not shown as "try again".
    /// </summary>
    async Task<TaskReviewStartResult> StartReviewAsync(TaskItem task, CancellationToken ct) =>
        new(await TryStartReviewAsync(task, ct), null);

    /// <summary>
    /// Cancel a task's review — used when the requirement is switched back off. Same contract as the approval's:
    /// false means an open review task could NOT be cancelled and the caller keeps the link (D2-FIX1 A1).
    /// D2-FIX3 item 8 — a task with NO link is looked up by its review object reference (a submission whose link write
    /// lost its race after the engine started the review); an instance found that way becomes the task's link.
    /// </summary>
    Task<bool> CancelReviewAsync(TaskItem task, CancellationToken ct);

    /// <summary>
    /// D2-FIX4 items 1 and 4 — the live review the task does NOT point at, read-only: the LATEST instance under the review
    /// object reference when it is still open and is not the task's link; null otherwise. See
    /// <see cref="ITaskApprovalService.FindUnlinkedOpenInstanceAsync"/> — the edit adopts it before its write so the link is
    /// stored with the flag change. Throws when the engine cannot be read. The default (in-memory doubles) finds nothing.
    /// </summary>
    Task<Guid?> FindUnlinkedOpenInstanceAsync(TaskItem task, CancellationToken ct) => Task.FromResult<Guid?>(null);
}

/// <summary>D2-FIX1 — a review start: the instance, or why MOD-0023 refused it (null when the reason is unknown).</summary>
public sealed record TaskReviewStartResult(Guid? InstanceId, string? RefusalReasonCode);

/// <summary>
/// Turns MOD-0023's reported state into the two flags MOD-0024 renders a REVIEW from — the exact counterpart of
/// <see cref="TaskApprovalView"/>, and separate from it because the two decisions are read from different
/// instances and gate different acts (approval gates `start`, review gates `complete`).
/// </summary>
public static class TaskReviewView
{
    /// <summary>
    /// <paramref name="outstanding"/> — MOD-0023 still owes a review verdict, so completion stays shut.
    /// Fail-closed, for the same reason approval is: a review required with no instance (a failed start) or an
    /// unreadable one counts as outstanding, because reporting it as released would let unreviewed work close.
    /// <paramref name="rejected"/> — the reviewer sent it back; the work returns to InProgress.
    /// </summary>
    public static (bool Outstanding, bool Rejected) Resolve(
        TaskItem task,
        IReadOnlyDictionary<Guid, TaskApprovalState> states)
    {
        ArgumentNullException.ThrowIfNull(task);
        ArgumentNullException.ThrowIfNull(states);

        // No review asked for and no link held, or the task is already closed: nobody is owed anything.
        // D2-FIX3 item 3 — the gate's own rule (TaskWorkflowLinkGate): the requirement OR a held link.
        if (!TaskWorkflowLinkGate.AsksReview(task) || task.Lifecycle is TaskLifecycle.Done or TaskLifecycle.Cancelled)
        {
            return (false, false);
        }

        if (!task.ReviewRequired)
        {
            // Requirement withdrawn, link held (a withdraw that failed): the review holds completion until MOD-0023
            // reports it closed, as the transition gate does. Nobody can resubmit work that needs no review, so a
            // refusal is never read as "sent back" here.
            states.TryGetValue(task.ReviewWorkflowInstanceId!.Value, out var held);
            return (Outstanding: TaskWorkflowLinkGate.Holds(held, required: false), Rejected: false);
        }

        /*
         * A review that has not been SUBMITTED yet is not outstanding — unlike approval, which is owed from the
         * moment the task is created. Review is requested by the holder when the work is ready, so before
         * submission there is no instance and nothing is waiting; `complete` is gated by the submission step
         * itself, not by a verdict nobody has been asked for.
         */
        if (task.ReviewWorkflowInstanceId is not { } instanceId)
        {
            return (task.Lifecycle == TaskLifecycle.PendingReview, false);
        }

        if (!states.TryGetValue(instanceId, out var state))
        {
            return (true, false);
        }

        /*
         * D2-FIX3 item 2 — a review MOD-0023 reports CANCELLED was WITHDRAWN (a hand-over or a half-finished cancel took it
         * back): nobody is reviewing the work, and it is the holder's to submit again — exactly what a refusal means here.
         * Read as outstanding (as before), it showed "waiting for the reviewer" with nobody to wait on and offered no way on.
         */
        /*
         * D2-FIX4 item 2 — a review the SLA sweep TIMED OUT is closed without a verdict: nobody is reviewing the work any
         * more, so it is the holder's to submit again, exactly like a withdrawn one. Read as outstanding (it is neither
         * approved nor rejected) it held `complete` shut for good and offered no way on — and, after a hand-over, the next
         * holder's submission was refused REVIEW_PENDING for ever.
         */
        var sentBack = state.IsRejected || state.IsCancelled || state.IsTimedOut;
        return (Outstanding: !state.IsApproved && !sentBack, Rejected: sentBack);
    }

    /// <summary>
    /// D2-FIX3 — the linked review was withdrawn rather than refused (both read as <c>Rejected</c> in
    /// <see cref="Resolve"/>). Only the review gate's chip tells the two apart: a withdrawn review is "required" again,
    /// never "rejected" — nobody refused anything. D2-FIX4 item 2 — a review that timed out is the same: closed without a
    /// verdict, so "required" again.
    /// </summary>
    public static bool Withdrawn(TaskItem task, IReadOnlyDictionary<Guid, TaskApprovalState> states) =>
        task.ReviewRequired
        && task.Lifecycle is not (TaskLifecycle.Done or TaskLifecycle.Cancelled)
        && task.ReviewWorkflowInstanceId is { } instanceId
        && states.TryGetValue(instanceId, out var state)
        && (state.IsCancelled || state.IsTimedOut) && !state.IsRejected && !state.IsApproved;
}

/// <summary>Operator-overridable settings for the review handoff.</summary>
public sealed class TaskReviewOptions
{
    public const string SectionName = "Tasks:Review";

    /// <summary>
    /// The workflow template MOD-0024 starts for task review. A tenant that designs its own flow points this at
    /// their template code; the default below is only a fallback so the toggle works on day one.
    /// </summary>
    public string TemplateCode { get; set; } = "task-review";

    public string TemplateName { get; set; } = "Task review";
}

/// <summary>
/// Faz 3b — review is MOD-0023's SECOND decision on a task, not a second engine.
///
/// <para><b>The rule, unchanged from approval (charter Binding A).</b> MOD-0024 owns no review engine, no review
/// status and no reviewer authority. It starts a MOD-0023 instance, keeps the instance id as the ONLY link, and
/// asks MOD-0023 for the state whenever it needs it. There is deliberately no "review status" field on
/// <see cref="Diten.Platform.Domain.Entities.Tasks.TaskItem"/> — the status is the instance's; the FLAG
/// (<c>ReviewRequired</c>) is MOD-0024's, and only the OUTCOME lands in MOD-0024's own lifecycle.</para>
///
/// <para><b>Why this type exists at all: the identity collision.</b> A task's gate finds its instance through
/// <c>GetLatestByObjectRefAsync</c>, which returns the LATEST instance for one object reference. Approval and
/// review are two live decisions on the SAME task, so sharing a reference would make each gate read whichever
/// started last — an approved task reporting "waiting for review", or the reverse. The two are therefore separate
/// OBJECT REFERENCES rather than separate engines.</para>
///
/// <para><b>Why the approval side is untouched.</b> Approval keeps the exact ObjectType and ObjectRef it has
/// always used, so every instance already in the database stays reachable and no migration is needed. Review adds
/// a NEW reference beside it. Changing approval's reference to something symmetrical like "task-approval" would
/// have been tidier and would have orphaned every historical approval instance — reachability of existing records
/// beats symmetry.</para>
///
/// <para>The workflow engine stores ObjectType as free text and validates it against no allowlist, so this needs
/// no MOD-0023 change: none of its files are modified.</para>
/// </summary>
public sealed class TaskReviewService : ITaskReviewService
{
    /// <summary>
    /// The object type MOD-0024 presents to the workflow engine for a REVIEW decision, and the review gate's
    /// ObjectType. Deliberately NOT <c>"task"</c> — see the type's own summary for why sharing it corrupts both
    /// gates.
    /// </summary>
    public const string ReviewObjectType = "task-review";

    /// <summary>
    /// MOD-0024's review object reference in the workflow engine's vocabulary.
    ///
    /// <para>Mirrors <c>TaskApprovalService.BuildObjectRef</c> in shape so the two read as siblings, and differs
    /// from it in the one segment that keeps their instance histories disjoint.</para>
    /// </summary>
    public static string BuildObjectRef(Guid taskId) => $"tasks|{ReviewObjectType}|{taskId}";

    private readonly IMediator _mediator;
    private readonly IWorkflowTemplateRepository _templates;
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IApprovalTaskRepository _approvalTasks;
    private readonly ITenantContext _tenantContext;
    private readonly Contracts.ICurrentUserContext _currentUser;
    private readonly TaskReviewOptions _options;
    private readonly ILogger<TaskReviewService> _logger;

    public TaskReviewService(
        IMediator mediator,
        IWorkflowTemplateRepository templates,
        // D2-FIX3 item 8 — REQUIRED, in the approval service's place: a review whose link was never stored is found by its
        // object reference (CancelReviewAsync). Optional would compile without it and quietly find nothing.
        IWorkflowInstanceRepository instances,
        IApprovalTaskRepository approvalTasks,
        ITenantContext tenantContext,
        Contracts.ICurrentUserContext currentUser,
        IOptions<TaskReviewOptions> options,
        ILogger<TaskReviewService> logger)
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

    public async Task<Guid?> TryStartReviewAsync(TaskItem task, CancellationToken ct) =>
        (await StartReviewAsync(task, ct)).InstanceId;

    public Task<TaskReviewStartResult> StartReviewAsync(TaskItem task, CancellationToken ct) =>
        StartReviewAsync(task, maySupersede: true, ct);

    private async Task<TaskReviewStartResult> StartReviewAsync(TaskItem task, bool maySupersede, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);

        try
        {
            /*
             * D2-FIX4 item 5 — THE ROUND IS NAMED BY THE ENGINE'S OWN LAST ROUND, not only by the task's link. The key names
             * the instance being replaced; read from the link alone, a link that was lost (a withdraw whose write fell over,
             * a requirement switched off and on, a hand-over that lost its race) fell back to "initial" — the FIRST round's
             * key — so the submission replayed round one, which was long closed, and the task was "successfully" linked to a
             * cancelled instance: nobody reviewing, nothing to tell the holder. The latest instance under the review object
             * reference is the engine's record of the chain and cannot be lost with a link:
             *   • it is still OPEN — the live round whose link was never stored: it is the task's review, adopted and not
             *     started a second time (one live review per task);
             *   • it is closed — the new round replaces IT: key {latest}, whatever the link says;
             *   • there is none — the link (an instance the engine no longer lists) or "initial".
             * With the link intact the latest IS the link, so every key a start has ever carried is unchanged: a crash
             * between the start and the link write still resolves to one instance, and nothing stored is re-keyed.
             */
            var latest = await _instances.GetLatestByObjectRefAsync(
                BuildObjectRef(task.Id), ReviewObjectType, task.Id.ToString(), ct);
            string? halfStartKey = null;
            if (latest is not null && IsOpen(latest.Status))
            {
                /*
                 * D2-FIX5 item 2 — ADOPTED ONLY WHEN THE ENGINE HOLDS AN OPEN TASK FOR IT. The generic start writes the instance,
                 * then its task, then the assignment snapshot — three separate writes. A crash after the first leaves an ACTIVE
                 * instance that nobody has been asked to decide; adopting it on the instance's status alone moved the task to
                 * PendingReview behind a review no reviewer would ever see. The same question the withdraw and the edit's
                 * look-up ask of an instance before they take it (OpenTasksAsync): at least one task still waiting on somebody.
                 * Without one the start is INCOMPLETE — the answer a replayed key gave before adoption existed (409, the
                 * engine's own start-conflict code, which the submission maps to "could not be started, try again"): no round is
                 * adopted and none is started beside it, and once the engine holds its task the same call adopts it.
                 */
                if ((await OpenTasksAsync(latest.Id, ct)).Count > 0)
                {
                    return new(latest.Id, null);
                }

                /*
                 * WP-WF-ENGINE-RECOVERY-01 FIX1 item 3 — A HALF START IS COMPLETED, NOT WAITED ON. The engine completes a generic
                 * start whose task was never written when the SAME key is replayed with the same facts (BL-549). So the start
                 * below is replayed under the round's own key: the engine writes its task and this call adopts the round. Facts
                 * that changed meanwhile (another reviewer, another starter) get the engine's 409; FIX2 item 5 — the half start is
                 * then superseded (closed) and a new round started, below.
                 */
                if (string.IsNullOrWhiteSpace(latest.IdempotencyKey))
                {
                    _logger.LogWarning(
                        "The live review {InstanceId} of task {TaskId} has no open task in the engine and no key to complete it "
                        + "with: it is neither adopted nor replaced.", latest.Id, task.Id);
                    return new(null, Workflow.WorkflowReasonCodes.WorkflowInstanceStartConflict);
                }

                halfStartKey = latest.IdempotencyKey;
            }

            var templateId = await EnsureTemplateAsync(ct);
            if (templateId is null)
            {
                return new(null, null);
            }

            var request = new Workflow.StartWorkflowInstanceRequest(
                TemplateId: templateId,
                TemplateCode: null,
                // The review's OWN object identity — this is what keeps the approval gate from reading this
                // instance and vice versa. See the type summary.
                ObjectType: ReviewObjectType,
                ObjectId: task.Id.ToString(),
                ObjectRef: BuildObjectRef(task.Id),
                // A CANDIDATE hint only, on the same terms as approval's manager: MOD-0023/MOD-0018 resolve who
                // may actually review (RuntimeAssignmentSnapshot). Passing a suggestion is not naming a decider,
                // and MOD-0024 never decides authority.
                CandidatePrincipalIds: task.ReviewerCandidateUserId is { } reviewer && reviewer != Guid.Empty
                    ? [reviewer.ToString()]
                    : [],
                ReasonCode: null,
                /*
                 * Idempotent per ROUND, not per task. (Since D2-FIX5 approval keys the same way: its first round keeps
                 * the task-only key, a restart after a closed round keys on that round.)
                 *
                 * A refused review sends the work back to be redone and resubmitted, so a task can legitimately
                 * need a SECOND instance. Keying on the task alone would hand the retry the refused instance back
                 * and the second round could never start. The key therefore names the instance being replaced:
                 * the same round always produces the same key (so a crash between starting the workflow and
                 * storing the link still resolves to one instance), and a new round after a refusal produces a
                 * different one.
                 */
                IdempotencyKey: halfStartKey ?? $"task-review:{_tenantContext.TenantId}:{task.Id}:"
                    + ((latest?.Id ?? task.ReviewWorkflowInstanceId)?.ToString() ?? "initial"),
                CommentRequired: false,
                EvidenceRequired: false,
                DueAt: task.DueAt);

            var response = await _mediator.Send(
                new StartWorkflowInstanceCommand(request, CorrelationId()), ct);

            if (halfStartKey is not null && maySupersede && TaskApprovalService.IsRefusedReplay(response))
            {
                return await SupersedeAndRestartAsync(task, latest!.Id, ct);
            }

            if (!response.IsSuccessful || response.Data is null)
            {
                _logger.LogWarning(
                    "Could not start task review for {TaskId}: {ReasonCode} {Message}. The task stays in progress "
                    + "rather than claiming to be under a review that was never opened.",
                    task.Id, response.ReasonCode, string.Join("; ", response.Errors));
                return new(null, response.ReasonCode);
            }

            /*
             * D2-FIX4 item 5 — NEVER LINK A ROUND THE ENGINE HAS ALREADY CLOSED. A key that resolves to an existing instance
             * answers with that instance whatever its status; a replay that found a cancelled one used to be reported as a
             * started review. A review that just opened is Pending or Active (never Escalated, Rejected, TimedOut or
             * Cancelled), so any other status is a replay of a dead round: the submission is refused, not recorded.
             */
            if (Enum.TryParse<WorkflowInstanceStatus>(response.Data.Status, out var opened) && !IsOpen(opened))
            {
                _logger.LogWarning(
                    "Starting task review for {TaskId} answered a {Status} instance {InstanceId}; it is not linked.",
                    task.Id, response.Data.Status, response.Data.WorkflowInstanceId);
                return new(null, null);
            }

            return new(response.Data.WorkflowInstanceId, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Starting task review for {TaskId} threw; the task stays in progress.", task.Id);
            return new(null, null);
        }
    }

    /// <summary>FIX2 item 5 — close the half start the edited submission cannot complete, then start a new round (once).
    /// See <see cref="TaskApprovalService"/>'s twin.</summary>
    private async Task<TaskReviewStartResult> SupersedeAndRestartAsync(TaskItem task, Guid halfStartId, CancellationToken ct)
    {
        var closed = await _mediator.Send(new Workflow.Commands.SupersedeWorkflowHalfStartCommand(halfStartId, CorrelationId()), ct);
        if (!closed.IsSuccessful)
        {
            _logger.LogWarning(
                "workflow.half_start.supersede_failed Task={TaskId} Instance={InstanceId} Reason={ReasonCode}: the half start "
                + "could not be closed; the task stays in progress.",
                task.Id, halfStartId, closed.ReasonCode);
            return new(null, Workflow.WorkflowReasonCodes.WorkflowInstanceStartConflict);
        }

        _logger.LogInformation(
            "workflow.half_start.superseded Task={TaskId} Instance={InstanceId}: its facts or its submitter changed, so it was "
            + "closed and a new review round is started.", task.Id, halfStartId);
        return await StartReviewAsync(task, maySupersede: false, ct);
    }

    /// <summary>An instance somebody can still decide: opening (Pending), running (Active) or escalated.</summary>
    private static bool IsOpen(WorkflowInstanceStatus status) =>
        status is WorkflowInstanceStatus.Pending or WorkflowInstanceStatus.Active or WorkflowInstanceStatus.Escalated;

    public async Task<bool> CancelReviewAsync(TaskItem task, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);

        try
        {
            // D2-FIX3 item 8 — no link is not "no review": a submission that lost its race after the engine started the
            // review never stored one. Found the way the gate finds it (the LATEST instance under the review reference).
            // D2-FIX4 item 1 — and the latest is asked WHATEVER the link says: a link left on round one while round two
            // (a resubmission whose task write lost its race) is the live review made the withdraw look at the closed
            // round one only and report "nothing open". The linked instance AND a different latest are both withdrawn.
            var linked = task.ReviewWorkflowInstanceId;
            var latest = (await _instances.GetLatestByObjectRefAsync(
                BuildObjectRef(task.Id), ReviewObjectType, task.Id.ToString(), ct))?.Id;
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
                // Through MOD-0023's OWN command so its audit trail and state machine stay authoritative;
                // MOD-0024 never edits workflow state directly. D2-FIX1 A1 — escalated review tasks are open too.
                var pending = await OpenTasksAsync(instanceId, ct);

                // A review that is not the task's link and is still OPEN is adopted as the link, before its withdraw: a
                // withdraw that fails leaves a link a retry can follow, and the next round's key names it. One already
                // closed is not adopted — it was never this task's live review, and nothing about it needs withdrawing.
                if (instanceId != task.ReviewWorkflowInstanceId && pending.Count > 0)
                {
                    task.ReviewWorkflowInstanceId = instanceId;
                }

                foreach (var reviewTask in pending)
                {
                    var result = await _mediator.Send(new CancelWorkflowTaskCommand(
                        reviewTask.Id,
                        new Workflow.CancelWorkflowTaskRequest(
                            ActorId: _currentUser.UserId.ToString(),
                            ReasonCode: "REVIEW_NO_LONGER_REQUIRED",
                            // Idempotent per review task: a retried cancel must not double-cancel.
                            IdempotencyKey: $"task-review-cancel:{reviewTask.Id}",
                            Comment: null),
                        CorrelationId(),
                        // D2-FIX1 A1 — the owner's own withdraw (see the approval's cancel).
                        AllowEscalated: true), ct);

                    if (!result.IsSuccessful)
                    {
                        _logger.LogWarning(
                            "Could not cancel review task {ReviewTaskId} for task {TaskId} ({ReasonCode}); the link is kept.",
                            reviewTask.Id, task.Id, result.ReasonCode);
                        return false;
                    }
                }
            }

            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not cancel the review for task {TaskId}; the link is kept.", task.Id);
            return false;
        }
    }

    public async Task<Guid?> FindUnlinkedOpenInstanceAsync(TaskItem task, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(task);

        var latest = (await _instances.GetLatestByObjectRefAsync(
            BuildObjectRef(task.Id), ReviewObjectType, task.Id.ToString(), ct))?.Id;
        return latest is { } id && id != task.ReviewWorkflowInstanceId && (await OpenTasksAsync(id, ct)).Count > 0 ? id : null;
    }

    /// <summary>The review tasks of an instance that are still waiting on somebody — what "open" means for a withdraw.</summary>
    private async Task<List<ApprovalTask>> OpenTasksAsync(Guid instanceId, CancellationToken ct) =>
        (await _approvalTasks.ListByInstanceIdAsync(instanceId, ct))
            .Where(t => t.Status is ApprovalTaskStatus.WaitingApproval or ApprovalTaskStatus.WaitingEvidence
                or ApprovalTaskStatus.Escalated)
            .ToList();

    /// <summary>
    /// Find (or install) the tenant's task-review template, through the SAME lazy, idempotent path the approval
    /// template uses — no new flow is designed here, only the same single-step shape under its own code.
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
                Description: "Default single-step task review, installed by MOD-0024. "
                             + "Replace or re-point it in the Workflow Designer to change the flow."),
            CorrelationId()), ct);

        if (!created.IsSuccessful || created.Data is null)
        {
            // A concurrent installer most likely won the race on the unique template code — adopt its template.
            var winner = await _templates.GetByTemplateCodeAsync(_options.TemplateCode, ct);
            if (winner is null)
            {
                _logger.LogWarning(
                    "Could not install the task-review template ({TemplateCode}): {ReasonCode}",
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
          "name": "Task review",
          "steps": [
            {
              "code": "review",
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
                PublishReason: "MOD-0024 default task review"),
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
            "Could not publish the task-review template {TemplateId}: {ReasonCode}", templateId, published.ReasonCode);
        return null;
    }

    private string CorrelationId() => $"task-review:{Guid.NewGuid():N}";
}
