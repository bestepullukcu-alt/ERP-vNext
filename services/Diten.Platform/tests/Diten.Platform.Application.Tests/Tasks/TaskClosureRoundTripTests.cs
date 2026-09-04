using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

using Task = System.Threading.Tasks.Task;

/// <summary>
/// Closing a task, end to end through the real handler — because the defect this slice repairs lived in exactly
/// the gap unit tests leave.
///
/// <para>Every piece of the write path already worked: <c>TaskTransitionRequest</c> accepts a ReasonCode, the
/// dispatcher forwards it, and the handler assigns it to <c>TaskItem.ClosureReasonCode</c> AND to the transition
/// log. The column was empty anyway, because the browser sent a literal <c>null</c>. So the lesson these tests
/// encode is the round trip: assert the VALUE that lands on the task, never merely that a field exists.</para>
/// </summary>
public sealed class TaskClosureRoundTripTests
{
    private const string Resolved = "RESOLVED";
    private const string Rejected = "REJECTED";

    private static TaskType TypeWithDictionary() => new()
    {
        TenantId = TaskTestData.Tenant,
        Code = "DEV",
        Name = "Deviation",
        ClosureOutcomes =
        [
            new TaskClosureOutcome
            {
                Code = Resolved,
                LabelText = "Çözüldü",
                Disposition = TaskClosureDisposition.Completed
            },
            new TaskClosureOutcome
            {
                Code = Rejected,
                LabelText = "Reddedildi",
                Disposition = TaskClosureDisposition.Completed,
                // ⭐ On the OUTCOME. Its sibling above does not require one, and that difference is the design.
                RequiresReason = true
            },
            new TaskClosureOutcome
            {
                Code = "SUPERSEDED",
                LabelText = "Yerini başka iş aldı",
                Disposition = TaskClosureDisposition.Cancelled
            }
        ]
    };

    private static readonly Guid UnitId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private static TaskItem OpenTask(Guid? typeId) => new()
    {
        TenantId = TaskTestData.Tenant,
        OrganizationUnitId = UnitId,
        Title = "Investigate the deviation",
        AssignmentTarget = TaskAssignmentTarget.Person,
        AssigneeUserId = TaskTestData.Me,
        // The actor is also the REQUESTER: cancelling is the requester's right and the handler enforces it
        // (TaskReasonCodes.CancelNotRequester). Without this the cancel cases would measure that rule instead of
        // the outcome dictionary they were written for.
        CreatedByUserId = TaskTestData.Me,
        Lifecycle = TaskLifecycle.InProgress,
        TaskTypeId = typeId
    };

    private static Task<Response<NoContent>> Close(
        FakeTaskItemRepository tasks,
        FakeTaskTypeRepository types,
        TaskItem task,
        TaskLifecycle target,
        string? reasonCode,
        string? note = null)
        => new TransitionTaskItemHandler(
                tasks,
                new TaskLifecycleService(),
                new FakeCurrentUserContext(TaskTestData.Me),
                new FakeChecklistRunRepository(),
                new TaskChecklistService(),
                new FakeWorkflowTransitionGate(),
                new FakeTaskDependencyRepository(),
                types,
                new FakeTaskNotificationService(),
                NullLogger<TransitionTaskItemHandler>.Instance)
            .Handle(
                new TransitionTaskItemCommand(
                    task.Id, target, new TaskTransitionRequest(task.Version, reasonCode, note), "corr"),
                CancellationToken.None);

    // ── The value actually lands ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_chosen_outcome_reaches_the_task_and_its_transition_log()
    {
        /*
         * ⚠ THE ASSERTION THE OLD SUITE NEVER MADE. 1500 tests were green while this column was null on every
         * row, because they checked that the transition happened and not what it recorded.
         */
        var type = TypeWithDictionary();
        var types = new FakeTaskTypeRepository(type);
        var task = OpenTask(type.Id);
        var tasks = new FakeTaskItemRepository(task);

        var result = await Close(tasks, types, task, TaskLifecycle.Done, Resolved);

        Assert.True(result.IsSuccessful);
        Assert.Equal(Resolved, task.ClosureReasonCode);
        Assert.NotNull(task.CompletedAt);

        // And on the log entry too — the report reads the transitions, not only the task's final state.
        var closing = tasks.Transitions.Events.Last();
        Assert.Equal(TaskTransitionKind.Completed, closing.Kind);
        Assert.Equal(Resolved, closing.ReasonCode);
    }

    [Fact]
    public async Task Cancelling_records_its_own_outcome_from_the_other_half_of_the_dictionary()
    {
        var type = TypeWithDictionary();
        var types = new FakeTaskTypeRepository(type);
        var task = OpenTask(type.Id);
        var tasks = new FakeTaskItemRepository(task);

        var result = await Close(tasks, types, task, TaskLifecycle.Cancelled, "SUPERSEDED");

        Assert.True(result.IsSuccessful);
        Assert.Equal("SUPERSEDED", task.ClosureReasonCode);
        Assert.NotNull(task.CancelledAt);
    }

    [Fact]
    public async Task The_code_is_matched_case_insensitively_because_it_is_normalized_at_rest()
    {
        var type = TypeWithDictionary();
        var types = new FakeTaskTypeRepository(type);
        var task = OpenTask(type.Id);
        var tasks = new FakeTaskItemRepository(task);

        Assert.True((await Close(tasks, types, task, TaskLifecycle.Done, "resolved")).IsSuccessful);
    }

    // ── The refusals ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_outcome_the_type_does_not_offer_is_refused_rather_than_stored()
    {
        /*
         * Stored, it would print as a raw code on that closed task forever — the dictionary exists precisely so
         * a closure reads as words. Refusing is also what stops one disposition's vocabulary leaking into the
         * other: SUPERSEDED is a cancellation outcome and must not complete anything.
         */
        var type = TypeWithDictionary();
        var types = new FakeTaskTypeRepository(type);
        var task = OpenTask(type.Id);
        var tasks = new FakeTaskItemRepository(task);

        var invented = await Close(tasks, types, task, TaskLifecycle.Done, "MADE_UP");
        Assert.False(invented.IsSuccessful);
        Assert.Equal(TaskReasonCodes.ClosureOutcomeUnknown, invented.ReasonCode);

        var wrongHalf = await Close(tasks, types, task, TaskLifecycle.Done, "SUPERSEDED");
        Assert.False(wrongHalf.IsSuccessful);
        Assert.Equal(TaskReasonCodes.ClosureOutcomeUnknown, wrongHalf.ReasonCode);

        // Refused BEFORE any mutation, exactly like the dependency and subtask gates beside it.
        Assert.Equal(TaskLifecycle.InProgress, task.Lifecycle);
        Assert.Null(task.ClosureReasonCode);
        Assert.Null(task.CompletedAt);
        Assert.Empty(tasks.Transitions.Events);
    }

    [Fact]
    public async Task A_type_that_offers_outcomes_will_not_accept_a_close_without_one()
    {
        var type = TypeWithDictionary();
        var types = new FakeTaskTypeRepository(type);
        var task = OpenTask(type.Id);
        var tasks = new FakeTaskItemRepository(task);

        var result = await Close(tasks, types, task, TaskLifecycle.Done, reasonCode: null);

        Assert.False(result.IsSuccessful);
        Assert.Equal(TaskReasonCodes.ClosureOutcomeRequired, result.ReasonCode);
        Assert.Equal(TaskLifecycle.InProgress, task.Lifecycle);
    }

    [Fact]
    public async Task The_reason_requirement_travels_on_the_outcome_and_not_on_a_global_setting()
    {
        /*
         * ⭐ THE STAR RULE, MEASURED BOTH WAYS FROM ONE DICTIONARY.
         *
         * REJECTED is refused without a note; RESOLVED — from the SAME type, in the SAME closure — is accepted
         * without one. A global "notes are mandatory" switch cannot produce this pair, so this test fails the
         * moment the flag is lifted off the outcome and hung above the list.
         */
        var type = TypeWithDictionary();

        var strictTypes = new FakeTaskTypeRepository(type);
        var strictTask = OpenTask(type.Id);
        var strictTasks = new FakeTaskItemRepository(strictTask);
        var refused = await Close(strictTasks, strictTypes, strictTask, TaskLifecycle.Done, Rejected);

        Assert.False(refused.IsSuccessful);
        Assert.Equal(TaskReasonCodes.ClosureReasonRequired, refused.ReasonCode);

        // With a note, the same outcome goes through — the rule is "say why", not "you may not choose this".
        var withNote = await Close(strictTasks, strictTypes, strictTask, TaskLifecycle.Done, Rejected, "Root cause not confirmed");
        Assert.True(withNote.IsSuccessful);
        Assert.Equal(Rejected, strictTask.ClosureReasonCode);

        // And the lenient sibling needs nothing.
        var lenientTypes = new FakeTaskTypeRepository(type);
        var lenientTask = OpenTask(type.Id);
        var lenientTasks = new FakeTaskItemRepository(lenientTask);
        Assert.True((await Close(lenientTasks, lenientTypes, lenientTask, TaskLifecycle.Done, Resolved)).IsSuccessful);
    }

    [Fact]
    public async Task Whitespace_is_not_a_reason()
    {
        // A required field satisfied by a space bar is not a required field.
        var type = TypeWithDictionary();
        var types = new FakeTaskTypeRepository(type);
        var task = OpenTask(type.Id);
        var tasks = new FakeTaskItemRepository(task);

        var result = await Close(tasks, types, task, TaskLifecycle.Done, Rejected, "   ");

        Assert.False(result.IsSuccessful);
        Assert.Equal(TaskReasonCodes.ClosureReasonRequired, result.ReasonCode);
    }

    // ── The compatibility rule, at the endpoint ──────────────────────────────────────────────────────────

    [Fact]
    public async Task A_task_whose_type_has_no_dictionary_closes_exactly_as_it_always_did()
    {
        /*
         * ⚠ THE ONE THAT MUST NEVER GO RED. A hundred-odd tasks are open against types with no outcomes, and an
         * unclassified task has no type at all. If the gate ever fires for them, none of that work can be closed
         * by anyone — the feature would break the product it was added to.
         *
         * Both shapes, and both closures, because "it worked for complete" is how the third branch gets missed.
         */
        var bare = new TaskType { TenantId = TaskTestData.Tenant, Code = "OLD", Name = "Old" };
        var types = new FakeTaskTypeRepository(bare);

        var classified = OpenTask(bare.Id);
        var classifiedTasks = new FakeTaskItemRepository(classified);
        Assert.True((await Close(classifiedTasks, types, classified, TaskLifecycle.Done, reasonCode: null)).IsSuccessful);
        Assert.Null(classified.ClosureReasonCode);

        var unclassified = OpenTask(typeId: null);
        var unclassifiedTasks = new FakeTaskItemRepository(unclassified);
        Assert.True((await Close(unclassifiedTasks, types, unclassified, TaskLifecycle.Cancelled, reasonCode: null)).IsSuccessful);
        Assert.Equal(TaskLifecycle.Cancelled, unclassified.Lifecycle);
    }

    [Fact]
    public async Task Cancelling_a_parent_does_not_stamp_its_outcome_onto_the_children()
    {
        /*
         * ⚠ A DEFECT THIS SLICE WOULD HAVE ACTIVATED, found while wiring it.
         *
         * `CancelOpenSubtasksAsync` used to copy `command.Request.ReasonCode` onto every child it called off.
         * That was invisible for as long as the value was always null. With a real outcome flowing, the code
         * comes from the PARENT's type dictionary and a subtask may be a different type — so the child would
         * store a code its own type does not offer, resolve to no label, and print a raw code forever.
         *
         * The child is still recorded as cancelled, in its own feed. What it does not get is a classification
         * that was never about it.
         */
        var type = TypeWithDictionary();
        var types = new FakeTaskTypeRepository(type);
        var parent = OpenTask(type.Id);
        var child = OpenTask(typeId: null);
        child.ParentTaskItemId = parent.Id;
        var tasks = new FakeTaskItemRepository(parent, child);

        Assert.True((await Close(tasks, types, parent, TaskLifecycle.Cancelled, "SUPERSEDED")).IsSuccessful);

        Assert.Equal("SUPERSEDED", parent.ClosureReasonCode);
        Assert.Equal(TaskLifecycle.Cancelled, child.Lifecycle);
        Assert.NotNull(child.CancelledAt);
        Assert.Null(child.ClosureReasonCode);
        Assert.Null(tasks.Transitions.Events.Last(e => e.TaskItemId == child.Id).ReasonCode);
    }

    [Fact]
    public async Task A_non_closing_transition_never_consults_the_dictionary()
    {
        /*
         * Starting work is not closing it. Without this, a type with a dictionary would demand an outcome on
         * `start` — and the picker would have nothing to offer, because the dictionary is about endings.
         */
        var type = TypeWithDictionary();
        var types = new FakeTaskTypeRepository(type);
        var task = OpenTask(type.Id);
        task.Lifecycle = TaskLifecycle.Open;
        var tasks = new FakeTaskItemRepository(task);

        Assert.True((await Close(tasks, types, task, TaskLifecycle.InProgress, reasonCode: null)).IsSuccessful);
        Assert.Null(TransitionTaskItemHandler.ClosureDispositionFor(TaskLifecycle.InProgress));
    }
}
