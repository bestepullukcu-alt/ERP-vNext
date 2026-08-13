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
    public async Task A_template_item_is_refused_with_its_OWN_reason_code_not_the_generic_one()
    {
        var task = InProgressTask();
        var run = TemplateRun(task.Id);
        var runs = new FakeChecklistRunRepository(run);

        var result = await Update(task, runs, "t0",
            new UpdateChecklistItemRequest("My own wording", ChecklistItemRequirement.Optional, false, run.Version));

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        /*
         * Not VALIDATION_FAILED: nothing is wrong with the payload, so a client told that would keep correcting
         * and resending something that was never going to be accepted.
         *
         * And not NOT_AUTHOR either, though that would also be true — a template row has no author. The specific
         * code is checked first on purpose: "this comes from the process" sends the reader somewhere useful,
         * where "somebody else added this" sends them looking for a colleague who does not exist.
         */
        Assert.Equal(TaskReasonCodes.ChecklistItemTemplateOwned, result.ReasonCode);
        Assert.Equal("WorkAggregation_Check_Sample", (await runs.GetByTaskIdAsync(task.Id))!.Items[0].LabelResourceKey);
    }

    [Fact]
    public async Task A_template_item_may_NOT_be_levelled_or_flagged_either_reversing_the_earlier_decision()
    {
        /*
         * RE-PINNED, not deleted — this test used to assert the opposite, and the reasoning it carried was:
         * "a template's WORDS belong to every task made from it, but how strictly THIS task is run is a
         * judgement about this task, and the person holding it is placed to make it."
         *
         * That reasoning fails in the only case that matters. The item most worth re-levelling is the BLOCKING
         * one standing between the holder and "done", and Blocking → Optional releases the gate exactly as
         * completely as deleting the row. Protecting the words and not the level protected nothing.
         *
         * The full rule is in A_template_item_is_now_protected_in_full_reversing_last_rounds_decision; this one
         * stays because the sentence it used to defend is worth seeing struck through.
         */
        var task = InProgressTask();
        var run = TemplateRun(task.Id);
        var runs = new FakeChecklistRunRepository(run);

        var result = await Update(task, runs, "t0",
            new UpdateChecklistItemRequest(null, ChecklistItemRequirement.Blocking, true, run.Version));

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(TaskReasonCodes.ChecklistItemTemplateOwned, result.ReasonCode);
        var stored = Assert.Single((await runs.GetByTaskIdAsync(task.Id))!.Items);
        Assert.Equal(ChecklistItemRequirement.Optional, stored.Requirement);
        Assert.False(stored.EvidenceRequired);
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
    public async Task A_template_item_may_NOT_be_removed_reversing_the_earlier_decision()
    {
        /*
         * RE-PINNED. The struck reasoning was "whether a step applies to THIS task is a judgement about this
         * task" — which, followed honestly, hands the holder the key to every gate on their own task. A step the
         * process defined is not the handler's to withdraw; that is also where the larger systems draw it.
         */
        var task = InProgressTask();
        var run = TemplateRun(task.Id);
        var runs = new FakeChecklistRunRepository(run);

        var result = await Remove(task, runs, "t0", run.Version);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        // The specific code, not the generic one — a template row has no author to name.
        Assert.Equal(TaskReasonCodes.ChecklistItemTemplateOwned, result.ReasonCode);
        Assert.Single((await runs.GetByTaskIdAsync(task.Id))!.Items);
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

    // ── Ownership: who may lift a gate ────────────────────────────────────────

    /*
     * THE HOLE THIS CLOSES.
     *
     * The delete endpoint checked the task, the run, the item, the lifecycle and the version — and never asked
     * whose step it was. So a BLOCKING item could be removed by the exact person it was blocking, and the level
     * that made it blocking could be dropped to Optional by the same person, which is the identical escape
     * through a quieter door. A gate anyone can lift is decoration.
     *
     * The rule is OWNERSHIP, not severity. A threshold ("Blocking is protected, Expected is not") only relocates
     * the argument to where the line sits and leaves the escape open on one side of it.
     */

    [Fact]
    public async Task Somebody_elses_BLOCKING_item_cannot_be_removed_by_the_person_it_blocks()
    {
        var task = InProgressTask();
        var run = ItemAddedBy(task.Id, TaskTestData.Rival, ChecklistItemRequirement.Blocking);
        var runs = new FakeChecklistRunRepository(run);

        var result = await Remove(task, runs, "owned", run.Version);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(TaskReasonCodes.ChecklistItemNotAuthor, result.ReasonCode);
        Assert.Single((await runs.GetByTaskIdAsync(task.Id))!.Items);
    }

    [Fact]
    public async Task Somebody_elses_item_cannot_be_DOWNGRADED_out_of_the_way_either()
    {
        // Blocking → Optional releases the gate as completely as deleting the row. A rule that protected only
        // the delete would have moved the escape, not closed it.
        var task = InProgressTask();
        var run = ItemAddedBy(task.Id, TaskTestData.Rival, ChecklistItemRequirement.Blocking);
        var runs = new FakeChecklistRunRepository(run);

        var result = await Update(task, runs, "owned",
            new UpdateChecklistItemRequest(null, ChecklistItemRequirement.Optional, false, run.Version));

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(TaskReasonCodes.ChecklistItemNotAuthor, result.ReasonCode);
        var stored = Assert.Single((await runs.GetByTaskIdAsync(task.Id))!.Items);
        Assert.Equal(ChecklistItemRequirement.Blocking, stored.Requirement);
    }

    [Fact]
    public async Task Your_OWN_item_stays_yours_to_change_and_to_remove()
    {
        var task = InProgressTask();
        var runs = new FakeChecklistRunRepository(ItemAddedBy(task.Id, TaskTestData.Me));

        var edited = await Update(task, runs, "owned",
            new UpdateChecklistItemRequest("reworded", ChecklistItemRequirement.Blocking, true, 1));
        Assert.True(edited.IsSuccessful);

        var removed = await Remove(task, runs, "owned", 2);
        Assert.True(removed.IsSuccessful);
        Assert.Empty((await runs.GetByTaskIdAsync(task.Id))!.Items);
    }

    [Fact]
    public async Task TICKING_somebody_elses_item_stays_open_to_everyone_because_that_is_the_work()
    {
        // The one verb deliberately left outside the rule. A checklist you may not tick is not a checklist.
        var task = InProgressTask();
        var run = ItemAddedBy(task.Id, TaskTestData.Rival, ChecklistItemRequirement.Blocking);
        var runs = new FakeChecklistRunRepository(run);

        var result = await new SetChecklistItemStateHandler(
                new FakeTaskItemRepository(task), runs, new TaskChecklistService(),
                new FakeCurrentUserContext(TaskTestData.Me))
            .Handle(new SetChecklistItemStateCommand(
                task.Id, new SetChecklistItemStateRequest("owned", true, run.Version), "corr"),
                CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.True(Assert.Single((await runs.GetByTaskIdAsync(task.Id))!.Items).Completed);
    }

    [Fact]
    public async Task An_item_with_NO_recorded_author_is_treated_as_somebody_elses()
    {
        /*
         * Two kinds of row arrive with a null author: written before the field existed, and instantiated from a
         * TEMPLATE (which has no author, because the template is the author). Both answer the same way, and the
         * asymmetry is the argument — refusing an edit that should have been allowed produces a complaint;
         * allowing a deletion that should have been refused removes a gate silently, and nobody finds out until
         * the thing it existed to prevent has happened.
         */
        var task = InProgressTask();
        var run = ItemAddedBy(task.Id, addedBy: null, ChecklistItemRequirement.Blocking);
        var runs = new FakeChecklistRunRepository(run);

        var removed = await Remove(task, runs, "owned", run.Version);
        var levelled = await Update(task, runs, "owned",
            new UpdateChecklistItemRequest(null, ChecklistItemRequirement.Optional, false, run.Version));

        foreach (var result in new[] { removed, levelled })
        {
            Assert.False(result.IsSuccessful);
            Assert.Equal(TaskReasonCodes.ChecklistItemNotAuthor, result.ReasonCode);
        }

        Assert.Single((await runs.GetByTaskIdAsync(task.Id))!.Items);
    }

    [Fact]
    public async Task A_TEMPLATE_item_is_now_protected_in_full_reversing_last_rounds_decision()
    {
        /*
         * RE-PINNED. A round ago a template item's level and evidence flag were deliberately left open, on the
         * reasoning that "how strictly THIS task is run is the holder's judgement", and its removal was allowed
         * outright. That reasoning fails in the one case that matters: the item most worth removing is the
         * blocking one standing between the holder and "done".
         */
        var task = InProgressTask();
        var run = new ChecklistRun { TenantId = TaskTestData.Tenant, TaskItemId = task.Id, Version = 1 };
        run.Items.Add(new ChecklistRunItem
        {
            Code = "t0", LabelResourceKey = "WorkAggregation_Check_Sample",
            Requirement = ChecklistItemRequirement.Blocking, SortOrder = 0
        });
        var runs = new FakeChecklistRunRepository(run);

        var levelled = await Update(task, runs, "t0",
            new UpdateChecklistItemRequest(null, ChecklistItemRequirement.Optional, false, run.Version));
        var flagged = await Update(task, runs, "t0",
            new UpdateChecklistItemRequest(null, ChecklistItemRequirement.Blocking, true, run.Version));
        var removed = await Remove(task, runs, "t0", run.Version);

        foreach (var result in new[] { levelled, flagged, removed })
        {
            Assert.False(result.IsSuccessful);
            Assert.Equal(TaskReasonCodes.ChecklistItemTemplateOwned, result.ReasonCode);
        }

        var stored = Assert.Single((await runs.GetByTaskIdAsync(task.Id))!.Items);
        Assert.Equal(ChecklistItemRequirement.Blocking, stored.Requirement);
        Assert.False(stored.EvidenceRequired);
    }

    [Fact]
    public async Task Adding_an_item_records_WHO_added_it_which_is_what_makes_the_rule_writable()
    {
        var task = InProgressTask();
        var runs = new FakeChecklistRunRepository();

        var result = await new AddChecklistItemHandler(
                new FakeTaskItemRepository(task), runs, new TaskChecklistService(),
                new FakeCurrentUserContext(TaskTestData.Me), new FakeTenantContext(TaskTestData.Tenant))
            .Handle(new AddChecklistItemCommand(
                task.Id, new AddChecklistItemRequest("mine", ChecklistItemRequirement.Blocking, 0), "corr"),
                CancellationToken.None);

        Assert.True(result.IsSuccessful);
        var stored = Assert.Single(Assert.Single(runs.Runs).Items);
        // Nothing had to be plumbed for this: the identity was already injected to fill CreatedBy. The rule was
        // unwritable purely because this one line was never written.
        Assert.Equal(TaskTestData.Me, stored.AddedByUserId);
        Assert.NotEqual(default, stored.AddedAt);
    }

    [Theory]
    [InlineData(TaskLifecycle.Done)]
    [InlineData(TaskLifecycle.Cancelled)]
    public async Task BL_093_a_closed_task_can_no_longer_GROW_new_checklist_items(TaskLifecycle lifecycle)
    {
        // Four of this card's five verbs refused a closed task and this one accepted, so a finished task could
        // still grow new steps.
        var task = InProgressTask();
        task.Lifecycle = lifecycle;
        var runs = new FakeChecklistRunRepository();

        var result = await new AddChecklistItemHandler(
                new FakeTaskItemRepository(task), runs, new TaskChecklistService(),
                new FakeCurrentUserContext(TaskTestData.Me), new FakeTenantContext(TaskTestData.Tenant))
            .Handle(new AddChecklistItemCommand(
                task.Id, new AddChecklistItemRequest("late", ChecklistItemRequirement.Optional, 0), "corr"),
                CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(TaskReasonCodes.InvalidState, result.ReasonCode);
        Assert.Empty(runs.Runs);
    }

    [Fact]
    public async Task Every_writable_member_of_a_checklist_item_survives_a_round_trip_through_the_double()
    {
        /*
         * The double in this file has dropped a newly added field TWICE, failing tests against correct
         * production code. It clones by SERIALISATION now rather than by a hand-written member list, and this
         * asserts that property-by-property so the next field added to ChecklistRunItem — like AddedByUserId in
         * this round — travels without anyone remembering the double exists.
         */
        var task = InProgressTask();
        var run = new ChecklistRun { TenantId = TaskTestData.Tenant, TaskItemId = task.Id, Version = 1 };
        run.Items.Add(new ChecklistRunItem
        {
            Code = "full", LabelText = "text", Requirement = ChecklistItemRequirement.Blocking,
            SortOrder = 3, EvidenceRequired = true, Completed = true,
            CompletedByUserId = TaskTestData.Me, CompletedAt = DateTimeOffset.UtcNow,
            AddedByUserId = TaskTestData.Rival, AddedAt = DateTimeOffset.UtcNow.AddDays(-1)
        });
        var runs = new FakeChecklistRunRepository(run);

        var stored = Assert.Single((await runs.GetByTaskIdAsync(task.Id))!.Items);
        var original = run.Items[0];
        foreach (var property in typeof(ChecklistRunItem).GetProperties().Where(p => p.CanWrite))
        {
            Assert.Equal(property.GetValue(original), property.GetValue(stored));
        }
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

    private static Task<Application.Common.Response<Application.Common.NoContent>> Remove(
        TaskItem task, FakeChecklistRunRepository runs, string code, int expectedVersion)
        => new RemoveChecklistItemHandler(
                new FakeTaskItemRepository(task), runs, new TaskChecklistService(),
                new FakeCurrentUserContext(TaskTestData.Me))
            .Handle(new RemoveChecklistItemCommand(
                task.Id, code, new RemoveChecklistItemRequest(expectedVersion), "corr"), CancellationToken.None);

    /// <summary>One item, attributed to whoever is named — or to nobody, which is the legacy/template case.</summary>
    private static ChecklistRun ItemAddedBy(
        Guid taskId, Guid? addedBy, ChecklistItemRequirement requirement = ChecklistItemRequirement.Optional)
    {
        var run = new ChecklistRun { TenantId = TaskTestData.Tenant, TaskItemId = taskId, Version = 1 };
        run.Items.Add(new ChecklistRunItem
        {
            Code = "owned", LabelText = "theirs", Requirement = requirement, SortOrder = 0,
            AddedByUserId = addedBy
        });
        return run;
    }

    private static ChecklistRun AdHocRun(Guid taskId, string text)
    {
        var run = new ChecklistRun { TenantId = TaskTestData.Tenant, TaskItemId = taskId, Version = 1 };
        // Attributed to Me: these cases are about what an author may do to their OWN row. An unattributed row
        // is somebody else's by design, which is a different test (An_item_with_NO_recorded_author_…).
        run.Items.Add(new ChecklistRunItem
        {
            Code = "a0", LabelText = text, SortOrder = 0, AddedByUserId = TaskTestData.Me
        });
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
            run.Items.Add(new ChecklistRunItem
            {
                Code = $"a{i}", LabelText = texts[i], SortOrder = i, AddedByUserId = TaskTestData.Me
            });
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
