using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;

/// <summary>
/// Phase 4 — turn the tenant's due recurrence rules into tasks, exactly once per period.
///
/// <para><b>The one thing this must never do is produce a duplicate.</b> Not on a rerun, not when two sweeps
/// overlap, not when the process dies mid-pass. The mechanism is a CLAIM: the rule's
/// <c>LastProcessInstanceId</c> is stamped with the period's name under an EXPECTED-VERSION write BEFORE the task
/// is created. A second sweep computing the same period loses that write and stops; it never reaches the create.
/// </para>
///
/// <para><b>The failure mode is deliberately "miss", not "duplicate".</b> If the process dies between claiming a
/// period and creating its task, the period is marked done and no task exists — one occurrence is lost. The
/// reverse order (create, then claim) would lose the claim instead and produce a SECOND task on the next pass.
/// A missing occurrence is visible and re-creatable by hand; a duplicate silently doubles someone's workload and
/// is indistinguishable from real work. Which failure you prefer is the whole design decision here, and this is
/// the one chosen.</para>
///
/// <para>Runs with NO user context — the sweep calls it inside <c>TenantScope.Begin</c>. Every read and write
/// below goes through tenant-scoped repositories, so a rule belonging to another tenant is not merely filtered,
/// it does not resolve.</para>
/// </summary>
public sealed class GenerateDueRecurringTasksHandler
    : IRequestHandler<GenerateDueRecurringTasksCommand, Response<GenerateDueRecurringTasksResponse>>
{
    private const int DefaultMaxRules = 200;

    private readonly ITaskRecurrenceRuleRepository _rules;
    private readonly ITaskItemRepository _tasks;
    private readonly IMediator _mediator;
    private readonly ILogger<GenerateDueRecurringTasksHandler> _logger;

    public GenerateDueRecurringTasksHandler(
        ITaskRecurrenceRuleRepository rules,
        ITaskItemRepository tasks,
        IMediator mediator,
        ILogger<GenerateDueRecurringTasksHandler> logger)
    {
        _rules = rules;
        _tasks = tasks;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Response<GenerateDueRecurringTasksResponse>> Handle(
        GenerateDueRecurringTasksCommand command,
        CancellationToken ct)
    {
        var now = (command.NowUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();
        var max = command.MaxRules <= 0 ? DefaultMaxRules : command.MaxRules;

        // ListActiveAsync already excludes deleted rows and inactive rules; the schedule checks both again,
        // because "the repository filters it" is not something a rule about cancelled work should rely on.
        var rules = (await _rules.ListActiveAsync(ct)).Take(max).ToList();

        var generated = 0;
        var alreadyDone = 0;
        var failed = 0;
        var skipped = 0;

        foreach (var rule in rules)
        {
            ct.ThrowIfCancellationRequested();

            if (TaskRecurrenceSchedule.LatestDueOccurrence(rule, now) is not { } occurrence)
            {
                continue;
            }

            /*
             * A rule that cannot say WHO the work belongs to is skipped BEFORE the claim, so it does not burn a
             * period it cannot fill.
             *
             * New rules cannot reach this state — the write path refuses them — but rules created before
             * assignment existed on the entity can, and the claim-first ordering means a burnt period is gone for
             * good. Skipping keeps those rules recoverable: fix the assignment and the period is still there.
             */
            if (TaskAssignmentIntentRules.Validate(
                    rule.AssignmentTarget, rule.AssigneeUserId, rule.PoolPositionId, allowSelfAssigned: false)
                is { } unusable)
            {
                skipped++;
                _logger.LogWarning(
                    "task.recurrence.rule_unassigned RuleId={RuleId} ReasonCode={ReasonCode} Message={Message} "
                    + "CorrelationId={CorrelationId}. No period was claimed, so fixing the rule recovers it.",
                    rule.Id, unusable.ReasonCode, unusable.Message, command.CorrelationId);
                continue;
            }

            var processInstanceId = TaskRecurrenceSchedule.ProcessInstanceId(rule.Id, occurrence);

            // Already made. The comparison is only possible because the name is DETERMINISTIC — a random id
            // would differ every pass and this check would never fire.
            if (string.Equals(rule.LastProcessInstanceId, processInstanceId, StringComparison.Ordinal))
            {
                alreadyDone++;
                continue;
            }

            try
            {
                /*
                 * CLAIM FIRST. The expected-version write is what makes two concurrent sweeps produce one task:
                 * both compute the same period name, both attempt this write, exactly one succeeds.
                 */
                var claimedVersion = rule.Version;
                rule.LastProcessInstanceId = processInstanceId;
                rule.LastGeneratedAt = now;

                if (!await _rules.UpdateAsync(rule, claimedVersion, ct))
                {
                    // Another pass claimed this period first. Not an error — it is the guard working.
                    alreadyDone++;
                    continue;
                }

                var taskId = await CreateTaskAsync(rule, occurrence, processInstanceId, command.CorrelationId, ct);
                if (taskId is null)
                {
                    failed++;
                    _logger.LogWarning(
                        "task.recurrence.generate_failed RuleId={RuleId} ProcessInstanceId={ProcessInstanceId} "
                        + "CorrelationId={CorrelationId}. The period stays claimed, so it will NOT be retried — "
                        + "a duplicate is worse than a missed occurrence.",
                        rule.Id, processInstanceId, command.CorrelationId);
                    continue;
                }

                generated++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failed++;
                // One rule's failure must not abort the tenant's other rules — the precedent sweep's rule,
                // applied one level down.
                _logger.LogWarning(
                    ex,
                    "task.recurrence.rule_failed RuleId={RuleId} ExceptionType={ExceptionType} CorrelationId={CorrelationId}",
                    rule.Id, ex.GetType().Name, command.CorrelationId);
            }
        }

        return Response<GenerateDueRecurringTasksResponse>.Success(
            new GenerateDueRecurringTasksResponse(rules.Count, generated, alreadyDone, failed, skipped),
            200,
            command.CorrelationId);
    }

    /// <summary>
    /// The generated task, built through the ORDINARY create paths so every rule that governs task creation —
    /// assignment resolution, the organization-unit fallback, checklist instantiation, notification — applies
    /// unchanged. A second create path here would be a second, subtly different task.
    /// </summary>
    private async Task<Guid?> CreateTaskAsync(
        TaskRecurrenceRule rule,
        DateTimeOffset occurrence,
        string processInstanceId,
        string correlationId,
        CancellationToken ct)
    {
        /*
         * DUE when the next occurrence begins: recurring work is expected to be finished before its replacement
         * arrives. That is a recurrence decision and it is made HERE — the working-time calculator (WC-2) is
         * deliberately not consulted. That seam answers "how much working time is left against a deadline"; it
         * has no business deciding WHEN work is created, and letting it push a daily task over a weekend would
         * make the schedule surprising in exactly the way a schedule must not be.
         */
        var dueAt = TaskRecurrenceSchedule.NextOccurrenceAfter(rule, occurrence);

        Response<Guid> response;
        if (rule.TaskTemplateId is { } templateId)
        {
            // The template's own shape wins, checklist included. DueAt is left to the template when it defines an
            // offset — a template that says "due in 3 days" is a more specific statement of intent than the
            // schedule's default.
            /*
             * The RULE's assignment is passed as an override. The from-template path already treats an explicit
             * value as winning over the template's default, which is the right precedence: a template says how
             * work is SHAPED, a rule says who this particular schedule is FOR. A rule that names nobody cannot
             * reach here at all, so there is no case where this silently blanks a template's own default.
             */
            response = await _mediator.Send(
                new CreateTaskItemFromTemplateCommand(
                    new CreateTaskFromTemplateRequest(
                        TaskTemplateId: templateId,
                        TitleOverride: null,
                        DueAt: dueAt,
                        AssignmentTargetOverride: rule.AssignmentTarget,
                        AssigneeUserId: rule.AssigneeUserId,
                        PoolPositionId: rule.PoolPositionId),
                    correlationId),
                ct);
        }
        else
        {
            response = await _mediator.Send(
                new CreateTaskItemCommand(
                    new CreateTaskItemRequest(
                        Title: rule.Name,
                        Description: null,
                        Priority: TaskPriority.Medium,
                        /*
                         * The rule's own assignment. This used to be SelfAssigned with a null assignee, and a
                         * background sweep has no "self": the current-user context answers Guid.Empty, so every
                         * task a template-less rule produced belonged to nobody and appeared in no list — while
                         * still consuming its period.
                         */
                        AssignmentTarget: rule.AssignmentTarget,
                        AssigneeUserId: rule.AssigneeUserId,
                        PoolPositionId: rule.PoolPositionId,
                        // Null is fine and usually right: creation resolves a unit from the pool's position, the
                        // assignee's position, or the tenant root. The rule only overrides that when it says so.
                        OrganizationUnitId: rule.OrganizationUnitId,
                        DueAt: dueAt,
                        StartAt: null,
                        PlannedDate: null,
                        EstimateHours: null,
                        Tags: null,
                        ReviewRequired: false,
                        ApprovalRequired: false,
                        ApprovalManagerUserId: null,
                        EmailNotificationsEnabled: true,
                        DelegationAllowed: false,
                        FieldValues: null,
                        Watchers: null),
                    correlationId,
                    /*
                     * A sweep has nobody to ask for a required configurable field. Refusing here would not
                     * collect the value — it would stop the recurrence silently while the period is consumed
                     * anyway, which is the exact failure this handler already fixed once for assignment.
                     * Recorded as BL-058: the rule editor has to carry field values before this can tighten.
                     */
                    EnforceRequiredFields: false),
                ct);
        }

        if (!response.IsSuccessful || response.Data == Guid.Empty)
        {
            return null;
        }

        /*
         * The provenance stamp, applied after creation because the create path owns the task's shape and must
         * not learn about recurrence. Without these two fields a generated task is indistinguishable from a
         * hand-made one, and the pack requires it to be distinguishable.
         */
        return await StampProvenanceAsync(response.Data, rule.Id, processInstanceId, ct);
    }

    private async Task<Guid?> StampProvenanceAsync(
        Guid taskId, Guid ruleId, string processInstanceId, CancellationToken ct)
    {
        var task = await _tasks.GetByIdAsync(taskId, ct);
        if (task is null)
        {
            return null;
        }

        task.RecurrenceRuleId = ruleId;
        task.ProcessInstanceId = processInstanceId;
        await _tasks.UpdateAsync(task, task.Version, ct);
        return taskId;
    }
}
