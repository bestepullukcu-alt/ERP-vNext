using System.Text.Json;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Application.Features.Workflow.Services;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Infrastructure.Persistence;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

/// <summary>
/// BL-547 (CT-WF-BL547) — THE TRUSTED TERMINAL-DECISION EVIDENCE, ANSWER BY ANSWER. Real replica set (never the shared
/// 27017), real repositories, and the production code that PRODUCES each state — the trusted start, the SLA sweep,
/// approve / reject / delegate / request-info, the public cancel, the owning module's withdraw, the trusted cancel — read
/// back by the real <see cref="GetTrustedWorkflowTerminalDecisionEvidenceHandler"/>.
/// <list type="bullet">
/// <item>An SLA-escalated approval is OPEN (instance and task Escalated; nobody has decided): "not terminal", the answer a
/// consumer waits on. It used to be "inconsistent", and MDM ended its request on it — a later approval was applied by nobody.</item>
/// <item>A cancelled or timed-out approval that nobody decided has its OWN code, and only when the facts hold together: a
/// consumer that hears it takes its request back, so nothing may be left that could still be decided.</item>
/// <item>"Inconsistent" is left for facts that contradict each other.</item>
/// </list>
/// A row marked SHAPED writes a status next to a real graph where no production path leaves that combination.
/// </summary>
public sealed partial class WorkflowCandidateDecisionMongoTests
{
    private const string ClosedCode = "WORKFLOW_INSTANCE_CLOSED_WITHOUT_DECISION";

    // ── open: the approval is waiting, asked for information, or escalated — nobody has decided ──────────────────

    [Theory]
    [InlineData(WorkflowInstanceStatus.Active, ApprovalTaskStatus.WaitingApproval)]     // a fresh start
    [InlineData(WorkflowInstanceStatus.Active, ApprovalTaskStatus.WaitingEvidence)]
    [InlineData(WorkflowInstanceStatus.Active, ApprovalTaskStatus.Escalated)]           // SHAPED
    [InlineData(WorkflowInstanceStatus.Escalated, ApprovalTaskStatus.Escalated)]        // what the SLA sweep writes
    [InlineData(WorkflowInstanceStatus.Escalated, ApprovalTaskStatus.WaitingApproval)]  // SHAPED
    [InlineData(WorkflowInstanceStatus.Escalated, ApprovalTaskStatus.WaitingEvidence)]  // SHAPED
    public async Task Terminal_evidence_of_an_open_approval_is_not_terminal(
        WorkflowInstanceStatus instanceStatus, ApprovalTaskStatus taskStatus)
    {
        var key = $"open-{instanceStatus}-{taskStatus}";
        var started = await _store.StartTrustedAsync(key);
        await _store.ShapeAsync(started, instanceStatus, taskStatus);

        var response = await _store.TerminalEvidenceAsync(started.WorkflowInstanceId, key);

        AssertEvidenceRefusal(response, WorkflowReasonCodes.WorkflowDecisionNotTerminal);
    }

    [Theory]
    [InlineData(WorkflowInstanceStatus.Pending, ApprovalTaskStatus.WaitingApproval)]    // a trusted start proves Active: Pending contradicts it
    [InlineData(WorkflowInstanceStatus.Escalated, ApprovalTaskStatus.Cancelled)]        // escalated, nothing left open
    [InlineData(WorkflowInstanceStatus.Escalated, ApprovalTaskStatus.Approved)]         // an approved task with no approve log
    [InlineData(WorkflowInstanceStatus.Escalated, ApprovalTaskStatus.Delegated)]        // nothing in the engine writes it: not open
    [InlineData(WorkflowInstanceStatus.Active, ApprovalTaskStatus.Cancelled)]
    [InlineData(WorkflowInstanceStatus.Active, ApprovalTaskStatus.Delegated)]
    public async Task Terminal_evidence_of_a_running_instance_without_exactly_one_open_task_is_inconsistent(
        WorkflowInstanceStatus instanceStatus, ApprovalTaskStatus taskStatus)
    {
        var key = $"not-open-{instanceStatus}-{taskStatus}";
        var started = await _store.StartTrustedAsync(key);
        await _store.ShapeAsync(started, instanceStatus, taskStatus);

        var response = await _store.TerminalEvidenceAsync(started.WorkflowInstanceId, key);

        AssertEvidenceRefusal(response, WorkflowReasonCodes.WorkflowTerminalEvidenceInconsistent);
    }

    [Theory]
    [InlineData(WorkflowInstanceStatus.Active)]
    [InlineData(WorkflowInstanceStatus.Escalated)]
    public async Task Terminal_evidence_of_a_running_instance_with_two_open_tasks_is_inconsistent(
        WorkflowInstanceStatus instanceStatus)
    {
        var key = $"two-open-{instanceStatus}";
        var started = await _store.StartTrustedAsync(key);
        await _store.ShapeAsync(started, instanceStatus, ApprovalTaskStatus.Escalated);
        await _store.AddTaskAsync(started, ApprovalTaskStatus.WaitingApproval);

        var response = await _store.TerminalEvidenceAsync(started.WorkflowInstanceId, key);

        AssertEvidenceRefusal(response, WorkflowReasonCodes.WorkflowTerminalEvidenceInconsistent);
    }

    // ── a REAL SLA escalation: open until it is decided, and then an ordinary decision ─────────────────────────

    [Theory]
    [InlineData(WorkflowTransitionAction.Approve)]
    [InlineData(WorkflowTransitionAction.Reject)]
    public async Task Terminal_evidence_of_a_real_sla_escalation_is_not_terminal_until_the_escalated_approval_is_decided(
        WorkflowTransitionAction decision)
    {
        var started = await _store.ReachAsync("escalated", $"escalated-{decision}");
        // non-vacuity: this IS the state the review found — instance and task escalated by the real sweep
        var instance = (await _store.Instances.GetByIdAsync(started.Started.WorkflowInstanceId))!;
        var task = (await _store.Tasks.GetByIdAsync(started.Started.ApprovalTaskId))!;
        Assert.Equal(WorkflowInstanceStatus.Escalated, instance.Status);
        Assert.Equal(ApprovalTaskStatus.Escalated, task.Status);
        Assert.Equal(1, task.EscalationLevel);

        AssertEvidenceRefusal(
            await _store.TerminalEvidenceAsync(started.Started.WorkflowInstanceId, started.Key),
            WorkflowReasonCodes.WorkflowDecisionNotTerminal);

        // the escalation candidate decides it later — the decision the old answer would have orphaned
        var decided = decision == WorkflowTransitionAction.Approve
            ? await _store.ApproveAsync(task.Id, D(CandidateA), started.Key + "-decide")
            : await _store.RejectAsync(task.Id, D(CandidateA), started.Key + "-decide");
        Assert.True(decided.IsSuccessful, decided.ReasonCode);

        var evidence = await _store.TerminalEvidenceAsync(started.Started.WorkflowInstanceId, started.Key);
        Assert.True(evidence.IsSuccessful, evidence.ReasonCode);
        Assert.Equal(decision.ToString(), evidence.Data!.TerminalAction);
        Assert.Equal(D(CandidateA), evidence.Data.ActorUserId);
        Assert.Equal(
            decision == WorkflowTransitionAction.Approve ? "Completed" : "Rejected",
            evidence.Data.InstanceStatus);
        Assert.Equal(
            decision == WorkflowTransitionAction.Approve ? "Approved" : "Rejected",
            evidence.Data.TaskStatus);
    }

    [Fact]
    public async Task Terminal_evidence_stays_not_terminal_through_a_delegation_and_a_request_for_information_after_an_escalation()
    {
        var started = await _store.ReachAsync("escalated", "escalated-then-moved");
        var taskId = started.Started.ApprovalTaskId;

        Assert.True((await _store.DelegateAsync(taskId, D(CandidateA), D(NextApprover), "moved-delegate")).IsSuccessful);
        var delegated = (await _store.Tasks.GetByIdAsync(taskId))!;
        Assert.Equal(ApprovalTaskStatus.WaitingApproval, delegated.Status); // non-vacuity: back to waiting,
        Assert.True(delegated.EscalationLevel > 0);                          // but it was escalated once
        AssertEvidenceRefusal(
            await _store.TerminalEvidenceAsync(started.Started.WorkflowInstanceId, started.Key),
            WorkflowReasonCodes.WorkflowDecisionNotTerminal);

        Assert.True((await _store.RequestInfoAsync(taskId, D(NextApprover), "moved-info")).IsSuccessful);
        Assert.Equal(ApprovalTaskStatus.WaitingEvidence, (await _store.Tasks.GetByIdAsync(taskId))!.Status);
        AssertEvidenceRefusal(
            await _store.TerminalEvidenceAsync(started.Started.WorkflowInstanceId, started.Key),
            WorkflowReasonCodes.WorkflowDecisionNotTerminal);
    }

    // ── decided: approved and rejected are evidence, as before ────────────────────────────────────────────────

    [Theory]
    [InlineData(WorkflowTransitionAction.Approve)]
    [InlineData(WorkflowTransitionAction.Reject)]
    public async Task Terminal_evidence_of_a_decided_approval_is_the_decision(WorkflowTransitionAction decision)
    {
        var started = await _store.ReachAsync("open", $"decided-{decision}");
        var decided = decision == WorkflowTransitionAction.Approve
            ? await _store.ApproveAsync(started.Started.ApprovalTaskId, D(First), started.Key + "-decide")
            : await _store.RejectAsync(started.Started.ApprovalTaskId, D(First), started.Key + "-decide");
        Assert.True(decided.IsSuccessful, decided.ReasonCode);

        var evidence = await _store.TerminalEvidenceAsync(started.Started.WorkflowInstanceId, started.Key);

        Assert.True(evidence.IsSuccessful, evidence.ReasonCode);
        Assert.Equal(decision.ToString(), evidence.Data!.TerminalAction);
        Assert.Equal(D(First), evidence.Data.ActorUserId);
        Assert.Equal(2, evidence.Data.TransitionSequence);
    }

    // ── closed without a decision: cancelled ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("cancelled")]                    // the public cancel
    [InlineData("withdrawn-after-escalation")]   // the owning module's withdraw of an escalated approval
    [InlineData("trusted-cancelled")]            // the trusted consumer's own cancel (MDM's maker withdrawal)
    public async Task Terminal_evidence_of_a_cancelled_approval_is_closed_without_decision(string path)
    {
        var started = await _store.ReachAsync(path, $"closed-{path}");
        Assert.Equal(WorkflowInstanceStatus.Cancelled, (await _store.Instances.GetByIdAsync(started.Started.WorkflowInstanceId))!.Status);

        var response = await _store.TerminalEvidenceAsync(started.Started.WorkflowInstanceId, started.Key);

        AssertEvidenceRefusal(response, WorkflowReasonCodes.WorkflowInstanceClosedWithoutDecision);
        Assert.Equal(ClosedCode, response.ReasonCode); // the literal the consumers match on
        Assert.Contains("cancelled", Assert.Single(response.Errors), StringComparison.OrdinalIgnoreCase);
        await AssertNothingCanStillBeDecidedAsync(started);
    }

    // ── closed without a decision: timed out ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("timed-out")]                    // the SLA ran out on a waiting task
    [InlineData("timed-out-after-escalation")]   // escalated first, then the timeout
    public async Task Terminal_evidence_of_a_timed_out_approval_is_closed_without_decision(string path)
    {
        var started = await _store.ReachAsync(path, $"closed-{path}");
        Assert.Equal(WorkflowInstanceStatus.TimedOut, (await _store.Instances.GetByIdAsync(started.Started.WorkflowInstanceId))!.Status);

        var response = await _store.TerminalEvidenceAsync(started.Started.WorkflowInstanceId, started.Key);

        AssertEvidenceRefusal(response, WorkflowReasonCodes.WorkflowInstanceClosedWithoutDecision);
        Assert.Equal(ClosedCode, response.ReasonCode);
        Assert.Contains("timed out", Assert.Single(response.Errors), StringComparison.OrdinalIgnoreCase);
        await AssertNothingCanStillBeDecidedAsync(started);
    }

    [Fact]
    public async Task Terminal_evidence_of_a_multi_step_approval_cancelled_after_a_prior_approval_is_closed_without_decision()
    {
        // The prior-approval rule is unchanged: step one was approved (a coherent earlier approval), step two is open.
        var templateId = await _store.StepTemplateAsync(twoSteps: true);
        var started = await _store.StartTrustedAsync("two-steps", templateId: templateId);
        Assert.True((await _store.ApproveAsync(started.ApprovalTaskId, D(First), "two-steps-first")).IsSuccessful);
        AssertEvidenceRefusal(
            await _store.TerminalEvidenceAsync(started.WorkflowInstanceId, "two-steps"),
            WorkflowReasonCodes.WorkflowDecisionNotTerminal);

        var second = Assert.Single(await _store.Tasks.ListByInstanceIdAsync(started.WorkflowInstanceId),
            t => t.Status == ApprovalTaskStatus.WaitingApproval);
        Assert.True((await _store.CancelAsync(second.Id, "two-steps-cancel")).IsSuccessful);

        var response = await _store.TerminalEvidenceAsync(started.WorkflowInstanceId, "two-steps");

        AssertEvidenceRefusal(response, WorkflowReasonCodes.WorkflowInstanceClosedWithoutDecision);
    }

    // ── facts that contradict each other stay "inconsistent" — never "closed" ─────────────────────────────────

    [Theory]
    [InlineData("task-reopened")]               // cancelled, but its task can be decided again
    [InlineData("second-open-task")]            // cancelled, but another task is waiting
    [InlineData("no-closing-log")]              // cancelled with no cancel on record
    [InlineData("wrong-closing-action")]        // timed out, but what closed it was a cancel
    [InlineData("closing-log-not-last")]        // something was written after the cancel
    [InlineData("closing-task-not-cancelled")]  // the cancel's task says approved
    [InlineData("terminal-approve-before-cancel")] // an approve that finished the instance, then "cancelled"
    [InlineData("reject-before-timeout")]       // a reject, then "timed out"
    [InlineData("prior-approval-undone")]       // two steps: the first step's approval no longer holds together
    [InlineData("closing-action-mismatch")]     // the last log is on the cancelled task and says "cancelled", but is no cancel
    [InlineData("closing-status-mismatch")]     // the last log is a cancel of that task that says the instance stays active
    public async Task Terminal_evidence_of_contradictory_closing_facts_is_inconsistent(string shape)
    {
        var started = await _store.ContradictoryClosureAsync(shape, $"contradiction-{shape}");

        var response = await _store.TerminalEvidenceAsync(started.Started.WorkflowInstanceId, started.Key);

        AssertEvidenceRefusal(response, WorkflowReasonCodes.WorkflowTerminalEvidenceInconsistent);
    }

    // ── the identity gate and the tenant boundary stand in front of every one of the answers ──────────────────

    [Theory]
    [InlineData("escalated")]
    [InlineData("cancelled")]
    [InlineData("timed-out")]
    public async Task Terminal_evidence_answers_the_new_codes_only_to_the_owning_consumer_of_the_owning_tenant(string state)
    {
        var started = await _store.ReachAsync(state, $"boundary-{state}");
        var id = started.Started.WorkflowInstanceId;

        // another service client of the same tenant, and an object the instance is not about: not found, nothing said
        foreach (var other in new[]
                 {
                     await _store.TerminalEvidenceAsync(id, started.Key, clientId: Guid.NewGuid()),
                     await _store.TerminalEvidenceAsync(id, started.Key, objectId: "GP-another-object")
                 })
        {
            Assert.False(other.IsSuccessful);
            Assert.Equal(404, other.StatusCode);
            Assert.Equal(WorkflowReasonCodes.NotFoundNonLeakage, other.ReasonCode);
        }

        // another tenant: the instance is not even visible
        _store.TenantContext.SetTenant(Guid.NewGuid());
        var foreign = await _store.TerminalEvidenceAsync(id, started.Key);
        Assert.False(foreign.IsSuccessful);
        Assert.Equal(404, foreign.StatusCode);
        Assert.Equal(WorkflowReasonCodes.NotFoundNonLeakage, foreign.ReasonCode);
    }

    // ── the answer travels in the envelope the consumers already read ─────────────────────────────────────────

    [Fact]
    public async Task Terminal_evidence_closed_answer_uses_exactly_the_envelope_of_the_answers_consumers_already_read()
    {
        // MDM's client accepts exactly these six keys and files anything else as an unreadable response (RequireEnvelope).
        string[] contract = ["correlation_id", "data", "errors", "isSuccessful", "reason_code", "statusCode"];
        var open = await _store.ReachAsync("open", "envelope-open");
        var cancelled = await _store.ReachAsync("cancelled", "envelope-cancelled");
        var timedOut = await _store.ReachAsync("timed-out", "envelope-timed-out");

        foreach (var (started, code) in new[]
                 {
                     (open, WorkflowReasonCodes.WorkflowDecisionNotTerminal),
                     (cancelled, WorkflowReasonCodes.WorkflowInstanceClosedWithoutDecision),
                     (timedOut, WorkflowReasonCodes.WorkflowInstanceClosedWithoutDecision)
                 })
        {
            var response = await _store.TerminalEvidenceAsync(started.Started.WorkflowInstanceId, started.Key);
            using var document = JsonDocument.Parse(JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
            var root = document.RootElement;

            Assert.Equal(contract, root.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).ToArray());
            Assert.Equal(409, root.GetProperty("statusCode").GetInt32());
            Assert.False(root.GetProperty("isSuccessful").GetBoolean());
            Assert.Equal(code, root.GetProperty("reason_code").GetString());
            Assert.Equal(JsonValueKind.Null, root.GetProperty("data").ValueKind);
            Assert.Equal(1, root.GetProperty("errors").GetArrayLength());
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────────────────────────────────────

    private static void AssertEvidenceRefusal(Response<TrustedWorkflowTerminalDecisionEvidence> response, string reasonCode)
    {
        Assert.False(response.IsSuccessful);
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(reasonCode, response.ReasonCode);
        Assert.Null(response.Data);
    }

    /// <summary>What "closed without a decision" promises a consumer that stops waiting: no task can still be decided
    /// (the gates refuse the closed one), and nothing is left waiting, asked for information or escalated.</summary>
    private async Task AssertNothingCanStillBeDecidedAsync(Reached reached)
    {
        var tasks = await _store.Tasks.ListByInstanceIdAsync(reached.Started.WorkflowInstanceId);
        Assert.All(tasks, t => Assert.True(
            t.Status is ApprovalTaskStatus.Approved or ApprovalTaskStatus.Cancelled or ApprovalTaskStatus.TimedOut, t.Status.ToString()));
        var late = await _store.ApproveAsync(reached.Started.ApprovalTaskId, D(First), reached.Key + "-late");
        Assert.Equal(409, late.StatusCode);
        Assert.Equal(WorkflowReasonCodes.WorkflowTaskInvalidState, late.ReasonCode);
    }

    private sealed record Reached(TrustedWorkflowStartResult Started, string Key);

    private sealed partial class Store
    {
        /// <summary>A trusted start, exactly as MDM makes one. <paramref name="dueAt"/> in the past makes the task overdue.</summary>
        public async Task<TrustedWorkflowStartResult> StartTrustedAsync(
            string key, DateTimeOffset? dueAt = null, Guid? templateId = null)
        {
            var template = templateId ?? await StepTemplateAsync(twoSteps: false);
            var started = await Coordinator().StartAsync(
                new TrustedWorkflowStartRequest(template, null, "GlobalProduct", "GP-" + key, null,
                    [D(First), D(Second)], "SUBMIT", key, false, false, dueAt),
                TrustedClient, TrustedMaker, "corr-" + key);
            Assert.True(started.IsSuccessful, started.ReasonCode);
            return started.Data!;
        }

        public Task<Response<TrustedWorkflowTerminalDecisionEvidence>> TerminalEvidenceAsync(
            Guid instanceId, string key, Guid? clientId = null, string? objectId = null) =>
            new GetTrustedWorkflowTerminalDecisionEvidenceHandler(Instances, Tasks, Logs).Handle(
                new GetTrustedWorkflowTerminalDecisionEvidenceQuery(
                    instanceId, clientId ?? TrustedClient, "GlobalProduct", objectId ?? "GP-" + key, "corr-evidence-" + key),
                default);

        /// <summary>SHAPED: the instance and its first task are given these statuses, over the real trusted graph.</summary>
        public async Task ShapeAsync(
            TrustedWorkflowStartResult started, WorkflowInstanceStatus instanceStatus, ApprovalTaskStatus taskStatus)
        {
            var task = (await Tasks.GetByIdAsync(started.ApprovalTaskId))!;
            task.Status = taskStatus;
            Assert.True(await Tasks.UpdateAsync(task, task.Version));
            await SetInstanceStatusAsync(started.WorkflowInstanceId, instanceStatus);
        }

        public async Task SetInstanceStatusAsync(Guid instanceId, WorkflowInstanceStatus status)
        {
            var instance = (await Instances.GetByIdAsync(instanceId))!;
            instance.Status = status;
            Assert.True(await Instances.UpdateAsync(instance, instance.Version));
        }

        public async Task SetTaskStatusAsync(Guid taskId, ApprovalTaskStatus status)
        {
            var task = (await Tasks.GetByIdAsync(taskId))!;
            task.Status = status;
            Assert.True(await Tasks.UpdateAsync(task, task.Version));
        }

        public Task AddTaskAsync(TrustedWorkflowStartResult started, ApprovalTaskStatus status) =>
            Tasks.CreateAsync(new ApprovalTask
            {
                TenantId = TenantContext.TenantId,
                WorkflowInstanceId = started.WorkflowInstanceId,
                StageCode = "stage-1",
                StepCode = "step-b",
                Status = status
            });

        /// <summary>A published template with an explicit first step "step-a" (the step the SLA rules of this class name) and,
        /// with <paramref name="twoSteps"/>, a second one whose only candidate is NextApprover.</summary>
        public async Task<Guid> StepTemplateAsync(bool twoSteps)
        {
            var template = await Templates.CreateAsync(new WorkflowTemplate
            {
                TenantId = TenantContext.TenantId,
                TemplateCode = "BL547-" + Guid.NewGuid().ToString("N")[..10],
                Name = "Terminal evidence",
                Status = WorkflowTemplateStatus.Published
            });
            var steps = twoSteps
                ? "{\"code\":\"step-a\"},{\"code\":\"step-b\",\"assignment\":{\"candidatePrincipalIds\":[\"" + D(NextApprover) + "\"]}}"
                : "{\"code\":\"step-a\"}";
            var version = await Versions.CreateAsync(new WorkflowTemplateVersion
            {
                TenantId = TenantContext.TenantId,
                TemplateId = template.Id,
                VersionNumber = 1,
                DefinitionJson = "{\"stages\":[{\"code\":\"stage-1\",\"steps\":[" + steps + "]}]}",
                SchemaVersion = "1.0",
                ExpressionVersion = "1.0",
                Status = WorkflowTemplateVersionStatus.Published,
                IsImmutable = true
            });
            template.ActivePublishedVersionId = version.Id;
            template.CurrentVersionId = version.Id;
            Assert.True(await Templates.UpdateAsync(template, template.Version));
            return template.Id;
        }

        public Task MoveDueAsync(Guid taskId, TimeSpan ago) => MoveDueCoreAsync(taskId, DateTimeOffset.UtcNow - ago);

        private async Task MoveDueCoreAsync(Guid taskId, DateTimeOffset dueAt)
        {
            var task = (await Tasks.GetByIdAsync(taskId))!;
            task.DueAt = dueAt;
            Assert.True(await Tasks.UpdateAsync(task, task.Version));
        }

        /// <summary>
        /// Brings a fresh trusted approval to a named state with the production code that does it: "open" (just started),
        /// "escalated" (the SLA sweep), "cancelled" (the public cancel), "withdrawn-after-escalation" (the owner's
        /// withdraw, allowEscalated), "trusted-cancelled" (the trusted consumer's cancel), "timed-out" (the sweep, the
        /// timeout already due) and "timed-out-after-escalation" (escalated first, the timeout comes later).
        /// </summary>
        public async Task<Reached> ReachAsync(string state, string key)
        {
            var overdue = state is "escalated" or "withdrawn-after-escalation" or "timed-out" or "timed-out-after-escalation";
            var templateId = await StepTemplateAsync(twoSteps: false);
            var started = await StartTrustedAsync(key, overdue ? DateTimeOffset.UtcNow.AddMinutes(-5) : null, templateId);
            switch (state)
            {
                case "open":
                    break;
                case "escalated":
                case "withdrawn-after-escalation":
                    await EscalationRuleAsync(templateId, [D(CandidateA)]);
                    await RunEscalationsAsync();
                    if (state == "withdrawn-after-escalation")
                    {
                        Assert.True((await CancelAsync(started.ApprovalTaskId, key + "-withdraw", allowEscalated: true)).IsSuccessful);
                    }

                    break;
                case "cancelled":
                    Assert.True((await CancelAsync(started.ApprovalTaskId, key + "-cancel")).IsSuccessful);
                    break;
                case "trusted-cancelled":
                    await TrustedCancelAsync(started, key);
                    break;
                case "timed-out":
                    await EscalationRuleAsync(templateId, [D(CandidateA)], timeoutAfterMinutes: 10);
                    await RunEscalationsAsync();
                    break;
                case "timed-out-after-escalation":
                    // escalates at 5 minutes overdue (the timeout is an hour after the due date)...
                    await EscalationRuleAsync(templateId, [D(CandidateA)], timeoutAfterMinutes: 60);
                    await RunEscalationsAsync();
                    Assert.Equal(WorkflowInstanceStatus.Escalated, (await Instances.GetByIdAsync(started.WorkflowInstanceId))!.Status);
                    // ...and two hours overdue is past it
                    await MoveDueAsync(started.ApprovalTaskId, TimeSpan.FromHours(2));
                    await RunEscalationsAsync();
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }

            return new Reached(started, key);
        }

        public async Task TrustedCancelAsync(TrustedWorkflowStartResult started, string key)
        {
            var instance = (await Instances.GetByIdAsync(started.WorkflowInstanceId))!;
            var task = (await Tasks.GetByIdAsync(started.ApprovalTaskId))!;
            var coordinator = new TrustedWorkflowCancellationCoordinator(
                Instances, Tasks, Logs, new PlatformTransactionExecutor(Context));
            var cancelled = await coordinator.CancelAsync(
                TrustedClient,
                TrustedMaker,
                new TrustedWorkflowCancellationRequest(instance.Id, task.Id, instance.ObjectType, instance.ObjectId,
                    TrustedMaker, instance.Version, task.Version, "WITHDRAWN", null, key + "-trusted-cancel"),
                "corr-" + key,
                default);
            Assert.True(cancelled.IsSuccessful, cancelled.ReasonCode);
        }

        /// <summary>
        /// A closed approval whose facts do NOT hold together. Each shape starts from a real cancel / timeout / decision
        /// and then writes ONE contradiction beside it (SHAPED), so each is the evidence of exactly one rule.
        /// </summary>
        public async Task<Reached> ContradictoryClosureAsync(string shape, string key)
        {
            switch (shape)
            {
                case "task-reopened":
                {
                    var reached = await ReachAsync("cancelled", key);
                    await SetTaskStatusAsync(reached.Started.ApprovalTaskId, ApprovalTaskStatus.WaitingApproval);
                    return reached;
                }
                case "second-open-task":
                {
                    var reached = await ReachAsync("cancelled", key);
                    await AddTaskAsync(reached.Started, ApprovalTaskStatus.WaitingApproval);
                    return reached;
                }
                case "no-closing-log":
                {
                    var reached = await ReachAsync("open", key);
                    await ShapeAsync(reached.Started, WorkflowInstanceStatus.Cancelled, ApprovalTaskStatus.Cancelled);
                    return reached;
                }
                case "wrong-closing-action":
                {
                    var reached = await ReachAsync("cancelled", key);
                    await SetTaskStatusAsync(reached.Started.ApprovalTaskId, ApprovalTaskStatus.TimedOut);
                    await SetInstanceStatusAsync(reached.Started.WorkflowInstanceId, WorkflowInstanceStatus.TimedOut);
                    return reached;
                }
                case "closing-log-not-last":
                {
                    var reached = await ReachAsync("cancelled", key);
                    await TakeNextSequenceNumberAsync(reached.Started.WorkflowInstanceId);
                    return reached;
                }
                case "closing-task-not-cancelled":
                {
                    var reached = await ReachAsync("cancelled", key);
                    await SetTaskStatusAsync(reached.Started.ApprovalTaskId, ApprovalTaskStatus.Approved);
                    return reached;
                }
                case "terminal-approve-before-cancel":
                {
                    var reached = await ReachAsync("open", key);
                    Assert.True((await ApproveAsync(reached.Started.ApprovalTaskId, D(First), key + "-approve")).IsSuccessful);
                    await SetInstanceStatusAsync(reached.Started.WorkflowInstanceId, WorkflowInstanceStatus.Cancelled);
                    return reached;
                }
                case "reject-before-timeout":
                {
                    var reached = await ReachAsync("open", key);
                    Assert.True((await RejectAsync(reached.Started.ApprovalTaskId, D(First), key + "-reject")).IsSuccessful);
                    await SetInstanceStatusAsync(reached.Started.WorkflowInstanceId, WorkflowInstanceStatus.TimedOut);
                    return reached;
                }
                case "prior-approval-undone":
                {
                    var templateId = await StepTemplateAsync(twoSteps: true);
                    var started = await StartTrustedAsync(key, templateId: templateId);
                    Assert.True((await ApproveAsync(started.ApprovalTaskId, D(First), key + "-first")).IsSuccessful);
                    var second = Assert.Single(await Tasks.ListByInstanceIdAsync(started.WorkflowInstanceId),
                        t => t.Status == ApprovalTaskStatus.WaitingApproval);
                    Assert.True((await CancelAsync(second.Id, key + "-cancel")).IsSuccessful);
                    await SetTaskStatusAsync(started.ApprovalTaskId, ApprovalTaskStatus.Cancelled);
                    return new Reached(started, key);
                }
                case "closing-action-mismatch":
                {
                    var reached = await ReachAsync("cancelled", key);
                    await AppendLogAsync(reached.Started, WorkflowTransitionAction.Delegate, WorkflowInstanceStatus.Cancelled);
                    return reached;
                }
                case "closing-status-mismatch":
                {
                    var reached = await ReachAsync("cancelled", key);
                    await AppendLogAsync(reached.Started, WorkflowTransitionAction.Cancel, WorkflowInstanceStatus.Active);
                    return reached;
                }
                default:
                    throw new ArgumentOutOfRangeException(nameof(shape), shape, null);
            }
        }

        /// <summary>SHAPED: one more transition log on the first task, at the next sequence number, naming the instance
        /// status it left behind.</summary>
        private async Task AppendLogAsync(
            TrustedWorkflowStartResult started, WorkflowTransitionAction action, WorkflowInstanceStatus instanceStatusAfter) =>
            await Logs.CreateAsync(new WorkflowTransitionLog
            {
                TenantId = TenantContext.TenantId,
                WorkflowInstanceId = started.WorkflowInstanceId,
                ApprovalTaskId = started.ApprovalTaskId,
                Action = action,
                ToState = ApprovalTaskStatus.Cancelled.ToString(),
                ToStatus = instanceStatusAfter.ToString(),
                ActorId = "shaped-writer",
                ActorRef = "shaped-writer",
                ReasonCode = "SHAPED",
                IdempotencyKey = "shaped-" + Guid.NewGuid().ToString("N"),
                SequenceNo = await Logs.GetLatestSequenceNoAsync(started.WorkflowInstanceId) + 1
            });
    }
}
