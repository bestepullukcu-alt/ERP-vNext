using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// The three verbs a checklist item could not do to itself: be reworded, be levelled, or leave.
///
/// <para>Three of <see cref="ChecklistRunItem"/>'s fields were written once, at creation, and then frozen —
/// stored faithfully, and unreachable by any endpoint for the rest of the task's life. The create form let an
/// author word an item, level it and flag it for evidence; the task itself let the person actually doing the
/// work change none of those things. This is the "stored but inert" class of defect this module has had to fix
/// repeatedly, and it is worth stating plainly that the tests below are not about new features: they are about
/// data the system was already keeping and pretending it could not touch.</para>
/// </summary>
public sealed class ChecklistEditVerbsTests
{
    // ── PUT: the editable face of an item ─────────────────────────────────────

    [Fact]
    public async Task Editing_an_ad_hoc_item_rewrites_its_text_its_level_and_its_evidence_flag()
    {
        var task = InProgressTask();
        var run = AdHocRun(task.Id, "Ring the auditor");
        var runs = new FakeChecklistRunRepository(run);

        var result = await Update(task, runs, "a0",
            new UpdateChecklistItemRequest("Ring the auditor BACK", ChecklistItemRequirement.Blocking, true, run.Version));

        Assert.True(result.IsSuccessful);
        // Read back through the repository, not through the object the handler was handed: the fake detaches
        // reads, so this fails if the handler mutated in memory and never committed.
        var stored = Assert.Single((await runs.GetByTaskIdAsync(task.Id))!.Items);
        Assert.Equal("Ring the auditor BACK", stored.LabelText);
        Assert.Equal(ChecklistItemRequirement.Blocking, stored.Requirement);
        Assert.True(stored.EvidenceRequired);
    }

    [Fact]
    public async Task A_template_items_TEXT_is_refused_with_its_own_reason_code()
    {
        var task = InProgressTask();
        var run = TemplateRun(task.Id);
        var runs = new FakeChecklistRunRepository(run);

        var result = await Update(task, runs, "t0",
            new UpdateChecklistItemRequest("My own wording", ChecklistItemRequirement.Optional, false, run.Version));

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        // Not VALIDATION_FAILED: nothing is wrong with the payload, so a client told that would keep correcting
        // and resending text that was never going to be accepted.
        Assert.Equal(TaskReasonCodes.ChecklistItemTemplateOwned, result.ReasonCode);
        Assert.Equal("WorkAggregation_Check_Sample", (await runs.GetByTaskIdAsync(task.Id))!.Items[0].LabelResourceKey);
    }

    [Fact]
    public async Task A_template_item_may_still_be_levelled_and_flagged_for_evidence()
    {
        /*
         * The line falls between the two on purpose. A template's WORDS belong to every task made from it —
         * letting one task reword its copy leaves the same step saying different things on different tasks, in a
         * list whose entire value is that it says the same thing. But how strictly THIS task is run is a
         * judgement about this task, and the person holding it is the one placed to make it.
         */
        var task = InProgressTask();
        var run = TemplateRun(task.Id);
        var runs = new FakeChecklistRunRepository(run);

        var result = await Update(task, runs, "t0",
            new UpdateChecklistItemRequest(null, ChecklistItemRequirement.Blocking, true, run.Version));

        Assert.True(result.IsSuccessful);
        var stored = Assert.Single((await runs.GetByTaskIdAsync(task.Id))!.Items);
        Assert.Equal(ChecklistItemRequirement.Blocking, stored.Requirement);
        Assert.True(stored.EvidenceRequired);
        Assert.Equal("WorkAggregation_Check_Sample", stored.LabelResourceKey);
    }

    [Fact]
    public async Task An_ad_hoc_item_cannot_be_emptied_of_its_text()
    {
        // It would render as a blank row: unidentifiable, and so unfixable by the next reader.
        var task = InProgressTask();
        var run = AdHocRun(task.Id, "Ring the auditor");
        var runs = new FakeChecklistRunRepository(run);

        var result = await Update(task, runs, "a0",
            new UpdateChecklistItemRequest("   ", ChecklistItemRequirement.Optional, false, run.Version));

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("Ring the auditor", (await runs.GetByTaskIdAsync(task.Id))!.Items[0].LabelText);
    }

    // ── DELETE ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Removing_an_item_takes_it_out_and_closes_the_gap_it_left()
    {
        var task = InProgressTask();
        var run = ThreeAdHoc(task.Id);
        var runs = new FakeChecklistRunRepository(run);

        var result = await new RemoveChecklistItemHandler(
                new FakeTaskItemRepository(task), runs, new TaskChecklistService(),
                new FakeCurrentUserContext(TaskTestData.Me))
            .Handle(new RemoveChecklistItemCommand(
                task.Id, "a1", new RemoveChecklistItemRequest(run.Version), "corr"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        var stored = (await runs.GetByTaskIdAsync(task.Id))!;
        Assert.Equal(new[] { "a0", "a2" }, stored.Items.OrderBy(i => i.SortOrder).Select(i => i.Code));
        // No hole at position 1: a later reorder sends the codes it can see, and a gap would make the two
        // disagree about what position 1 even is.
        Assert.Equal(new[] { 0, 1 }, stored.Items.OrderBy(i => i.SortOrder).Select(i => i.SortOrder));
    }

    [Fact]
    public async Task A_template_item_MAY_be_removed_even_though_it_may_not_be_reworded()
    {
        // Whether a step applies to this task is a judgement about this task. Rewording is different: it leaves
        // the item in the list still claiming to be the template's step while saying something else.
        var task = InProgressTask();
        var run = TemplateRun(task.Id);
        var runs = new FakeChecklistRunRepository(run);

        var result = await new RemoveChecklistItemHandler(
                new FakeTaskItemRepository(task), runs, new TaskChecklistService(),
                new FakeCurrentUserContext(TaskTestData.Me))
            .Handle(new RemoveChecklistItemCommand(
                task.Id, "t0", new RemoveChecklistItemRequest(run.Version), "corr"), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Empty((await runs.GetByTaskIdAsync(task.Id))!.Items);
    }

    // ── PUT order: the whole list, one write ──────────────────────────────────

    [Fact]
    public async Task Reordering_writes_the_whole_order_in_one_call()
    {
        var task = InProgressTask();
        var run = ThreeAdHoc(task.Id);
        var runs = new FakeChecklistRunRepository(run);

        var result = await Reorder(task, runs, new[] { "a2", "a0", "a1" }, run.Version);

        Assert.True(result.IsSuccessful);
        var stored = (await runs.GetByTaskIdAsync(task.Id))!;
        Assert.Equal(new[] { "a2", "a0", "a1" }, stored.Items.OrderBy(i => i.SortOrder).Select(i => i.Code));
    }

    [Fact]
    public async Task A_partial_order_is_refused_whole_rather_than_applied_to_the_part_it_covers()
    {
        // Half a reorder is not a smaller reorder, it is a different one: the unnamed item would land at an end
        // nobody dragged it to.
        var task = InProgressTask();
        var run = ThreeAdHoc(task.Id);
        var runs = new FakeChecklistRunRepository(run);

        var result = await Reorder(task, runs, new[] { "a2", "a0" }, run.Version);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(new[] { "a0", "a1", "a2" },
            (await runs.GetByTaskIdAsync(task.Id))!.Items.OrderBy(i => i.SortOrder).Select(i => i.Code));
    }

    [Fact]
    public async Task A_repeated_code_is_refused()
    {
        var task = InProgressTask();
        var run = ThreeAdHoc(task.Id);
        var runs = new FakeChecklistRunRepository(run);

        var result = await Reorder(task, runs, new[] { "a0", "a0", "a1" }, run.Version);

        Assert.False(result.IsSuccessful);
        Assert.Equal(400, result.StatusCode);
    }

    // ── The guards all three share ────────────────────────────────────────────

    [Theory]
    [InlineData(TaskLifecycle.Done)]
    [InlineData(TaskLifecycle.Cancelled)]
    public async Task A_closed_task_refuses_all_three_verbs(TaskLifecycle lifecycle)
    {
        /*
         * The front end disables these controls on a closed task. That is a courtesy to the reader, not a guard:
         * it is JavaScript on the caller's machine and the endpoint is reachable without it.
         */
        var task = InProgressTask();
        task.Lifecycle = lifecycle;
        var run = ThreeAdHoc(task.Id);
        var runs = new FakeChecklistRunRepository(run);
        var tasks = new FakeTaskItemRepository(task);

        var edit = await Update(task, runs, "a0",
            new UpdateChecklistItemRequest("changed", ChecklistItemRequirement.Blocking, true, run.Version));
        var remove = await new RemoveChecklistItemHandler(
                tasks, runs, new TaskChecklistService(), new FakeCurrentUserContext(TaskTestData.Me))
            .Handle(new RemoveChecklistItemCommand(
                task.Id, "a0", new RemoveChecklistItemRequest(run.Version), "corr"), CancellationToken.None);
        var reorder = await Reorder(task, runs, new[] { "a2", "a1", "a0" }, run.Version);

        foreach (var result in new[] { edit, remove, reorder })
        {
            Assert.False(result.IsSuccessful);
            Assert.Equal(409, result.StatusCode);
            Assert.Equal(TaskReasonCodes.InvalidState, result.ReasonCode);
        }

        var stored = (await runs.GetByTaskIdAsync(task.Id))!;
        Assert.Equal(3, stored.Items.Count);
        Assert.Equal(new[] { "a0", "a1", "a2" }, stored.Items.OrderBy(i => i.SortOrder).Select(i => i.Code));
    }

    [Fact]
    public async Task A_stale_expectedVersion_is_a_409_on_all_three_and_writes_nothing()
    {
        /*
         * This is the guard that failed silently earlier in this module's life: a write issued without an
         * expected version reported success and changed nothing at all. A conditional write that is skipped is
         * indistinguishable from one that succeeded, right up until the reload.
         */
        var task = InProgressTask();
        var run = ThreeAdHoc(task.Id);
        var runs = new FakeChecklistRunRepository(run);
        var stale = run.Version - 1;

        var edit = await Update(task, runs, "a0",
            new UpdateChecklistItemRequest("changed", ChecklistItemRequirement.Blocking, true, stale));
        var remove = await new RemoveChecklistItemHandler(
                new FakeTaskItemRepository(task), runs, new TaskChecklistService(),
                new FakeCurrentUserContext(TaskTestData.Me))
            .Handle(new RemoveChecklistItemCommand(
                task.Id, "a0", new RemoveChecklistItemRequest(stale), "corr"), CancellationToken.None);
        var reorder = await Reorder(task, runs, new[] { "a2", "a1", "a0" }, stale);

        foreach (var result in new[] { edit, remove, reorder })
        {
            Assert.False(result.IsSuccessful);
            Assert.Equal(409, result.StatusCode);
            Assert.Equal(TaskReasonCodes.ConcurrencyConflict, result.ReasonCode);
        }

        var stored = (await runs.GetByTaskIdAsync(task.Id))!;
        Assert.Equal(3, stored.Items.Count);
        Assert.Equal("first", stored.Items.Single(i => i.Code == "a0").LabelText);
        Assert.Equal(new[] { "a0", "a1", "a2" }, stored.Items.OrderBy(i => i.SortOrder).Select(i => i.Code));
    }

    [Fact]
    public async Task An_unknown_item_code_is_a_404_not_a_silent_success()
    {
        var task = InProgressTask();
        var run = ThreeAdHoc(task.Id);
        var runs = new FakeChecklistRunRepository(run);

        var result = await Update(task, runs, "nope",
            new UpdateChecklistItemRequest("x", ChecklistItemRequirement.Optional, false, run.Version));

        Assert.False(result.IsSuccessful);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal(TaskReasonCodes.ChecklistItemNotFound, result.ReasonCode);
    }

    [Fact]
    public async Task Raising_an_item_to_blocking_re_resolves_the_runs_status()
    {
        // A level change decides whether the run blocks completion, so the status must be recomputed after one —
        // exactly as ticking an item does. Left out, the task would still offer "complete" behind a new gate.
        var task = InProgressTask();
        var run = ThreeAdHoc(task.Id);
        var runs = new FakeChecklistRunRepository(run);
        var before = (await runs.GetByTaskIdAsync(task.Id))!.Status;

        await Update(task, runs, "a0",
            new UpdateChecklistItemRequest("first", ChecklistItemRequirement.Blocking, false, run.Version));

        var after = (await runs.GetByTaskIdAsync(task.Id))!;
        Assert.Equal(new TaskChecklistService().ResolveStatus(after), after.Status);
        Assert.Equal(ChecklistItemRequirement.Blocking, after.Items.Single(i => i.Code == "a0").Requirement);
        _ = before;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Task<Application.Common.Response<Application.Common.NoContent>> Update(
        TaskItem task, FakeChecklistRunRepository runs, string code, UpdateChecklistItemRequest request)
        => new UpdateChecklistItemHandler(
                new FakeTaskItemRepository(task), runs, new TaskChecklistService(),
                new FakeCurrentUserContext(TaskTestData.Me))
            .Handle(new UpdateChecklistItemCommand(task.Id, code, request, "corr"), CancellationToken.None);

    private static Task<Application.Common.Response<Application.Common.NoContent>> Reorder(
        TaskItem task, FakeChecklistRunRepository runs, string[] codes, int expectedVersion)
        => new ReorderChecklistHandler(
                new FakeTaskItemRepository(task), runs, new FakeCurrentUserContext(TaskTestData.Me))
            .Handle(new ReorderChecklistCommand(
                task.Id, new ReorderChecklistRequest(codes, expectedVersion), "corr"), CancellationToken.None);

    private static ChecklistRun AdHocRun(Guid taskId, string text)
    {
        var run = new ChecklistRun { TenantId = TaskTestData.Tenant, TaskItemId = taskId, Version = 1 };
        run.Items.Add(new ChecklistRunItem { Code = "a0", LabelText = text, SortOrder = 0 });
        return run;
    }

    private static ChecklistRun TemplateRun(Guid taskId)
    {
        var run = new ChecklistRun { TenantId = TaskTestData.Tenant, TaskItemId = taskId, Version = 1 };
        run.Items.Add(new ChecklistRunItem
        {
            Code = "t0", LabelResourceKey = "WorkAggregation_Check_Sample", SortOrder = 0
        });
        return run;
    }

    private static ChecklistRun ThreeAdHoc(Guid taskId)
    {
        var run = new ChecklistRun { TenantId = TaskTestData.Tenant, TaskItemId = taskId, Version = 1 };
        var texts = new[] { "first", "second", "third" };
        for (var i = 0; i < texts.Length; i++)
        {
            run.Items.Add(new ChecklistRunItem { Code = $"a{i}", LabelText = texts[i], SortOrder = i });
        }

        return run;
    }

    private static TaskItem InProgressTask()
    {
        var task = new TaskItem
        {
            TenantId = TaskTestData.Tenant,
            Title = "Checklist work",
            AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
            AssigneeUserId = TaskTestData.Me,
            CreatedByUserId = TaskTestData.Me,
            OrganizationUnitId = Guid.NewGuid(),
            Lifecycle = TaskLifecycle.InProgress,
            Version = 1
        };
        return task;
    }
}
