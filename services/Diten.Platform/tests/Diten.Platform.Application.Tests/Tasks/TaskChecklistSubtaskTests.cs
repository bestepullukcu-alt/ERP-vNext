using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// MOD-0024 Phase 2 — checklist and subtasks (pack §12 E1/E2).
///
/// <para>Two rules carry most of the weight, and they are deliberately different:</para>
/// <list type="bullet">
/// <item>A <b>blocking checklist item</b> stops completion, and the SERVER enforces it — disabling a button is
/// presentation, refusing the write is the rule.</item>
/// <item>An <b>open subtask</b> does NOT stop completion. Two competing blocking mechanisms would make "why can't
/// I finish this?" unanswerable.</item>
/// </list>
/// </summary>
public sealed class TaskChecklistSubtaskTests
{
    // ── The checklist gate ────────────────────────────────────────────────────

    [Fact]
    public async Task An_open_BLOCKING_item_disables_complete_and_says_why()
    {
        var task = InProgressTask();
        var runs = new FakeChecklistRunRepository(RunFor(task.Id, (Blocking: true, Completed: false)));

        var item = await ProjectOne(task, runs);

        var complete = Assert.Single(item.Actions, a => a.Code == "complete");
        Assert.False(complete.Enabled);
        Assert.Equal(TaskReasonCodes.ChecklistIncomplete, complete.DisabledReasonCode);
        Assert.NotNull(complete.DisabledReason);
    }

    [Fact]
    public async Task Ticking_the_last_blocking_item_enables_complete()
    {
        var task = InProgressTask();
        var runs = new FakeChecklistRunRepository(RunFor(task.Id, (Blocking: true, Completed: true)));

        var item = await ProjectOne(task, runs);

        Assert.True(Assert.Single(item.Actions, a => a.Code == "complete").Enabled);
    }

    [Fact]
    public async Task A_merely_REQUIRED_item_does_not_block_completion()
    {
        // "Required" means expected, not mandatory-to-finish. Only Blocking gates.
        var task = InProgressTask();
        var run = RunFor(task.Id);
        run.Items.Add(new ChecklistRunItem
        {
            Code = "req", LabelResourceKey = "K", Requirement = ChecklistItemRequirement.Required, Completed = false
        });
        var runs = new FakeChecklistRunRepository(run);

        var item = await ProjectOne(task, runs);

        Assert.True(Assert.Single(item.Actions, a => a.Code == "complete").Enabled);
    }

    [Fact]
    public async Task The_SERVER_refuses_completion_even_when_the_caller_ignores_the_disabled_button()
    {
        // The projection can only disable a control; a caller can POST straight to the endpoint.
        var task = InProgressTask();
        var tasks = new FakeTaskItemRepository(task);
        var runs = new FakeChecklistRunRepository(RunFor(task.Id, (Blocking: true, Completed: false)));

        var result = await Transition(tasks, runs, task.Id, TaskLifecycle.Done, task.Version);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(TaskReasonCodes.ChecklistIncomplete, result.ReasonCode);
        // And nothing moved.
        Assert.Equal(TaskLifecycle.InProgress, tasks.Items.Single().Lifecycle);
    }

    [Fact]
    public async Task The_server_allows_completion_once_the_blocking_item_is_done()
    {
        var task = InProgressTask();
        var tasks = new FakeTaskItemRepository(task);
        var runs = new FakeChecklistRunRepository(RunFor(task.Id, (Blocking: true, Completed: true)));

        var result = await Transition(tasks, runs, task.Id, TaskLifecycle.Done, task.Version);

        Assert.True(result.IsSuccessful);
        Assert.Equal(TaskLifecycle.Done, tasks.Items.Single().Lifecycle);
    }

    [Fact]
    public async Task Ticking_an_item_persists_and_the_next_projection_shows_it()
    {
        var task = InProgressTask();
        var tasks = new FakeTaskItemRepository(task);
        var run = RunFor(task.Id, (Blocking: true, Completed: false));
        var runs = new FakeChecklistRunRepository(run);

        var before = await ProjectOne(task, runs);
        Assert.False(before.Checklist!.Items.Single().Completed);

        var handler = new SetChecklistItemStateHandler(
            tasks, runs, new TaskChecklistService(), new FakeCurrentUserContext(TaskTestData.Me));
        var result = await handler.Handle(
            new SetChecklistItemStateCommand(
                task.Id, new SetChecklistItemStateRequest("i0", true, run.Version), "corr"),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        var after = await ProjectOne(task, runs);
        Assert.True(after.Checklist!.Items.Single().Completed);
        Assert.True(Assert.Single(after.Actions, a => a.Code == "complete").Enabled);
    }

    // ── Label form: resource vs display ───────────────────────────────────────

    [Fact]
    public async Task A_template_item_keeps_its_resource_key_and_an_ad_hoc_item_carries_TEXT()
    {
        var task = InProgressTask();
        var run = RunFor(task.Id);
        run.Items.Add(new ChecklistRunItem { Code = "tpl", LabelResourceKey = "WorkAggregation_Check_Verify" });
        run.Items.Add(new ChecklistRunItem { Code = "adhoc", LabelText = "Call the supplier back" });

        var item = await ProjectOne(task, new FakeChecklistRunRepository(run));

        var template = item.Checklist!.Items.Single(i => i.Id == "tpl");
        Assert.Equal(WorkItemContract.LabelResource, template.Label.Kind);
        Assert.Equal("WorkAggregation_Check_Verify", template.Label.Key);

        var adhoc = item.Checklist.Items.Single(i => i.Id == "adhoc");
        // Typed text as a DISPLAY label — routing it through a resource key is what renders the key itself.
        Assert.Equal(WorkItemContract.LabelDisplay, adhoc.Label.Kind);
        Assert.Equal("Call the supplier back", adhoc.Label.Text);
        Assert.Null(adhoc.Label.Key);
    }

    [Fact]
    public async Task An_ad_hoc_item_added_through_the_API_is_stored_as_text_not_a_key()
    {
        var task = InProgressTask();
        var tasks = new FakeTaskItemRepository(task);
        var runs = new FakeChecklistRunRepository();

        var handler = new AddChecklistItemHandler(
            tasks, runs, new TaskChecklistService(),
            new FakeCurrentUserContext(TaskTestData.Me), new FakeTenantContext(TaskTestData.Tenant));

        var result = await handler.Handle(
            new AddChecklistItemCommand(
                task.Id, new AddChecklistItemRequest("Ring the auditor", ChecklistItemRequirement.Blocking, 0), "corr"),
            CancellationToken.None);

        Assert.True(result.IsSuccessful);
        var stored = Assert.Single(runs.Runs).Items.Single();
        Assert.Equal("Ring the auditor", stored.LabelText);
        Assert.Null(stored.LabelResourceKey);
    }

    // ── Subtasks ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task An_open_subtask_BLOCKS_the_parent()
    {
        /*
         * This assertion used to say the opposite, and the reversal is the point (BL-035, owner decision
         * 2026-07-29): "the work was split into three, two were never done, and the whole thing is complete" is
         * not a sentence a task engine should be able to produce.
         *
         * The old objection — two blocking mechanisms make "why can't I finish this?" unanswerable — is answered
         * by blockedState.blockers[], which now names each blocker individually and did not exist then.
         */
        var parent = InProgressTask();
        var child = SubtaskOf(parent.Id, TaskLifecycle.Open);
        var tasks = new FakeTaskItemRepository(parent, child);
        var runs = new FakeChecklistRunRepository();

        var item = Assert.Single(
            (await Provider(tasks, runs).GetWorkItemsAsync(FullyPermittedActor(), CancellationToken.None))
            .Where(i => i.Id == parent.Id.ToString()));
        // Visible and disabled, never hidden — the reader has to be able to see what they cannot do, and why.
        var complete = Assert.Single(item.Actions, a => a.Code == "complete");
        Assert.False(complete.Enabled);
        Assert.Equal(WorkAggregationReasonCodes.SubtaskBlocked, complete.DisabledReasonCode);

        var result = await Transition(tasks, runs, parent.Id, TaskLifecycle.Done, parent.Version);
        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(TaskReasonCodes.SubtaskBlocked, result.ReasonCode);
    }

    [Fact]
    public async Task A_subtask_is_projected_on_its_parent_AND_as_its_own_row()
    {
        var parent = InProgressTask();
        var child = SubtaskOf(parent.Id, TaskLifecycle.Open);
        var items = await Provider(new FakeTaskItemRepository(parent, child), new FakeChecklistRunRepository(), new FakeTaskApprovalService())
            .GetWorkItemsAsync(FullyPermittedActor(), CancellationToken.None);

        // Its own row — it is assigned to me, so I must be able to work it directly.
        var childRow = Assert.Single(items, i => i.Id == child.Id.ToString());
        Assert.Equal(parent.Id.ToString(), childRow.ParentTaskItemId);
        // A subtask has no subtasks of its own.
        Assert.DoesNotContain("subtasks", childRow.WorkItemCapabilities);
        Assert.Null(childRow.Subtasks);

        // …and listed under the parent.
        var parentRow = Assert.Single(items, i => i.Id == parent.Id.ToString());
        Assert.Equal("full", parentRow.Subtasks!.Mode);
        Assert.Equal(child.Title, Assert.Single(parentRow.Subtasks.Items).Title);
        Assert.Equal("not-started", Assert.Single(parentRow.Subtasks.Items).Status);
    }

    [Fact]
    public async Task A_subtask_of_a_subtask_is_refused()
    {
        var parent = TopLevel();
        var child = SubtaskOf(parent.Id, TaskLifecycle.Open);
        var tasks = new FakeTaskItemRepository(parent, child);

        var result = await CreateSubtask(tasks, parentId: child.Id);

        Assert.False(result.IsSuccessful);
        Assert.Equal(TaskReasonCodes.SubtaskDepthExceeded, result.ReasonCode);
    }

    [Fact]
    public async Task A_subtask_under_a_top_level_task_is_created()
    {
        var parent = TopLevel();
        var tasks = new FakeTaskItemRepository(parent);

        var result = await CreateSubtask(tasks, parentId: parent.Id);

        Assert.True(result.IsSuccessful);
        Assert.Equal(parent.Id, tasks.Items.Single(t => t.Id != parent.Id).ParentTaskItemId);
    }

    [Fact]
    public async Task Another_tenants_task_cannot_be_used_as_a_parent()
    {
        var foreign = new TaskItem
        {
            TenantId = TaskTestData.OtherTenant,
            Title = "Another tenant's task",
            AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
            AssigneeUserId = TaskTestData.Me,
            OrganizationUnitId = Guid.NewGuid(),
            Lifecycle = TaskLifecycle.Open,
            Version = 1
        };
        var tasks = new FakeTaskItemRepository(foreign);

        var result = await CreateSubtask(tasks, parentId: foreign.Id);

        // Not "forbidden" — invisible. The tenant filter means it simply does not exist here.
        Assert.False(result.IsSuccessful);
        Assert.Equal(TaskReasonCodes.ParentTaskNotFound, result.ReasonCode);
    }

    [Fact]
    public async Task Cancelling_a_parent_cancels_its_OPEN_subtasks_but_rewrites_no_history()
    {
        var parent = InProgressTask();
        var open = SubtaskOf(parent.Id, TaskLifecycle.Open);
        var running = SubtaskOf(parent.Id, TaskLifecycle.InProgress);
        var finished = SubtaskOf(parent.Id, TaskLifecycle.Done);
        var tasks = new FakeTaskItemRepository(parent, open, running, finished);

        var result = await Transition(tasks, new FakeChecklistRunRepository(), parent.Id, TaskLifecycle.Cancelled, parent.Version);

        Assert.True(result.IsSuccessful);
        // Open work is called off with its parent — leaving it would strand work nobody can contextualize.
        Assert.Equal(TaskLifecycle.Cancelled, tasks.Items.Single(t => t.Id == open.Id).Lifecycle);
        Assert.Equal(TaskLifecycle.Cancelled, tasks.Items.Single(t => t.Id == running.Id).Lifecycle);
        // Already finished stays finished: cancellation does not undo completed work.
        Assert.Equal(TaskLifecycle.Done, tasks.Items.Single(t => t.Id == finished.Id).Lifecycle);
    }

    [Fact]
    public async Task A_subtask_runs_its_own_lifecycle()
    {
        var parent = TopLevel();
        var child = SubtaskOf(parent.Id, TaskLifecycle.Open);
        var tasks = new FakeTaskItemRepository(parent, child);

        var result = await Transition(tasks, new FakeChecklistRunRepository(), child.Id, TaskLifecycle.InProgress, child.Version);

        Assert.True(result.IsSuccessful);
        Assert.Equal(TaskLifecycle.InProgress, tasks.Items.Single(t => t.Id == child.Id).Lifecycle);
        // The parent is untouched by its child's progress.
        Assert.Equal(TaskLifecycle.Open, tasks.Items.Single(t => t.Id == parent.Id).Lifecycle);
    }

    // ── Capability ⇔ container, both directions ───────────────────────────────

    [Fact]
    public async Task A_task_with_no_checklist_STILL_declares_the_container_so_a_first_item_can_be_added()
    {
        /*
         * This assertion is the reverse of what it said until the create/add round, and the reversal is the
         * point. While the only way to get a checklist was to name a template at creation, a task with no run
         * could never grow one, so declaring an empty container would have been an offer the product could not
         * keep. The shell now has an add row on both the create form and the detail page — and under the old
         * rule that row was unreachable exactly where it was needed: a task with no run declared no capability,
         * the card was never drawn, and the only place to add a first item was a task that already had one.
         *
         * `subtasks` has always worked this way for the same reason. Version 0 says "no run exists yet", which
         * is the branch AddChecklistItemHandler already takes when it finds none.
         */
        var item = await ProjectOne(InProgressTask(), new FakeChecklistRunRepository(), new FakeTaskApprovalService());

        Assert.Contains("checklist", item.WorkItemCapabilities);
        Assert.NotNull(item.Checklist);
        Assert.Empty(item.Checklist!.Items);
        Assert.Equal(0, item.Checklist.Version);
    }

    [Fact]
    public async Task A_task_WITH_a_checklist_declares_both_even_when_every_item_is_done()
    {
        var task = InProgressTask();
        var item = await ProjectOne(task, new FakeChecklistRunRepository(RunFor(task.Id, (Blocking: false, Completed: true))));

        Assert.Contains("checklist", item.WorkItemCapabilities);
        Assert.NotNull(item.Checklist);
    }

    [Fact]
    public async Task A_parent_declares_the_subtasks_container_even_with_no_children_yet()
    {
        // Capability without data is valid; data without capability is not. The shell needs the container to
        // offer "add a subtask".
        var item = await ProjectOne(InProgressTask(), new FakeChecklistRunRepository(), new FakeTaskApprovalService());

        Assert.Contains("subtasks", item.WorkItemCapabilities);
        Assert.NotNull(item.Subtasks);
        Assert.Empty(item.Subtasks!.Items);
    }

    [Fact]
    public async Task The_page_is_read_in_batches_not_once_per_task()
    {
        var tasks = Enumerable.Range(0, 12).Select(_ => InProgressTask()).ToArray();
        var runs = new FakeChecklistRunRepository(tasks.Select(t => RunFor(t.Id)).ToArray());

        await Provider(new FakeTaskItemRepository(tasks), runs)
            .GetWorkItemsAsync(FullyPermittedActor(), CancellationToken.None);

        // 12 tasks, ONE checklist read.
        Assert.Equal(1, runs.CallCount);
    }


    // ── Task template (pack §12 E5) ───────────────────────────────────────────

    [Fact]
    public async Task Creating_a_task_from_a_template_ALSO_instantiates_its_checklist()
    {
        var checklistTemplate = new ChecklistTemplate
        {
            Id = Guid.Parse("77777777-7777-7777-7777-777777777777"),
            TenantId = TaskTestData.Tenant,
            Code = "CLOSE", Name = "Month-end close",
            Items =
            [
                new ChecklistTemplateItem
                {
                    Code = "reconcile", LabelResourceKey = "WorkAggregation_Check_Reconcile",
                    Requirement = ChecklistItemRequirement.Blocking, SortOrder = 0
                }
            ]
        };
        var runs = new FakeChecklistRunRepository();
        var tasks = new FakeTaskItemRepository();

        var result = await CreateWithChecklist(tasks, runs, new FakeChecklistTemplateRepository(checklistTemplate),
            checklistTemplate.Id);

        Assert.True(result.IsSuccessful);
        var run = Assert.Single(runs.Runs);
        Assert.Equal(Assert.Single(tasks.Items).Id, run.TaskItemId);
        var item = Assert.Single(run.Items);
        // The template's label FORM and its blocking requirement both carry over.
        Assert.Equal("WorkAggregation_Check_Reconcile", item.LabelResourceKey);
        Assert.Equal(ChecklistItemRequirement.Blocking, item.Requirement);
        Assert.False(item.Completed);
    }

    [Fact]
    public async Task A_missing_checklist_template_leaves_the_task_created_without_one()
    {
        // Losing the task because a template vanished would be worse than a task with no checklist.
        var runs = new FakeChecklistRunRepository();
        var tasks = new FakeTaskItemRepository();

        var result = await CreateWithChecklist(tasks, runs, new FakeChecklistTemplateRepository(), Guid.NewGuid());

        Assert.True(result.IsSuccessful);
        Assert.Single(tasks.Items);
        Assert.Empty(runs.Runs);
    }

    private static Task<Application.Common.Response<Guid>> CreateWithChecklist(
        FakeTaskItemRepository tasks,
        FakeChecklistRunRepository runs,
        FakeChecklistTemplateRepository templates,
        Guid checklistTemplateId)
    {
        var unitId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var handler = new CreateTaskItemHandler(
            tasks, new FakeTaskAssignmentRepository(), new FakeTaskWatcherRepository(),
            new FakePositionRepository(),
            new FakeOrganizationUnitRepository(new Domain.Entities.Organization.OrganizationUnit
            {
                Id = unitId, TenantId = TaskTestData.Tenant, Code = "ROOT", Name = "Root",
                LegalEntityId = Guid.NewGuid()
            }),
            new FakePositionAssignmentRepository(),
            new TaskFieldDefinitionService(new FakeTaskFieldDefinitionRepository(), TaskRecordSourceDoubles.None, TaskActors.PermitAll()),
            new TaskLifecycleService(), new FakeTaskApprovalService(), templates, runs, new TaskChecklistService(),
            new FakeTaskNotificationService(),
            new FakeCurrentUserContext(TaskTestData.Me), new FakeTenantContext(TaskTestData.Tenant),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CreateTaskItemHandler>.Instance);

        var request = new CreateTaskItemRequest(
            Title: "From template", Description: null, Priority: TaskPriority.Medium,
            AssignmentTarget: TaskAssignmentTarget.SelfAssigned, AssigneeUserId: null, PoolPositionId: null,
            OrganizationUnitId: unitId, DueAt: DateTimeOffset.UtcNow.AddDays(3), StartAt: null, PlannedDate: null,
            EstimateHours: null, Tags: null, ReviewRequired: false, ApprovalRequired: false,
            ApprovalManagerUserId: null, EmailNotificationsEnabled: false, DelegationAllowed: false,
            FieldValues: null, Watchers: null, ParentTaskItemId: null, ChecklistTemplateId: checklistTemplateId);

        return handler.Handle(new CreateTaskItemCommand(request, "corr"), CancellationToken.None);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<WorkItemProjectionDto> ProjectOne(
        TaskItem task, FakeChecklistRunRepository runs, FakeTaskApprovalService? approvals = null)
        => Assert.Single(
            await Provider(new FakeTaskItemRepository(task), runs, approvals)
                .GetWorkItemsAsync(FullyPermittedActor(), CancellationToken.None));

    private static TaskWorkItemProvider Provider(
        FakeTaskItemRepository tasks,
        FakeChecklistRunRepository runs,
        FakeTaskApprovalService? approvals = null)
        => new(tasks, new FakePositionAssignmentRepository(), new TaskLifecycleService(),
            new TaskAssignmentResolver(), new FakeUserDisplayNameResolver(), runs,
            approvals ?? new FakeTaskApprovalService(), new FakeTaskDependencyRepository(), new FakeTaskCommentRepository(), new FakeTaskTransitionRepository(), new FakeTaskPersonalOverlayRepository(), new FakeTaskWatcherRepository(), TaskActors.PermitAll(), new FakePositionRepository(), new FakeOrganizationUnitRepository(), SlaForTests.Real(), new FakeTaskFieldDefinitionRepository());

    private static Task<Application.Common.Response<Application.Common.NoContent>> Transition(
        FakeTaskItemRepository tasks,
        FakeChecklistRunRepository runs,
        Guid id,
        TaskLifecycle target,
        int expectedVersion)
        => new TransitionTaskItemHandler(
                tasks, new TaskLifecycleService(), new FakeCurrentUserContext(TaskTestData.Me),
                runs, new TaskChecklistService(), new FakeWorkflowTransitionGate(),
                new FakeTaskDependencyRepository(), new FakeTaskNotificationService(), NullLogger<TransitionTaskItemHandler>.Instance)
            .Handle(
                new TransitionTaskItemCommand(id, target, new TaskTransitionRequest(expectedVersion, null, null), "corr"),
                CancellationToken.None);

    private static Task<Application.Common.Response<Guid>> CreateSubtask(FakeTaskItemRepository tasks, Guid parentId)
    {
        var unitId = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var handler = new CreateTaskItemHandler(
            tasks,
            new FakeTaskAssignmentRepository(),
            new FakeTaskWatcherRepository(),
            new FakePositionRepository(),
            new FakeOrganizationUnitRepository(new Domain.Entities.Organization.OrganizationUnit
            {
                Id = unitId,
                TenantId = TaskTestData.Tenant,
                Code = "ROOT",
                Name = "Root",
                LegalEntityId = Guid.NewGuid()
            }),
            new FakePositionAssignmentRepository(),
            new TaskFieldDefinitionService(new FakeTaskFieldDefinitionRepository(), TaskRecordSourceDoubles.None, TaskActors.PermitAll()),
            new TaskLifecycleService(),
            new FakeTaskApprovalService(),
            new FakeChecklistTemplateRepository(),
            new FakeChecklistRunRepository(),
            new TaskChecklistService(),
            new FakeTaskNotificationService(),
            new FakeCurrentUserContext(TaskTestData.Me),
            new FakeTenantContext(TaskTestData.Tenant),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<CreateTaskItemHandler>.Instance);

        var request = new CreateTaskItemRequest(
            Title: "Child work", Description: null, Priority: TaskPriority.Medium,
            AssignmentTarget: TaskAssignmentTarget.SelfAssigned, AssigneeUserId: null, PoolPositionId: null,
            OrganizationUnitId: unitId, DueAt: DateTimeOffset.UtcNow.AddDays(3), StartAt: null, PlannedDate: null,
            EstimateHours: null, Tags: null, ReviewRequired: false, ApprovalRequired: false,
            ApprovalManagerUserId: null, EmailNotificationsEnabled: false, DelegationAllowed: false,
            FieldValues: null, Watchers: null, ParentTaskItemId: parentId);

        return handler.Handle(new CreateTaskItemCommand(request, "corr"), CancellationToken.None);
    }

    private static WorkItemActor FullyPermittedActor()
        => new(TaskTestData.Me, IsPlatformActor: false, new HashSet<string>(
            new[] { TaskPermissions.Update, TaskPermissions.Claim, TaskPermissions.Complete, TaskPermissions.Cancel },
            StringComparer.OrdinalIgnoreCase));

    private static ChecklistRun RunFor(Guid taskId, params (bool Blocking, bool Completed)[] items)
    {
        var run = new ChecklistRun { TenantId = TaskTestData.Tenant, TaskItemId = taskId, Version = 1 };
        for (var i = 0; i < items.Length; i++)
        {
            run.Items.Add(new ChecklistRunItem
            {
                Code = $"i{i}",
                LabelResourceKey = "WorkAggregation_Check_Sample",
                Requirement = items[i].Blocking ? ChecklistItemRequirement.Blocking : ChecklistItemRequirement.Optional,
                SortOrder = i,
                Completed = items[i].Completed
            });
        }

        return run;
    }

    private static TaskItem TopLevel() => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "Parent work",
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        // A self-assigned task is created by its own assignee; CreateTaskItemHandler always stamps this. Without
        // it the fixture describes a task with no requester, which only stopped mattering while anyone could
        // cancel anything.
        CreatedByUserId = TaskTestData.Me,
        OrganizationUnitId = Guid.NewGuid(),
        Lifecycle = TaskLifecycle.Open,
        Version = 1
    };

    private static TaskItem InProgressTask()
    {
        var task = TopLevel();
        task.Lifecycle = TaskLifecycle.InProgress;
        return task;
    }

    private static TaskItem SubtaskOf(Guid parentId, TaskLifecycle lifecycle)
    {
        var task = TopLevel();
        task.Title = "Child work";
        task.ParentTaskItemId = parentId;
        task.Lifecycle = lifecycle;
        task.CompletedAt = lifecycle == TaskLifecycle.Done ? DateTimeOffset.UtcNow : null;
        return task;
    }
}
