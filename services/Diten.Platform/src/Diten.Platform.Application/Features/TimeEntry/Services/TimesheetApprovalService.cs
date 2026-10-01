using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using WorkflowContracts = Diten.Platform.Application.Features.Workflow;

namespace Diten.Platform.Application.Features.TimeEntry.Services;

/// <summary>MOD-0023's outcome for one instance, as this module reads it. Never stored as "the" decision — the
/// finalizer copies what it needs onto the week and MOD-0023 stays the owner.</summary>
public enum TimesheetDecisionOutcome
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3,
    Missing = 4
}

public sealed record TimesheetDecision(
    TimesheetDecisionOutcome Outcome,
    Guid? ActorUserId,
    string? Comment,
    DateTimeOffset? DecidedAtUtc);

/// <summary>What a start answered. <see cref="WorkflowInstanceId"/> is set only for an instance that is OPEN
/// (Active) — a closed one handed back for a reused key is never adopted (F1). <see cref="AssignedApproverUserId"/>
/// is the one person MOD-0023 actually assigned.</summary>
public sealed record TimesheetApprovalStart(Guid? WorkflowInstanceId, Guid? AssignedApproverUserId, string? ReasonCode);

public interface ITimesheetApprovalService
{
    /// <summary>Starts the MOD-0023 instance for this submission (idempotent per week + revision + submission number).</summary>
    Task<TimesheetApprovalStart> StartAsync(
        TimesheetWeek week, IReadOnlyList<Guid> candidateUserIds, int submissionNumber, CancellationToken ct = default);

    /// <summary>Cancels a not-yet-closed instance through MOD-0023's own cancel command — every approval task that is
    /// still open, escalated ones included (F8). True when nothing is left open and nothing was decided; FALSE when the
    /// instance is already decided (F13) or MOD-0023 refused — the decision got there first.</summary>
    Task<bool> CancelAsync(Guid workflowInstanceId, Guid actorUserId, CancellationToken ct = default);

    /// <summary>F1 — an earlier submit that started its instance but never recorded it on the week leaves that instance
    /// open in the approver's inbox. Before a new submission starts, the instance of <paramref name="submissionNumber"/>
    /// (if it exists, is still open and is not the week's own) is cancelled, so it can never be approved.</summary>
    Task RetireSubmissionAsync(TimesheetWeek week, int submissionNumber, Guid actorUserId, CancellationToken ct = default);

    Task<TimesheetDecision> ReadDecisionAsync(Guid workflowInstanceId, CancellationToken ct = default);

    /// <summary>
    /// BL-484 — the OUTCOME of several instances in ONE read, keyed by instance id: exactly the
    /// <see cref="TimesheetDecision.Outcome"/> <see cref="ReadDecisionAsync"/> answers for each (an instance that does not
    /// exist in this tenant is <see cref="TimesheetDecisionOutcome.Missing"/>). Who decided and why is NOT read here —
    /// the finalizer reads that for the one week it applies.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, TimesheetDecisionOutcome>> ReadOutcomesAsync(
        IReadOnlyCollection<Guid> workflowInstanceIds, CancellationToken ct = default);
}

/// <summary>
/// MOD-0280-FU01 D6 / R4 / R5 — the approval handoff to MOD-0023, the same shape as <c>TaskApprovalService</c>.
///
/// <para><b>This module decides nothing.</b> It installs its definition, starts an instance with the candidates it
/// resolved, cancels it on withdraw, and READS the outcome back. There is no approve or reject endpoint here; those
/// are MOD-0023's own actions (<c>platform.workflow.tasks.approve</c> / <c>.reject</c>).</para>
///
/// <para><b>The definition carries "comment required on reject".</b> MOD-0023 enforces it in its reject path
/// (R5); every other definition keeps it off.</para>
/// </summary>
public sealed class TimesheetApprovalService : ITimesheetApprovalService
{
    /// <summary>One approval step; the candidates come from the start request. The root <c>options</c> block is the
    /// per-definition switch MOD-0023 reads on reject.</summary>
    internal const string DefinitionJson = """
    {
      "name": "Timesheet approval",
      "options": { "rejectCommentRequired": true },
      "stages": [
        {
          "code": "stage-1",
          "steps": [
            {
              "code": "approve",
              "name": "Line manager approval",
              "type": "approval",
              "assignment": { "mode": "candidates" }
            }
          ]
        }
      ]
    }
    """;

    private readonly IMediator _mediator;
    private readonly IWorkflowTemplateRepository _templates;
    private readonly IWorkflowInstanceRepository _instances;
    private readonly IApprovalTaskRepository _approvalTasks;
    private readonly IWorkflowTransitionLogRepository _transitions;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<TimesheetApprovalService> _logger;

    public TimesheetApprovalService(
        IMediator mediator,
        IWorkflowTemplateRepository templates,
        IWorkflowInstanceRepository instances,
        IApprovalTaskRepository approvalTasks,
        IWorkflowTransitionLogRepository transitions,
        ITenantContext tenantContext,
        ILogger<TimesheetApprovalService> logger)
    {
        _mediator = mediator;
        _templates = templates;
        _instances = instances;
        _approvalTasks = approvalTasks;
        _transitions = transitions;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    /// <summary>
    /// Idempotency key <c>timesheet-week:{tenant}:{weekId}:{revision}:{submission}</c>. The pack's key stops at the
    /// revision; the submission number is added because a revision can be submitted, withdrawn and submitted again,
    /// and MOD-0023 hands back the SAME (closed) instance for a key it has seen.
    /// </summary>
    public static string IdempotencyKey(Guid tenantId, TimesheetWeek week, int submissionNumber)
        => $"timesheet-week:{tenantId}:{week.Id}:{week.RevisionNumber}:{submissionNumber}";

    public static string ObjectRef(Guid weekId) => $"time-entry|{TimeEntryModule.ApprovalObjectType}|{weekId}";

    public async Task<TimesheetApprovalStart> StartAsync(
        TimesheetWeek week, IReadOnlyList<Guid> candidateUserIds, int submissionNumber, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(week);

        var templateId = await EnsureTemplateAsync(ct);
        if (templateId is null)
        {
            return new TimesheetApprovalStart(null, null, TimeEntryReasonCodes.ApprovalStartFailed);
        }

        var response = await _mediator.Send(new StartWorkflowInstanceCommand(
            new WorkflowContracts.StartWorkflowInstanceRequest(
                TemplateId: templateId,
                TemplateCode: null,
                ObjectType: TimeEntryModule.ApprovalObjectType,
                ObjectId: week.Id.ToString(),
                ObjectRef: ObjectRef(week.Id),
                CandidatePrincipalIds: candidateUserIds.Select(id => id.ToString()).ToList(),
                ReasonCode: null,
                IdempotencyKey: IdempotencyKey(_tenantContext.TenantId, week, submissionNumber),
                // requirements.commentRequired would demand a comment on APPROVE too; the reject-only rule is the
                // definition's own option (see DefinitionJson).
                CommentRequired: false,
                EvidenceRequired: false,
                DueAt: null),
            CorrelationId()), ct);

        if (!response.IsSuccessful || response.Data is null)
        {
            _logger.LogWarning(
                "Timesheet approval could not start for week {WeekId}: {ReasonCode}. The week stays Draft.",
                week.Id, response.ReasonCode);
            return new TimesheetApprovalStart(null, null, response.ReasonCode ?? TimeEntryReasonCodes.ApprovalStartFailed);
        }

        // MOD-0023 hands back the instance it already has for a key it has seen. If that instance is closed — decided
        // or cancelled — it belongs to OTHER content, and adopting it would apply an old decision to this week.
        var instance = await _instances.GetByIdAsync(response.Data.WorkflowInstanceId, ct);
        if (instance is null || instance.Status != WorkflowInstanceStatus.Active)
        {
            _logger.LogWarning(
                "Timesheet approval start for week {WeekId} returned instance {InstanceId} in state {Status}; not adopted.",
                week.Id, response.Data.WorkflowInstanceId, instance?.Status);
            return new TimesheetApprovalStart(null, null, TimeEntryReasonCodes.ApprovalInstanceClosed);
        }

        var assigned = await _approvalTasks.GetActiveByInstanceIdAsync(instance.Id, ct)
                       ?? await _approvalTasks.GetFirstByInstanceIdAsync(instance.Id, ct);
        Guid? assignee = assigned is not null && Guid.TryParse(assigned.AssigneeRef, out var parsed) ? parsed : null;
        return new TimesheetApprovalStart(instance.Id, assignee, null);
    }

    public async Task RetireSubmissionAsync(TimesheetWeek week, int submissionNumber, Guid actorUserId, CancellationToken ct = default)
    {
        if (submissionNumber < 1)
        {
            return;
        }

        var stale = await _instances.GetByIdempotencyKeyAsync(IdempotencyKey(_tenantContext.TenantId, week, submissionNumber), ct);
        if (stale is null || stale.Id == week.WorkflowInstanceId || IsClosed(stale.Status))
        {
            return;
        }

        if (!await CancelAsync(stale.Id, actorUserId, ct))
        {
            _logger.LogWarning("Stale timesheet approval {InstanceId} of week {WeekId} could not be cancelled.", stale.Id, week.Id);
        }
    }

    /// <summary>States from which MOD-0023 moves no further: a decision, a cancellation, a timeout.</summary>
    private static bool IsClosed(WorkflowInstanceStatus status)
        => status is WorkflowInstanceStatus.Approved or WorkflowInstanceStatus.Completed or WorkflowInstanceStatus.Rejected
            or WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.TimedOut;

    public async Task<bool> CancelAsync(Guid workflowInstanceId, Guid actorUserId, CancellationToken ct = default)
    {
        var instance = await _instances.GetByIdAsync(workflowInstanceId, ct);
        if (instance is null || instance.Status is WorkflowInstanceStatus.Cancelled or WorkflowInstanceStatus.TimedOut)
        {
            // Nothing left open to cancel, and nothing was decided: a timed-out or already-cancelled instance.
            return true;
        }

        if (IsClosed(instance.Status))
        {
            // F13 — DECIDED (approved or rejected) between the caller's read and this cancel. That is not a successful
            // cancel: the caller must not treat the approval as withdrawn, or the decision is lost.
            return false;
        }

        // Every task still waiting on somebody, whatever MOD-0023 has done with it since: an ESCALATED approval is
        // just as open as a waiting one, and leaving it would leave the withdrawn week approvable (F8).
        var open = (await _approvalTasks.ListByInstanceIdAsync(workflowInstanceId, ct))
            .Where(t => t.Status is ApprovalTaskStatus.WaitingApproval or ApprovalTaskStatus.WaitingEvidence
                or ApprovalTaskStatus.Escalated)
            .ToList();

        foreach (var approvalTask in open)
        {
            // MOD-0023's OWN command, so its audit trail and state machine stay authoritative.
            var result = await _mediator.Send(new CancelWorkflowTaskCommand(
                approvalTask.Id,
                new WorkflowContracts.CancelWorkflowTaskRequest(
                    ActorId: actorUserId.ToString(),
                    ReasonCode: "TIMESHEET_WITHDRAWN",
                    IdempotencyKey: $"timesheet-withdraw:{approvalTask.Id}",
                    Comment: null),
                CorrelationId(),
                // B3 — the owner withdrawing its own object is the one path that may cancel an escalated approval.
                AllowEscalated: true), ct);

            if (!result.IsSuccessful)
            {
                return false;
            }
        }

        return true;
    }

    public async Task<TimesheetDecision> ReadDecisionAsync(Guid workflowInstanceId, CancellationToken ct = default)
    {
        var instance = await _instances.GetByIdAsync(workflowInstanceId, ct);
        if (instance is null)
        {
            return new TimesheetDecision(TimesheetDecisionOutcome.Missing, null, null, null);
        }

        var outcome = OutcomeOf(instance);
        if (outcome is TimesheetDecisionOutcome.Pending or TimesheetDecisionOutcome.Cancelled)
        {
            return new TimesheetDecision(outcome, null, null, instance.CompletedAt);
        }

        // Who decided and why: the transition log's own row for the decision, the newest one.
        var action = outcome == TimesheetDecisionOutcome.Approved ? WorkflowTransitionAction.Approve : WorkflowTransitionAction.Reject;
        var decision = (await _transitions.ListByInstanceIdAsync(workflowInstanceId, ct))
            .Where(log => log.Action == action)
            .OrderByDescending(log => log.SequenceNo)
            .FirstOrDefault();

        Guid? actor = decision is not null && Guid.TryParse(decision.ActorId, out var parsed) ? parsed : null;
        return new TimesheetDecision(outcome, actor, decision?.Comment, instance.CompletedAt ?? decision?.CreatedAt);
    }

    public async Task<IReadOnlyDictionary<Guid, TimesheetDecisionOutcome>> ReadOutcomesAsync(
        IReadOnlyCollection<Guid> workflowInstanceIds, CancellationToken ct = default)
    {
        var wanted = workflowInstanceIds.Distinct().ToList();
        if (wanted.Count == 0)
        {
            return new Dictionary<Guid, TimesheetDecisionOutcome>();
        }

        var found = (await _instances.ListByIdsAsync(wanted, ct)).ToDictionary(i => i.Id);
        return wanted.ToDictionary(id => id, id => OutcomeOf(found.GetValueOrDefault(id)));
    }

    /// <summary>MOD-0023's instance state as this module reads it — the ONE mapping, for the single read and the batch.</summary>
    private static TimesheetDecisionOutcome OutcomeOf(WorkflowInstance? instance)
        => instance?.Status switch
        {
            null => TimesheetDecisionOutcome.Missing,
            // MOD-0023 closes an approved single-step instance as Completed; Approved is accepted too.
            WorkflowInstanceStatus.Completed or WorkflowInstanceStatus.Approved => TimesheetDecisionOutcome.Approved,
            WorkflowInstanceStatus.Rejected => TimesheetDecisionOutcome.Rejected,
            WorkflowInstanceStatus.Cancelled => TimesheetDecisionOutcome.Cancelled,
            // Pending, Active, Escalated, TimedOut: nobody has decided — escalation is not an approval.
            _ => TimesheetDecisionOutcome.Pending
        };

    /// <summary>Find or install (and publish) the tenant's timesheet definition — lazy and race-tolerant, exactly as
    /// <c>TaskApprovalService.EnsureTemplateAsync</c> does it.</summary>
    private async Task<Guid?> EnsureTemplateAsync(CancellationToken ct)
    {
        var existing = await _templates.GetByTemplateCodeAsync(TimeEntryModule.ApprovalTemplateCode, ct);
        if (existing is not null)
        {
            return existing.ActivePublishedVersionId is not null ? existing.Id : await PublishAsync(existing.Id, ct);
        }

        var created = await _mediator.Send(new CreateWorkflowDefinitionCommand(
            new WorkflowContracts.CreateWorkflowDefinitionRequest(
                TemplateCode: TimeEntryModule.ApprovalTemplateCode,
                Name: "Timesheet approval",
                Description: "Line-manager approval of a weekly timesheet, installed by MOD-0280-FU01. "
                             + "A rejection must carry a comment."),
            CorrelationId()), ct);

        if (!created.IsSuccessful || created.Data is null)
        {
            var winner = await _templates.GetByTemplateCodeAsync(TimeEntryModule.ApprovalTemplateCode, ct);
            if (winner is null)
            {
                _logger.LogWarning("Could not install the timesheet approval definition: {ReasonCode}", created.ReasonCode);
                return null;
            }

            return winner.ActivePublishedVersionId is not null ? winner.Id : await PublishAsync(winner.Id, ct);
        }

        return await PublishAsync(created.Data.Id, ct);
    }

    private async Task<Guid?> PublishAsync(Guid templateId, CancellationToken ct)
    {
        var published = await _mediator.Send(new PublishWorkflowDefinitionCommand(
            templateId,
            new WorkflowContracts.PublishWorkflowDefinitionRequest(
                DefinitionJson: DefinitionJson,
                SchemaVersion: "1.0",
                ExpressionVersion: "1.0",
                ExpectedTemplateVersion: null,
                ExpectedRowVersion: null,
                PublishReason: "MOD-0280-FU01 timesheet approval"),
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

        _logger.LogWarning("Could not publish the timesheet approval definition {TemplateId}: {ReasonCode}",
            templateId, published.ReasonCode);
        return null;
    }

    private static string CorrelationId() => $"timesheet-approval:{Guid.NewGuid():N}";
}
