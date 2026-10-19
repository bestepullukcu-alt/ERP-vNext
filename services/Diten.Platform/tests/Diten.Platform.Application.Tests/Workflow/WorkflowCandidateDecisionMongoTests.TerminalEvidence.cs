using System.Text.Json;
using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Workflow;
using Diten.Platform.Application.Features.Workflow.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Workflow.Queries;
using Diten.Platform.Application.Features.Workflow.Services;
using Diten.Platform.Domain.Entities.Workflow;
using Diten.Platform.Domain.Enums.Workflow;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence;
using Diten.Platform.Infrastructure.Persistence.Repositories;
using Xunit;

namespace Diten.Platform.Application.Tests.Workflow;

/// <summary>
/// BL-547 + BL-547-FIX1 — THE TRUSTED TERMINAL-DECISION EVIDENCE, ANSWER BY ANSWER. Real replica set (never the shared
/// 27017), real repositories, and the production code that PRODUCES each state — the trusted start, the SLA sweep,
/// approve / reject / delegate / request-info, the public cancel, the trusted cancel — read back by the real
/// <see cref="GetTrustedWorkflowTerminalDecisionEvidenceHandler"/>.
/// <list type="bullet">
/// <item>An SLA-escalated approval is OPEN (instance and task Escalated; nobody has decided): "not terminal", the answer a
/// consumer waits on. It used to be "inconsistent", and MDM ended its request on it — a later approval was applied by nobody.</item>
/// <item>A cancelled or timed-out approval that nobody decided has its OWN code, and only when the facts hold together: a
/// consumer that hears it takes its request back, so nothing may be left that could still be decided.</item>
/// <item>"Inconsistent" is left for facts that contradict each other.</item>
/// <item>The three reads (instance, tasks, logs) are not a snapshot: read while the approval is closing, the answer is the
/// closing, not "inconsistent" (the second look).</item>
/// </list>
/// EVERY ROW SAYS WHERE ITS STATE CAME FROM, in its data (<c>origin</c>): REAL — brought about by production code and nothing
/// else; SHAPED — a status written directly next to a real graph, because no production path leaves that combination;
/// SEAM — brought about by a production method that no production code calls on this kind of instance (the withdraw of an
/// ESCALATED approval, <c>allowEscalated</c>: the Tasks, Timesheet and Review modules send it through
/// <c>CancelWorkflowTaskCommand</c> for their own objects; nothing sends it for a trusted consumer's instance, and the
/// trusted cancel refuses an escalated approval).
/// </summary>
public sealed partial class WorkflowCandidateDecisionMongoTests
{
    private const string ClosedCode = "WORKFLOW_INSTANCE_CLOSED_WITHOUT_DECISION";
    private const string Real = "REAL";
    private const string Shaped = "SHAPED";
    private const string Seam = "SEAM";

    // ── open: the approval is waiting, asked for information, or escalated — nobody has decided ──────────────────

    [Theory]
    [InlineData(Real, WorkflowInstanceStatus.Active, ApprovalTaskStatus.WaitingApproval)]     // a fresh start
    [InlineData(Real, WorkflowInstanceStatus.Active, ApprovalTaskStatus.WaitingEvidence)]     // a real request for information
    [InlineData(Real, WorkflowInstanceStatus.Escalated, ApprovalTaskStatus.Escalated)]        // what the SLA sweep writes
    [InlineData(Shaped, WorkflowInstanceStatus.Active, ApprovalTaskStatus.Escalated)]
    [InlineData(Shaped, WorkflowInstanceStatus.Escalated, ApprovalTaskStatus.WaitingApproval)]
    [InlineData(Shaped, WorkflowInstanceStatus.Escalated, ApprovalTaskStatus.WaitingEvidence)]
    public async Task Terminal_evidence_of_an_open_approval_is_not_terminal(
        string origin, WorkflowInstanceStatus instanceStatus, ApprovalTaskStatus taskStatus)
    {
        var key = $"open-{origin}-{instanceStatus}-{taskStatus}";
        TrustedWorkflowStartResult started;
        if (IsReal(origin))
        {
            var state = (instanceStatus, taskStatus) switch
            {
                (WorkflowInstanceStatus.Active, ApprovalTaskStatus.WaitingApproval) => "open",
                (WorkflowInstanceStatus.Active, ApprovalTaskStatus.WaitingEvidence) => "request-info",
                (WorkflowInstanceStatus.Escalated, ApprovalTaskStatus.Escalated) => "escalated",
                _ => throw new ArgumentOutOfRangeException(nameof(taskStatus), "no production path for this REAL row")
            };
            started = (await _store.ReachAsync(state, key)).Started;
            // non-vacuity: production code left exactly this combination
            Assert.Equal(instanceStatus, (await _store.Instances.GetByIdAsync(started.WorkflowInstanceId))!.Status);
            Assert.Equal(taskStatus, (await _store.Tasks.GetByIdAsync(started.ApprovalTaskId))!.Status);
        }
        else
        {
            started = await _store.StartTrustedAsync(key);
            await _store.ShapeAsync(started, instanceStatus, taskStatus);
        }

        var response = await _store.TerminalEvidenceAsync(started.WorkflowInstanceId, key);

        AssertEvidenceRefusal(response, WorkflowReasonCodes.WorkflowDecisionNotTerminal);
    }

    [Theory]
    [InlineData(Shaped, WorkflowInstanceStatus.Pending, ApprovalTaskStatus.WaitingApproval)]    // a trusted start proves Active: Pending contradicts it
    [InlineData(Shaped, WorkflowInstanceStatus.Escalated, ApprovalTaskStatus.Cancelled)]        // escalated, nothing left open
    [InlineData(Shaped, WorkflowInstanceStatus.Escalated, ApprovalTaskStatus.Approved)]         // an approved task with no approve log
    [InlineData(Shaped, WorkflowInstanceStatus.Escalated, ApprovalTaskStatus.Delegated)]        // nothing in the engine writes Delegated: not open
    [InlineData(Shaped, WorkflowInstanceStatus.Active, ApprovalTaskStatus.Cancelled)]
    [InlineData(Shaped, WorkflowInstanceStatus.Active, ApprovalTaskStatus.Delegated)]
    public async Task Terminal_evidence_of_a_running_instance_without_exactly_one_open_task_is_inconsistent(
        string origin, WorkflowInstanceStatus instanceStatus, ApprovalTaskStatus taskStatus)
    {
        Assert.False(IsReal(origin));
        var key = $"not-open-{instanceStatus}-{taskStatus}";
        var started = await _store.StartTrustedAsync(key);
        await _store.ShapeAsync(started, instanceStatus, taskStatus);

        var response = await _store.TerminalEvidenceAsync(started.WorkflowInstanceId, key);

        AssertEvidenceRefusal(response, WorkflowReasonCodes.WorkflowTerminalEvidenceInconsistent);
    }

    [Theory]
    [InlineData(Shaped, WorkflowInstanceStatus.Active)]
    [InlineData(Shaped, WorkflowInstanceStatus.Escalated)]
    public async Task Terminal_evidence_of_a_running_instance_with_two_open_tasks_is_inconsistent(
        string origin, WorkflowInstanceStatus instanceStatus)
    {
        Assert.False(IsReal(origin));
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
    [InlineData(Real, "cancelled")]                          // the public cancel
    [InlineData(Real, "trusted-cancelled")]                  // the trusted consumer's own cancel (MDM's maker withdrawal)
    [InlineData(Seam, "withdrawn-after-escalation")]         // allowEscalated, see the class summary: no production caller for a trusted instance
    public async Task Terminal_evidence_of_a_cancelled_approval_is_closed_without_decision(string origin, string path)
    {
        var started = await _store.ReachAsync(path, $"closed-{path}");
        Assert.Equal(WorkflowInstanceStatus.Cancelled, (await _store.Instances.GetByIdAsync(started.Started.WorkflowInstanceId))!.Status);

        var response = await _store.TerminalEvidenceAsync(started.Started.WorkflowInstanceId, started.Key);

        AssertEvidenceRefusal(response, WorkflowReasonCodes.WorkflowInstanceClosedWithoutDecision);
        Assert.Equal(ClosedCode, response.ReasonCode); // the literal the consumers match on
        await AssertNothingCanStillBeDecidedAsync(started);
        Assert.Equal(origin == Seam, path == "withdrawn-after-escalation");
    }

    // ── closed without a decision: timed out ─────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(Real, "timed-out")]                    // the SLA ran out on a waiting task
    [InlineData(Real, "timed-out-after-escalation")]   // escalated first, then the timeout
    public async Task Terminal_evidence_of_a_timed_out_approval_is_closed_without_decision(string origin, string path)
    {
        Assert.True(IsReal(origin));
        var started = await _store.ReachAsync(path, $"closed-{path}");
        Assert.Equal(WorkflowInstanceStatus.TimedOut, (await _store.Instances.GetByIdAsync(started.Started.WorkflowInstanceId))!.Status);

        var response = await _store.TerminalEvidenceAsync(started.Started.WorkflowInstanceId, started.Key);

        AssertEvidenceRefusal(response, WorkflowReasonCodes.WorkflowInstanceClosedWithoutDecision);
        Assert.Equal(ClosedCode, response.ReasonCode);
        await AssertNothingCanStillBeDecidedAsync(started);
    }

    // ── closed after a request for information: the closing task was WaitingEvidence ───────────────────────────

    [Theory]
    [InlineData(Real, "cancelled-after-request-info")]           // the public cancel of a WaitingEvidence task
    [InlineData(Real, "trusted-cancelled-after-request-info")]   // the trusted cancel of a WaitingEvidence task
    [InlineData(Real, "timed-out-after-request-info")]           // the SLA timeout of a WaitingEvidence task
    public async Task Terminal_evidence_of_an_approval_closed_while_it_asked_for_information_is_closed_without_decision(
        string origin, string path)
    {
        Assert.True(IsReal(origin));
        var started = await _store.ReachAsync(path, $"closed-{path}"); // ReachAsync asserts the task WaitingEvidence just before it closes
        var logs = await _store.Logs.ListByInstanceIdAsync(started.Started.WorkflowInstanceId);
        // non-vacuity: the closing transition left a task that was waiting for information (the trusted cancel's log
        // writes task and instance status in crossed columns, so either one is read)
        Assert.Contains(logs, l => l.Action is WorkflowTransitionAction.Cancel or WorkflowTransitionAction.Timeout
            && (l.FromState == nameof(ApprovalTaskStatus.WaitingEvidence) || l.FromStatus == nameof(ApprovalTaskStatus.WaitingEvidence)));

        var response = await _store.TerminalEvidenceAsync(started.Started.WorkflowInstanceId, started.Key);

        AssertEvidenceRefusal(response, WorkflowReasonCodes.WorkflowInstanceClosedWithoutDecision);
        await AssertNothingCanStillBeDecidedAsync(started);
    }

    [Theory]
    [InlineData(Real, "cancelled")]
    [InlineData(Real, "timed-out")]
    public async Task Terminal_evidence_of_a_multi_step_approval_closed_after_a_prior_approval_is_closed_without_decision(
        string origin, string closed)
    {
        // The prior-approval rule is unchanged: step one was approved (a coherent earlier approval), step two is open.
        Assert.True(IsReal(origin));
        var key = $"two-steps-{closed}";
        var templateId = await _store.StepTemplateAsync(twoSteps: true);
        var started = await _store.StartTrustedAsync(key, templateId: templateId);
        Assert.True((await _store.ApproveAsync(started.ApprovalTaskId, D(First), key + "-first")).IsSuccessful);
        AssertEvidenceRefusal(
            await _store.TerminalEvidenceAsync(started.WorkflowInstanceId, key),
            WorkflowReasonCodes.WorkflowDecisionNotTerminal);

        await _store.CloseSecondStepAsync(started, templateId, closed, key);

        var response = await _store.TerminalEvidenceAsync(started.WorkflowInstanceId, key);

        AssertEvidenceRefusal(response, WorkflowReasonCodes.WorkflowInstanceClosedWithoutDecision);
    }

    // ── facts that contradict each other stay "inconsistent" — never "closed" ─────────────────────────────────

    public static IEnumerable<object[]> ContradictoryClosingShapes()
    {
        string[] shapes =
        [
            "no-closing-log",                  // instance and task closed, no closing transition on record
            "wrong-closing-action",            // closed as the other kind (cancelled <-> timed out)
            "closing-log-not-last",            // something was written after the closing transition
            "closing-task-ended-the-other-way",// the closing log's task says the other ending
            "terminal-approve-before-close",   // an approve that finished the instance, then "closed"
            "terminal-reject-before-close",    // a reject, then "closed"
            "prior-approval-undone",           // two steps: the first step's task is no longer approved
            "prior-approval-actor-changed",    // two steps: the first step's task names another actor than its log
            "closing-action-mismatch",         // the last log is on the closing task and says the status, but is no close
            "closing-status-mismatch",         // the last log is a close of that task that says the instance stays active
            "approved-task-without-log"        // an approved task that has no approve log (a decision may exist)
        ];
        foreach (var closed in new[] { "cancelled", "timed-out" })
        {
            foreach (var shape in shapes)
            {
                yield return [Shaped, closed, shape];
            }
        }
    }

    [Theory]
    [MemberData(nameof(ContradictoryClosingShapes))]
    public async Task Terminal_evidence_of_contradictory_closing_facts_is_inconsistent(string origin, string closed, string shape)
    {
        Assert.False(IsReal(origin));
        var reached = await _store.ContradictoryClosureAsync(closed, shape, $"contradiction-{closed}-{shape}");

        var response = await _store.TerminalEvidenceAsync(reached.Started.WorkflowInstanceId, reached.Key);

        AssertEvidenceRefusal(response, WorkflowReasonCodes.WorkflowTerminalEvidenceInconsistent);
    }

    [Theory]
    [InlineData(Shaped, "cancelled", ApprovalTaskStatus.WaitingApproval)]
    [InlineData(Shaped, "cancelled", ApprovalTaskStatus.WaitingEvidence)]
    [InlineData(Shaped, "cancelled", ApprovalTaskStatus.Escalated)]
    [InlineData(Shaped, "cancelled", ApprovalTaskStatus.Delegated)]   // nothing in the engine writes Delegated
    [InlineData(Shaped, "timed-out", ApprovalTaskStatus.WaitingApproval)]
    [InlineData(Shaped, "timed-out", ApprovalTaskStatus.WaitingEvidence)]
    [InlineData(Shaped, "timed-out", ApprovalTaskStatus.Escalated)]
    [InlineData(Shaped, "timed-out", ApprovalTaskStatus.Delegated)]
    public async Task Terminal_evidence_of_a_closed_approval_whose_closing_task_can_be_decided_again_is_inconsistent(
        string origin, string closed, ApprovalTaskStatus taskStatus)
    {
        Assert.False(IsReal(origin));
        var reached = await _store.ReachAsync(closed, $"reopened-{closed}-{taskStatus}");
        await _store.SetTaskStatusAsync(reached.Started.ApprovalTaskId, taskStatus);

        var response = await _store.TerminalEvidenceAsync(reached.Started.WorkflowInstanceId, reached.Key);

        AssertEvidenceRefusal(response, WorkflowReasonCodes.WorkflowTerminalEvidenceInconsistent);
    }

    [Theory]
    [InlineData(Shaped, "cancelled", ApprovalTaskStatus.WaitingApproval)]
    [InlineData(Shaped, "cancelled", ApprovalTaskStatus.WaitingEvidence)]
    [InlineData(Shaped, "cancelled", ApprovalTaskStatus.Escalated)]
    [InlineData(Shaped, "cancelled", ApprovalTaskStatus.Delegated)]   // nothing in the engine writes Delegated
    [InlineData(Shaped, "cancelled", ApprovalTaskStatus.Rejected)]
    [InlineData(Shaped, "timed-out", ApprovalTaskStatus.WaitingApproval)]
    [InlineData(Shaped, "timed-out", ApprovalTaskStatus.WaitingEvidence)]
    [InlineData(Shaped, "timed-out", ApprovalTaskStatus.Escalated)]
    [InlineData(Shaped, "timed-out", ApprovalTaskStatus.Delegated)]
    [InlineData(Shaped, "timed-out", ApprovalTaskStatus.Rejected)]
    public async Task Terminal_evidence_of_a_closed_approval_with_another_task_that_is_not_closed_is_inconsistent(
        string origin, string closed, ApprovalTaskStatus otherTaskStatus)
    {
        Assert.False(IsReal(origin));
        var reached = await _store.ReachAsync(closed, $"other-task-{closed}-{otherTaskStatus}");
        await _store.AddTaskAsync(reached.Started, otherTaskStatus);

        var response = await _store.TerminalEvidenceAsync(reached.Started.WorkflowInstanceId, reached.Key);

        AssertEvidenceRefusal(response, WorkflowReasonCodes.WorkflowTerminalEvidenceInconsistent);
    }

    // ── read while the approval is closing: three reads are not a snapshot ───────────────────────────────────

    [Theory]
    [InlineData("approve", "tasks")]   // the transition commits between the instance read and the task read
    [InlineData("approve", "logs")]    // ... between the task read and the log read
    [InlineData("reject", "tasks")]
    [InlineData("reject", "logs")]
    [InlineData("cancel", "tasks")]
    [InlineData("cancel", "logs")]
    [InlineData("timeout", "tasks")]
    [InlineData("timeout", "logs")]
    public async Task Terminal_evidence_read_while_the_approval_closes_answers_the_closing_not_inconsistent(
        string closing, string window)
    {
        var key = $"race-{closing}-{window}";
        var reached = await _store.ReachAsync(closing == "timeout" ? "timeout-armed" : "open", key);
        var taskId = reached.Started.ApprovalTaskId;
        var seam = new ReadSeam(window, async () =>
        {
            switch (closing)
            {
                case "approve":
                    Assert.True((await _store.ApproveAsync(taskId, D(First), key + "-commit")).IsSuccessful);
                    break;
                case "reject":
                    Assert.True((await _store.RejectAsync(taskId, D(First), key + "-commit")).IsSuccessful);
                    break;
                case "cancel":
                    Assert.True((await _store.CancelAsync(taskId, key + "-commit")).IsSuccessful);
                    break;
                default:
                    await _store.RunEscalationsAsync();
                    break;
            }
        });

        var response = await _store.TerminalEvidenceAsync(reached.Started.WorkflowInstanceId, key, seam: seam);

        // non-vacuity: the commit landed INSIDE the handler's reads (the instance had been read as running)
        Assert.True(seam.Committed);
        // A cancel or a timeout that commits after the TASKS were read leaves facts that still hold together as "one open
        // task": the answer is the true-as-read "not terminal" (a consumer asks again) and there is nothing to look at again.
        // Everything else does not hold together, and the handler looks once more — and only once.
        var staleButCoherent = closing is "cancel" or "timeout" && window == "logs";
        Assert.Equal(staleButCoherent ? 1 : 2, seam.TaskReads);
        Assert.Equal(staleButCoherent ? 1 : 2, seam.LogReads);
        switch (closing)
        {
            case "approve":
            case "reject":
                Assert.True(response.IsSuccessful, response.ReasonCode);
                Assert.Equal(closing == "approve" ? "Approve" : "Reject", response.Data!.TerminalAction);
                break;
            default:
                if (staleButCoherent)
                {
                    AssertEvidenceRefusal(response, WorkflowReasonCodes.WorkflowDecisionNotTerminal);
                    // the next ask finds it closed
                    AssertEvidenceRefusal(
                        await _store.TerminalEvidenceAsync(reached.Started.WorkflowInstanceId, key),
                        WorkflowReasonCodes.WorkflowInstanceClosedWithoutDecision);
                }
                else
                {
                    AssertEvidenceRefusal(response, WorkflowReasonCodes.WorkflowInstanceClosedWithoutDecision);
                }

                break;
        }
    }

    [Fact]
    public async Task Terminal_evidence_looks_again_only_when_a_running_instance_does_not_hold_together_and_only_once()
    {
        // facts that hold together: one read of tasks and logs, no more
        var open = await _store.ReachAsync("open", "reads-open");
        var coherent = new ReadSeam(null, null);
        AssertEvidenceRefusal(
            await _store.TerminalEvidenceAsync(open.Started.WorkflowInstanceId, open.Key, seam: coherent),
            WorkflowReasonCodes.WorkflowDecisionNotTerminal);
        Assert.Equal((1, 1), (coherent.TaskReads, coherent.LogReads));

        // a decided approval: one read
        var decided = await _store.ReachAsync("open", "reads-decided");
        Assert.True((await _store.ApproveAsync(decided.Started.ApprovalTaskId, D(First), "reads-decided-approve")).IsSuccessful);
        var terminal = new ReadSeam(null, null);
        Assert.True((await _store.TerminalEvidenceAsync(decided.Started.WorkflowInstanceId, decided.Key, seam: terminal)).IsSuccessful);
        Assert.Equal((1, 1), (terminal.TaskReads, terminal.LogReads));

        // a running instance that still does not hold together after the second look: exactly two reads, inconsistent
        var broken = await _store.StartTrustedAsync("reads-broken");
        await _store.ShapeAsync(broken, WorkflowInstanceStatus.Active, ApprovalTaskStatus.Cancelled); // SHAPED
        var twice = new ReadSeam(null, null);
        AssertEvidenceRefusal(
            await _store.TerminalEvidenceAsync(broken.WorkflowInstanceId, "reads-broken", seam: twice),
            WorkflowReasonCodes.WorkflowTerminalEvidenceInconsistent);
        Assert.Equal((2, 2), (twice.TaskReads, twice.LogReads));

        // a CLOSED instance that does not hold together is not read again: nothing about a closed approval changes
        var closed = await _store.ReachAsync("cancelled", "reads-closed");
        await _store.AddTaskAsync(closed.Started, ApprovalTaskStatus.Approved); // SHAPED
        var once = new ReadSeam(null, null);
        AssertEvidenceRefusal(
            await _store.TerminalEvidenceAsync(closed.Started.WorkflowInstanceId, closed.Key, seam: once),
            WorkflowReasonCodes.WorkflowTerminalEvidenceInconsistent);
        Assert.Equal((1, 1), (once.TaskReads, once.LogReads));
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

    [Fact]
    public async Task Terminal_evidence_message_is_a_human_aid_and_not_a_contract()
    {
        // The code and the envelope are the contract (the tests above). The sentence only tells a person which ending it
        // was: no consumer may match on its words, and this test pins none — only that there is a sentence and that the two
        // endings do not share it.
        var cancelled = await _store.ReachAsync("cancelled", "message-cancelled");
        var timedOut = await _store.ReachAsync("timed-out", "message-timed-out");

        var cancelledMessage = Assert.Single((await _store.TerminalEvidenceAsync(
            cancelled.Started.WorkflowInstanceId, cancelled.Key)).Errors);
        var timedOutMessage = Assert.Single((await _store.TerminalEvidenceAsync(
            timedOut.Started.WorkflowInstanceId, timedOut.Key)).Errors);

        Assert.False(string.IsNullOrWhiteSpace(cancelledMessage));
        Assert.False(string.IsNullOrWhiteSpace(timedOutMessage));
        Assert.NotEqual(cancelledMessage, timedOutMessage);
    }

    // ── helpers ───────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>REAL → production code brought the state about; SHAPED and SEAM → something else did (see the class summary).</summary>
    private static bool IsReal(string origin) => origin switch
    {
        Real => true,
        Shaped or Seam => false,
        _ => throw new ArgumentOutOfRangeException(nameof(origin), origin, "a row is REAL, SHAPED or SEAM")
    };

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

    /// <summary>
    /// TEST-ONLY — not reachable from production DI. Wraps the task and the log repository the handler is given and, once,
    /// runs <c>commit</c> just before the first task read (<c>window</c> "tasks") or the first log read ("logs") — the
    /// instance having been read already — so a transition commits between the handler's reads. Counts the reads.
    /// </summary>
    private sealed class ReadSeam(string? window, Func<Task>? commit)
    {
        private bool _fired;
        public int TaskReads { get; private set; }
        public int LogReads { get; private set; }
        public bool Committed => _fired;

        public async Task BeforeTaskReadAsync()
        {
            TaskReads++;
            if (window == "tasks") await FireAsync();
        }

        public async Task BeforeLogReadAsync()
        {
            LogReads++;
            if (window == "logs") await FireAsync();
        }

        private async Task FireAsync()
        {
            if (_fired || commit is null) return;
            _fired = true;
            await commit();
        }
    }

    private sealed class SeamTasks(IApprovalTaskRepository inner, ReadSeam seam) : IApprovalTaskRepository
    {
        public async Task<IReadOnlyList<ApprovalTask>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default)
        {
            await seam.BeforeTaskReadAsync();
            return await inner.ListByInstanceIdAsync(workflowInstanceId, ct);
        }

        public Task<ApprovalTask?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<ApprovalTask> CreateAsync(ApprovalTask task, CancellationToken ct = default) => inner.CreateAsync(task, ct);
        public Task<ApprovalTask?> GetFirstByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            inner.GetFirstByInstanceIdAsync(workflowInstanceId, ct);
        public Task<ApprovalTask?> GetActiveByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            inner.GetActiveByInstanceIdAsync(workflowInstanceId, ct);
        public Task<IReadOnlyList<ApprovalTask>> GetAllForTenantAsync(CancellationToken ct = default) => inner.GetAllForTenantAsync(ct);
        public Task<bool> UpdateAsync(ApprovalTask task, int expectedVersion, CancellationToken ct = default) =>
            inner.UpdateAsync(task, expectedVersion, ct);
    }

    private sealed class SeamLogs(IWorkflowTransitionLogRepository inner, ReadSeam seam) : IWorkflowTransitionLogRepository
    {
        public async Task<IReadOnlyList<WorkflowTransitionLog>> ListByInstanceIdAsync(Guid workflowInstanceId, CancellationToken ct = default)
        {
            await seam.BeforeLogReadAsync();
            return await inner.ListByInstanceIdAsync(workflowInstanceId, ct);
        }

        public Task<WorkflowTransitionLog> CreateAsync(WorkflowTransitionLog log, CancellationToken ct = default) => inner.CreateAsync(log, ct);
        public Task<WorkflowTransitionLog?> GetByIdAsync(Guid id, CancellationToken ct = default) => inner.GetByIdAsync(id, ct);
        public Task<WorkflowTransitionLog?> GetByTaskActionIdempotencyKeyAsync(
            Guid approvalTaskId, WorkflowTransitionAction action, string idempotencyKey, CancellationToken ct = default) =>
            inner.GetByTaskActionIdempotencyKeyAsync(approvalTaskId, action, idempotencyKey, ct);
        public Task<long> GetLatestSequenceNoAsync(Guid workflowInstanceId, CancellationToken ct = default) =>
            inner.GetLatestSequenceNoAsync(workflowInstanceId, ct);
    }

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

        /// <summary>The real handler over the real repositories — or, with a <paramref name="seam"/>, over repositories
        /// that let a transition commit between its reads (test-only).</summary>
        public Task<Response<TrustedWorkflowTerminalDecisionEvidence>> TerminalEvidenceAsync(
            Guid instanceId, string key, Guid? clientId = null, string? objectId = null, ReadSeam? seam = null)
        {
            var handler = seam is null
                ? new GetTrustedWorkflowTerminalDecisionEvidenceHandler(Instances, Tasks, Logs)
                : new GetTrustedWorkflowTerminalDecisionEvidenceHandler(Instances, new SeamTasks(Tasks, seam), new SeamLogs(Logs, seam));
            return handler.Handle(
                new GetTrustedWorkflowTerminalDecisionEvidenceQuery(
                    instanceId, clientId ?? TrustedClient, "GlobalProduct", objectId ?? "GP-" + key, "corr-evidence-" + key),
                default);
        }

        /// <summary>SHAPED: the instance and its first task are given these statuses, over the real trusted graph.</summary>
        public async Task ShapeAsync(
            TrustedWorkflowStartResult started, WorkflowInstanceStatus instanceStatus, ApprovalTaskStatus taskStatus)
        {
            await SetTaskStatusAsync(started.ApprovalTaskId, taskStatus);
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
        /// with <paramref name="twoSteps"/>, a second one "step-b" whose only candidate is NextApprover.</summary>
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

        /// <summary>An SLA rule of a named step (EscalationRuleAsync names step-a).</summary>
        public Task StepRuleAsync(Guid templateId, string stepCode, IReadOnlyList<string> escalateTo, int? timeoutAfterMinutes) =>
            new SlaEscalationRuleRepository(Context, TenantContext).CreateAsync(new SlaEscalationRule
            {
                TenantId = TenantContext.TenantId,
                TemplateId = templateId,
                StageCode = "stage-1",
                StepCode = stepCode,
                DueInMinutes = 10,
                EscalateAfterMinutes = 10,
                TimeoutAfterMinutes = timeoutAfterMinutes,
                EscalationPrincipalIds = escalateTo.ToList()
            });

        private async Task AskForInformationAsync(TrustedWorkflowStartResult started, string key)
        {
            Assert.True((await RequestInfoAsync(started.ApprovalTaskId, D(First), key + "-info")).IsSuccessful);
            Assert.Equal(ApprovalTaskStatus.WaitingEvidence, (await Tasks.GetByIdAsync(started.ApprovalTaskId))!.Status);
        }

        /// <summary>
        /// Brings a fresh trusted approval to a named state. REAL (production code only): "open" (just started),
        /// "request-info" (a request for information), "escalated" (the SLA sweep), "cancelled" (the public cancel),
        /// "trusted-cancelled" (the trusted consumer's cancel), "timed-out" (the sweep, the timeout already due),
        /// "timed-out-after-escalation" (escalated first, the timeout comes later), "cancelled-after-request-info" /
        /// "trusted-cancelled-after-request-info" / "timed-out-after-request-info" (the closing task was WaitingEvidence),
        /// "timeout-armed" (overdue, with a timeout rule, not swept yet). SEAM: "withdrawn-after-escalation" (the withdraw of
        /// an escalated approval, allowEscalated — see the class summary).
        /// </summary>
        public async Task<Reached> ReachAsync(string state, string key)
        {
            var overdue = state is "escalated" or "withdrawn-after-escalation" or "timed-out" or "timed-out-after-escalation"
                or "timed-out-after-request-info" or "timeout-armed";
            var templateId = await StepTemplateAsync(twoSteps: false);
            var started = await StartTrustedAsync(key, overdue ? DateTimeOffset.UtcNow.AddMinutes(-5) : null, templateId);
            switch (state)
            {
                case "open":
                    break;
                case "request-info":
                    await AskForInformationAsync(started, key);
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
                case "cancelled-after-request-info":
                    await AskForInformationAsync(started, key);
                    Assert.True((await CancelAsync(started.ApprovalTaskId, key + "-cancel")).IsSuccessful);
                    break;
                case "trusted-cancelled-after-request-info":
                    await AskForInformationAsync(started, key);
                    await TrustedCancelAsync(started, key);
                    break;
                case "timed-out":
                    await EscalationRuleAsync(templateId, [D(CandidateA)], timeoutAfterMinutes: 10);
                    await RunEscalationsAsync();
                    break;
                case "timeout-armed":
                    await EscalationRuleAsync(templateId, [D(CandidateA)], timeoutAfterMinutes: 10);
                    break;
                case "timed-out-after-request-info":
                    await AskForInformationAsync(started, key);
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

        /// <summary>Step one is approved already; closes step two: cancelled (the public cancel) or timed out (the sweep,
        /// a rule on step-b and the task overdue).</summary>
        public async Task CloseSecondStepAsync(TrustedWorkflowStartResult started, Guid templateId, string closed, string key)
        {
            var second = Assert.Single(await Tasks.ListByInstanceIdAsync(started.WorkflowInstanceId),
                t => t.Status == ApprovalTaskStatus.WaitingApproval);
            if (closed == "cancelled")
            {
                Assert.True((await CancelAsync(second.Id, key + "-cancel")).IsSuccessful);
                return;
            }

            await MoveDueAsync(second.Id, TimeSpan.FromMinutes(5));
            await StepRuleAsync(templateId, "step-b", [D(CandidateA)], timeoutAfterMinutes: 10);
            await RunEscalationsAsync();
            Assert.Equal(WorkflowInstanceStatus.TimedOut, (await Instances.GetByIdAsync(started.WorkflowInstanceId))!.Status);
        }

        /// <summary>
        /// A closed approval whose facts do NOT hold together. Each shape starts from a real cancel / timeout / decision
        /// and then writes ONE contradiction beside it (SHAPED), so each is the evidence of one rule. <paramref name="closed"/>
        /// is "cancelled" or "timed-out"; the shapes are the same for both.
        /// </summary>
        public async Task<Reached> ContradictoryClosureAsync(string closed, string shape, string key)
        {
            var cancelled = closed == "cancelled";
            var closedInstance = cancelled ? WorkflowInstanceStatus.Cancelled : WorkflowInstanceStatus.TimedOut;
            var closedTask = cancelled ? ApprovalTaskStatus.Cancelled : ApprovalTaskStatus.TimedOut;
            var otherInstance = cancelled ? WorkflowInstanceStatus.TimedOut : WorkflowInstanceStatus.Cancelled;
            var otherTask = cancelled ? ApprovalTaskStatus.TimedOut : ApprovalTaskStatus.Cancelled;
            var closingAction = cancelled ? WorkflowTransitionAction.Cancel : WorkflowTransitionAction.Timeout;
            Reached reached;
            switch (shape)
            {
                case "no-closing-log":
                    reached = await ReachAsync("open", key);
                    await ShapeAsync(reached.Started, closedInstance, closedTask);
                    return reached;
                case "wrong-closing-action":
                    reached = await ReachAsync(closed, key);
                    await SetTaskStatusAsync(reached.Started.ApprovalTaskId, otherTask);
                    await SetInstanceStatusAsync(reached.Started.WorkflowInstanceId, otherInstance);
                    return reached;
                case "closing-log-not-last":
                    reached = await ReachAsync(closed, key);
                    await TakeNextSequenceNumberAsync(reached.Started.WorkflowInstanceId);
                    return reached;
                case "closing-task-ended-the-other-way":
                    reached = await ReachAsync(closed, key);
                    await SetTaskStatusAsync(reached.Started.ApprovalTaskId, otherTask);
                    return reached;
                case "terminal-approve-before-close":
                    reached = await ReachAsync("open", key);
                    Assert.True((await ApproveAsync(reached.Started.ApprovalTaskId, D(First), key + "-approve")).IsSuccessful);
                    await SetInstanceStatusAsync(reached.Started.WorkflowInstanceId, closedInstance);
                    return reached;
                case "terminal-reject-before-close":
                    reached = await ReachAsync("open", key);
                    Assert.True((await RejectAsync(reached.Started.ApprovalTaskId, D(First), key + "-reject")).IsSuccessful);
                    await SetInstanceStatusAsync(reached.Started.WorkflowInstanceId, closedInstance);
                    return reached;
                case "prior-approval-undone":
                case "prior-approval-actor-changed":
                {
                    var templateId = await StepTemplateAsync(twoSteps: true);
                    var started = await StartTrustedAsync(key, templateId: templateId);
                    Assert.True((await ApproveAsync(started.ApprovalTaskId, D(First), key + "-first")).IsSuccessful);
                    await CloseSecondStepAsync(started, templateId, closed, key);
                    var first = (await Tasks.GetByIdAsync(started.ApprovalTaskId))!;
                    if (shape == "prior-approval-undone")
                    {
                        first.Status = ApprovalTaskStatus.Cancelled;
                    }
                    else
                    {
                        first.ActionedBy = "somebody-else";
                    }

                    Assert.True(await Tasks.UpdateAsync(first, first.Version));
                    return new Reached(started, key);
                }
                case "closing-action-mismatch":
                    reached = await ReachAsync(closed, key);
                    await AppendLogAsync(reached.Started, WorkflowTransitionAction.Delegate, closedTask, closedInstance);
                    return reached;
                case "closing-status-mismatch":
                    reached = await ReachAsync(closed, key);
                    await AppendLogAsync(reached.Started, closingAction, closedTask, WorkflowInstanceStatus.Active);
                    return reached;
                case "approved-task-without-log":
                    reached = await ReachAsync(closed, key);
                    await AddTaskAsync(reached.Started, ApprovalTaskStatus.Approved);
                    return reached;
                default:
                    throw new ArgumentOutOfRangeException(nameof(shape), shape, null);
            }
        }

        /// <summary>SHAPED: one more transition log on the first task, at the next sequence number, naming the task and
        /// instance status it left behind.</summary>
        private async Task AppendLogAsync(
            TrustedWorkflowStartResult started, WorkflowTransitionAction action, ApprovalTaskStatus taskStatusAfter,
            WorkflowInstanceStatus instanceStatusAfter) =>
            await Logs.CreateAsync(new WorkflowTransitionLog
            {
                TenantId = TenantContext.TenantId,
                WorkflowInstanceId = started.WorkflowInstanceId,
                ApprovalTaskId = started.ApprovalTaskId,
                Action = action,
                ToState = taskStatusAfter.ToString(),
                ToStatus = instanceStatusAfter.ToString(),
                ActorId = "shaped-writer",
                ActorRef = "shaped-writer",
                ReasonCode = "SHAPED",
                IdempotencyKey = "shaped-" + Guid.NewGuid().ToString("N"),
                SequenceNo = await Logs.GetLatestSequenceNoAsync(started.WorkflowInstanceId) + 1
            });
    }
}
