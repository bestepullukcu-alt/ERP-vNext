using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

using Task = System.Threading.Tasks.Task;

/// <summary>
/// "This work came back" — the signal, and why it is a signal rather than a lifecycle state.
///
/// <para><b>The defect.</b> The return ACTION has worked since WC-1: it demands a reason, refuses anyone but the
/// assignee, hands the task back to its requester and records
/// <c>TaskTransitionKind.Returned</c> with the returner's own sentence. None of it reached the browser —
/// MEASURED 2026-09-03, <c>"Returned"</c> appeared in ZERO lines of <c>TaskWorkItemProvider</c>. So a returned
/// task landed in the requester's inbox indistinguishable from one raised that morning, and the sentence
/// explaining why was written, stored, and never shown to the person it was written for.</para>
///
/// <para><b>Why not a lifecycle state.</b> The task really is <c>Open</c> — somebody has to do it. "It has been
/// here before" is ORIGIN, and a new <c>TaskLifecycle</c> member would cost a persisted enum on a document
/// store, the frontend contract's own list, every switch over either, and every provider that maps its native
/// status into ours. These tests pin the signal AND the absence of that state.</para>
/// </summary>
public sealed class TaskReturnedSignalTests
{
    private static readonly Guid UnitId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private static TaskItem Task(TaskLifecycle lifecycle = TaskLifecycle.Open) => new()
    {
        TenantId = TaskTestData.Tenant,
        OrganizationUnitId = UnitId,
        Title = "Draft the deviation report",
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        CreatedByUserId = TaskTestData.Me,
        Lifecycle = lifecycle,
        Version = 1
    };

    private static TaskTransition Transition(
        Guid taskId, TaskTransitionKind kind, string? reason, DateTimeOffset at) => new()
    {
        TenantId = TaskTestData.Tenant,
        TaskItemId = taskId,
        Kind = kind,
        FromLifecycle = TaskLifecycle.InProgress,
        ToLifecycle = TaskLifecycle.Open,
        ActorUserId = TaskTestData.Me,
        Reason = reason,
        CreatedAt = at
    };

    private static (TaskWorkItemProvider Provider, FakeTaskTransitionRepository Transitions) Build(
        TaskItem task, params TaskTransition[] history)
    {
        var transitions = new FakeTaskTransitionRepository();
        foreach (var entry in history)
        {
            transitions.CreateAsync(entry).GetAwaiter().GetResult();
        }

        var provider = new TaskWorkItemProvider(
            new FakeTaskItemRepository(task),
            new FakePositionAssignmentRepository(),
            new TaskLifecycleService(),
            new TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver(),
            new FakeChecklistRunRepository(),
            new FakeTaskApprovalService(),
            new FakeTaskDependencyRepository(),
            new FakeTaskCommentRepository(),
            transitions,
            new FakeTaskPersonalOverlayRepository(),
            new FakeTaskWatcherRepository(),
            TaskActors.PermitAll(),
            new FakePositionRepository(),
            new FakeOrganizationUnitRepository(),
            SlaForTests.Real(),
            new FakeTaskFieldDefinitionRepository(),
            new FakeTaskTypeRepository());

        return (provider, transitions);
    }

    private static WorkItemActor Actor() => new(TaskTestData.Me, IsPlatformActor: true, new HashSet<string>());

    private static async Task<WorkItemProjectionDto> ProjectAsync(
        TaskItem task, params TaskTransition[] history)
    {
        var (provider, _) = Build(task, history);
        return Assert.Single(await provider.GetWorkItemsAsync(Actor(), CancellationToken.None));
    }

    // ── (a) A TASK THAT NEVER CAME BACK CARRIES NOTHING ──────────────────────────────────────────────────

    [Fact]
    public async Task A_task_that_was_never_returned_carries_no_signal_at_all()
    {
        /*
         * NULL, not a zero-count object. "Never returned" is an ABSENCE — the overwhelming majority of tasks —
         * and an object reading `count: 0` is a signal every reader has to inspect before it can be ignored.
         * The shell then has to remember to test the number rather than the presence, and that is the shape of
         * the `viewerRole` defect this contract has already paid for.
         */
        var item = await ProjectAsync(Task());

        Assert.Null(item.Returned);
    }

    [Fact]
    public async Task Other_transitions_are_not_mistaken_for_a_return()
    {
        /*
         * A return and a reassignment-to-the-requester leave byte-identical documents behind — same new holder,
         * same reopened gate, same rewound lifecycle. Only the declared KIND separates them, which is exactly
         * why the handler declares it instead of inferring it. This proves the signal reads that kind and not
         * some property the two acts share.
         */
        var task = Task();
        var item = await ProjectAsync(
            task,
            Transition(task.Id, TaskTransitionKind.Reassigned, "handing this over", DateTimeOffset.UtcNow.AddDays(-2)),
            Transition(task.Id, TaskTransitionKind.Started, null, DateTimeOffset.UtcNow.AddDays(-1)));

        Assert.Null(item.Returned);
    }

    // ── (b) A RETURNED TASK CARRIES THE SIGNAL AND THE SENTENCE ──────────────────────────────────────────

    [Fact]
    public async Task A_returned_task_carries_when_it_came_back_and_why_in_the_returners_own_words()
    {
        var task = Task();
        var at = DateTimeOffset.UtcNow.AddDays(-1);
        var item = await ProjectAsync(task, Transition(task.Id, TaskTransitionKind.Returned, "Wrong batch number", at));

        Assert.NotNull(item.Returned);
        Assert.Equal(at, item.Returned!.At);
        Assert.Equal(1, item.Returned.Count);

        // A DISPLAY label, never a resource key: it is a sentence a person typed, in their own language.
        Assert.NotNull(item.Returned.Reason);
        Assert.Equal(WorkItemContract.LabelDisplay, item.Returned.Reason!.Kind);
        Assert.Equal("Wrong batch number", item.Returned.Reason.Text);
        Assert.Null(item.Returned.Reason.Key);
    }

    [Fact]
    public async Task The_lifecycle_is_still_Open_and_no_new_state_was_invented()
    {
        /*
         * ⚠ THE SCOPE GUARD. If a `Returned` lifecycle member is ever added, this is where the decision gets
         * argued again rather than slipped in: the task IS open — somebody has to do it — and the signal beside
         * it says where it came from. Two facts, two fields.
         */
        var task = Task();
        var item = await ProjectAsync(task, Transition(task.Id, TaskTransitionKind.Returned, "Not my remit", DateTimeOffset.UtcNow));

        Assert.NotNull(item.Returned);
        Assert.Equal(nameof(TaskLifecycle.Open), item.TaskLifecycle);
        Assert.DoesNotContain("Returned", Enum.GetNames<TaskLifecycle>());
    }

    // ── (c) THE COUNT ────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Returned_twice_counts_both_and_quotes_the_MOST_RECENT_sentence()
    {
        /*
         * The latest return is the one the reader is being asked about; the count beside it says the earlier
         * ones happened. Quoting the FIRST sentence would explain a decision that has since been superseded.
         *
         * The count is deliberately a COUNT and not a rate — a rate needs a denominator and a period, and
         * inventing either inside a per-item projection would be a second answer to a question the report
         * (Faz 5) has not asked yet.
         */
        var task = Task();
        var older = DateTimeOffset.UtcNow.AddDays(-5);
        var newer = DateTimeOffset.UtcNow.AddDays(-1);

        var item = await ProjectAsync(
            task,
            Transition(task.Id, TaskTransitionKind.Returned, "Missing the attachment", older),
            Transition(task.Id, TaskTransitionKind.Started, null, older.AddHours(1)),
            Transition(task.Id, TaskTransitionKind.Returned, "Still missing the attachment", newer));

        Assert.Equal(2, item.Returned!.Count);
        Assert.Equal(newer, item.Returned.At);
        Assert.Equal("Still missing the attachment", item.Returned.Reason!.Text);
    }

    // ── The signal survives closure, and that is a decision ──────────────────────────────────────────────

    [Theory]
    [InlineData(TaskLifecycle.Done)]
    [InlineData(TaskLifecycle.Cancelled)]
    public async Task A_finished_task_still_reports_that_it_came_back(TaskLifecycle terminal)
    {
        /*
         * ⚠ MEASURED DECISION, not an oversight.
         *
         * The row CHIP is a triage signal and the shell hides it on finished work — there is nothing to triage
         * on a task nobody has to pick up. The projection is a different job: it states what is true, and "this
         * came back twice before it was finished" is part of what happened to the work. The DETAIL page reads
         * that; the rework count (Faz 5) will read it too.
         *
         * Deciding it here — by withholding the fact — would answer the detail page's question with the inbox's
         * reasoning. `closedAt` gets the same split: emitted as fact, drawn selectively.
         */
        var task = Task(terminal);
        var item = await ProjectAsync(task, Transition(task.Id, TaskTransitionKind.Returned, "Sent back once", DateTimeOffset.UtcNow.AddDays(-3)));

        Assert.NotNull(item.Returned);
        Assert.Equal(1, item.Returned!.Count);
    }

    // ── (d) DERIVED FROM HISTORY ALREADY READ ────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_signal_costs_no_extra_read_because_it_is_derived_from_the_batch()
    {
        /*
         * ⚠ THE N+1 GUARD. The page reads every task's history in ONE batched call so an activity feed does not
         * cost a round-trip per row. A new field derived from that same history is precisely how the per-task
         * read creeps back — someone adds `_transitions.ListByTaskIdAsync(task.Id)` inside the projection loop
         * and nothing fails.
         *
         * So this asserts the SHAPE of the reads, not their count alone: one batched call, and never the
         * per-task variant.
         */
        var task = Task();
        var (provider, transitions) = Build(
            task, Transition(task.Id, TaskTransitionKind.Returned, "Back to you", DateTimeOffset.UtcNow));

        var items = await provider.GetWorkItemsAsync(Actor(), CancellationToken.None);

        Assert.NotNull(Assert.Single(items).Returned);
        Assert.Equal(1, transitions.ListByTaskIdsCalls);
        Assert.Equal(0, transitions.ListByTaskIdCalls);
    }
}
