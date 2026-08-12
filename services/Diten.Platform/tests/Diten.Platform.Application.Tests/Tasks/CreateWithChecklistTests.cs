using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// Giving a NEW task its checklist, in the same request that creates it.
///
/// <para><b>What was missing.</b> Everything about checklists worked except the way in. The entities, the two
/// commands, the projection, the tick control and the completion gate had all shipped; the create form had no
/// trace of a checklist, and <c>CreateTaskItemRequest</c> could carry only a TEMPLATE id — for which no listing
/// endpoint exists. So a blocking item could stop a task from closing, and there was no place to write one.</para>
///
/// <para>The items travel WITH the task rather than in a follow-up call, which is what removes the question
/// "the task was created and the checklist was not — now what?" rather than answering it.</para>
/// </summary>
public sealed class CreateWithChecklistTests
{
    private static readonly Guid PositionId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid UnitId = Guid.Parse("66666666-6666-6666-6666-666666666666");

    [Fact]
    public async Task Items_typed_on_the_form_are_stored_with_the_task()
    {
        var runs = new FakeChecklistRunRepository();

        var response = await Create(runs, Request(with:
        [
            new CreateChecklistItemRequest("Fatura eki yüklendi", ChecklistItemRequirement.Blocking),
            new CreateChecklistItemRequest("Muhasebe onayı alındı", ChecklistItemRequirement.Required),
            new CreateChecklistItemRequest("Arşive kopya bırakıldı")
        ]));

        Assert.Equal(201, response.StatusCode);

        var run = Assert.Single(runs.Runs);
        Assert.Equal(3, run.Items.Count);
        Assert.Equal(
            ["Fatura eki yüklendi", "Muhasebe onayı alındı", "Arşive kopya bırakıldı"],
            run.Items.OrderBy(i => i.SortOrder).Select(i => i.LabelText));

        // The three levels are DIFFERENT things and are stored as such — see the gate tests below for the two
        // that actually behave differently.
        Assert.Equal(
            [ChecklistItemRequirement.Blocking, ChecklistItemRequirement.Required, ChecklistItemRequirement.Optional],
            run.Items.OrderBy(i => i.SortOrder).Select(i => i.Requirement));
    }

    [Fact]
    public async Task The_order_the_author_typed_is_the_order_that_is_stored()
    {
        // SortOrder comes from the ARRAY's position, never from a field the client sends: a payload carrying its
        // own sort key can contradict its own list, and then two readers disagree about what "first" means.
        var runs = new FakeChecklistRunRepository();

        await Create(runs, Request(with:
        [
            new CreateChecklistItemRequest("bir"),
            new CreateChecklistItemRequest("iki"),
            new CreateChecklistItemRequest("üç")
        ]));

        var run = Assert.Single(runs.Runs);
        Assert.Equal([0, 1, 2], run.Items.Select(i => i.SortOrder));
        Assert.Equal(["bir", "iki", "üç"], run.Items.Select(i => i.LabelText));
    }

    [Fact]
    public async Task Evidence_required_survives_the_create()
    {
        // The model has carried this since Phase 1 and no screen ever set it. The paperclip on the form does.
        var runs = new FakeChecklistRunRepository();

        await Create(runs, Request(with:
        [
            new CreateChecklistItemRequest("Fatura eki yüklendi", ChecklistItemRequirement.Blocking, EvidenceRequired: true),
            new CreateChecklistItemRequest("Sözlü teyit alındı")
        ]));

        var run = Assert.Single(runs.Runs);
        Assert.True(run.Items[0].EvidenceRequired);
        Assert.False(run.Items[1].EvidenceRequired);
    }

    [Fact]
    public async Task An_item_the_user_typed_is_TEXT_and_never_a_resource_key()
    {
        // The distinction that puts a raw key on screen when it is lost. An ad-hoc item has no key at all.
        var runs = new FakeChecklistRunRepository();

        await Create(runs, Request(with: [new CreateChecklistItemRequest("Kendi yazdığım madde")]));

        var item = Assert.Single(Assert.Single(runs.Runs).Items);
        Assert.Null(item.LabelResourceKey);
        Assert.Equal("Kendi yazdığım madde", item.LabelText);
        // The code is the SERVER's identifier, not the text.
        Assert.StartsWith("adhoc-", item.Code);
    }

    [Fact]
    public async Task A_blank_row_is_dropped_rather_than_failing_the_whole_create()
    {
        /*
         * A trailing empty row is a slip of the form, not a request to refuse an otherwise valid task — and the
         * author cannot see a difference between the two. Refusing here would make them hunt for the invisible
         * row that killed their create.
         */
        var runs = new FakeChecklistRunRepository();

        var response = await Create(runs, Request(with:
        [
            new CreateChecklistItemRequest("gerçek madde"),
            new CreateChecklistItemRequest("   ")
        ]));

        Assert.Equal(201, response.StatusCode);
        Assert.Single(Assert.Single(runs.Runs).Items);
    }

    [Fact]
    public async Task No_items_means_no_run_at_all
        ()
    {
        // Not an empty run: a document that says "this task has a checklist with nothing in it" is a different
        // claim from "this task has no checklist", and the add path already knows how to start one.
        var runs = new FakeChecklistRunRepository();

        await Create(runs, Request(with: []));
        await Create(runs, Request(with: null));

        Assert.Empty(runs.Runs);
    }

    [Fact]
    public async Task A_template_and_typed_items_make_ONE_list_in_the_order_shown()
    {
        /*
         * Two SOURCES for one list, not two features. The author saw one list on screen, so one run is written —
         * and the template's items come first because that is where they appeared.
         */
        var templateId = Guid.NewGuid();
        var templates = new FakeChecklistTemplateRepository(new ChecklistTemplate
        {
            Id = templateId,
            TenantId = TaskTestData.Tenant,
            Code = "CLOSE",
            Name = "Ay sonu kapanış",
            IsActive = true,
            Items =
            [
                new ChecklistTemplateItem { Code = "t1", LabelResourceKey = "ChkMatchBalances", SortOrder = 0 },
                new ChecklistTemplateItem { Code = "t2", LabelResourceKey = "ChkReconcileFx", SortOrder = 1 }
            ]
        });
        var runs = new FakeChecklistRunRepository();

        await Create(runs, Request(with: [new CreateChecklistItemRequest("kendi maddem")], templateId: templateId),
            templates);

        var run = Assert.Single(runs.Runs);
        Assert.Equal(3, run.Items.Count);
        // Template items keep their RESOURCE KEY (they are our text, in seven languages); the typed one is text.
        Assert.Equal(["ChkMatchBalances", "ChkReconcileFx", null], run.Items.Select(i => i.LabelResourceKey));
        Assert.Equal([null, null, "kendi maddem"], run.Items.Select(i => i.LabelText));
        Assert.Equal([0, 1, 2], run.Items.Select(i => i.SortOrder));
    }

    [Fact]
    public async Task A_vanished_template_still_lets_the_typed_items_through()
    {
        // The template branch already chose to keep the task when a template disappears. The items the user typed
        // are not collateral for that decision.
        var runs = new FakeChecklistRunRepository();

        var response = await Create(
            runs,
            Request(with: [new CreateChecklistItemRequest("kendi maddem")], templateId: Guid.NewGuid()),
            new FakeChecklistTemplateRepository());

        Assert.Equal(201, response.StatusCode);
        Assert.Equal("kendi maddem", Assert.Single(Assert.Single(runs.Runs).Items).LabelText);
    }

    [Fact]
    public async Task A_checklist_that_cannot_be_stored_does_NOT_take_the_task_down_with_it()
    {
        /*
         * THE HALF-CREATED DECISION, asserted rather than described.
         *
         * The run references the task's id so it cannot be written first, and there is no transaction across the
         * two. When the run write fails the TASK IS KEPT: what the user typed into the form is their work, and
         * destroying it to keep a checklist intact is the worse trade. That is only defensible because the
         * recovery path exists — the detail page's add row, built in this same round.
         */
        var runs = new FakeChecklistRunRepository { FailNextCreate = true };
        var tasks = new FakeTaskItemRepository();

        var response = await Create(runs, Request(with: [new CreateChecklistItemRequest("kaybolacak madde")]), tasks: tasks);

        Assert.Equal(201, response.StatusCode);
        Assert.Single(tasks.Items);
        Assert.Empty(runs.Runs);
    }

    // ── The gate: the two levels that behave differently ────────────────────────────────────────────────────

    [Fact]
    public async Task A_BLOCKING_item_written_at_create_stops_the_task_from_closing()
    {
        var runs = new FakeChecklistRunRepository();
        var tasks = new FakeTaskItemRepository();
        await Create(runs, Request(with:
        [
            new CreateChecklistItemRequest("Fatura eki yüklendi", ChecklistItemRequirement.Blocking)
        ]), tasks: tasks);

        var task = tasks.Items.Single();
        task.Lifecycle = TaskLifecycle.InProgress;

        var response = await Complete(tasks, runs, task);

        // The gate that already existed — this round writes the item, it does not touch TaskBlockingRules.
        Assert.Equal(409, response.StatusCode);
        Assert.Equal(TaskReasonCodes.ChecklistIncomplete, response.ReasonCode);
    }

    [Fact]
    public async Task A_REQUIRED_item_does_NOT_stop_it
        ()
    {
        // The difference between the two levels, which is the whole reason there are three. An unfinished
        // `Required` item is an expectation; only `Blocking` is a barrier.
        var runs = new FakeChecklistRunRepository();
        var tasks = new FakeTaskItemRepository();
        await Create(runs, Request(with:
        [
            new CreateChecklistItemRequest("Muhasebe onayı alındı", ChecklistItemRequirement.Required)
        ]), tasks: tasks);

        var task = tasks.Items.Single();
        task.Lifecycle = TaskLifecycle.InProgress;

        Assert.Equal(204, (await Complete(tasks, runs, task)).StatusCode);
    }

    [Fact]
    public async Task Ticking_the_blocking_item_releases_the_task()
    {
        // Non-vacuity for the refusal above: the gate has to OPEN, or the test would pass on a task that could
        // never be completed for some other reason.
        var runs = new FakeChecklistRunRepository();
        var tasks = new FakeTaskItemRepository();
        await Create(runs, Request(with:
        [
            new CreateChecklistItemRequest("Fatura eki yüklendi", ChecklistItemRequirement.Blocking)
        ]), tasks: tasks);

        var task = tasks.Items.Single();
        task.Lifecycle = TaskLifecycle.InProgress;
        var run = runs.Runs.Single();

        var tick = await new SetChecklistItemStateHandler(
                tasks, runs, new TaskChecklistService(), new FakeCurrentUserContext(TaskTestData.Me))
            .Handle(
                new SetChecklistItemStateCommand(
                    task.Id,
                    new SetChecklistItemStateRequest(run.Items[0].Code, Completed: true, run.Version),
                    "corr"),
                CancellationToken.None);

        Assert.Equal(204, tick.StatusCode);
        Assert.Equal(204, (await Complete(tasks, runs, task)).StatusCode);
    }

    // ── Adding to a task that has no run yet ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_FIRST_item_added_from_the_detail_page_starts_the_run()
    {
        /*
         * The path a create with no items has to leave open, and the reason the projection now ships an empty
         * checklist container: a task that never got items at creation must still be able to grow one.
         */
        var task = SelfTask();
        var tasks = new FakeTaskItemRepository(task);
        var runs = new FakeChecklistRunRepository();

        var response = await new AddChecklistItemHandler(
                tasks, runs, new TaskChecklistService(),
                new FakeCurrentUserContext(TaskTestData.Me), new FakeTenantContext(TaskTestData.Tenant))
            .Handle(
                new AddChecklistItemCommand(
                    task.Id,
                    // Version 0 — what the empty container publishes, meaning "no run exists yet".
                    new AddChecklistItemRequest("sonradan eklenen madde", ChecklistItemRequirement.Blocking, 0),
                    "corr"),
                CancellationToken.None);

        Assert.Equal(204, response.StatusCode);
        var item = Assert.Single(Assert.Single(runs.Runs).Items);
        Assert.Equal("sonradan eklenen madde", item.LabelText);
        Assert.Equal(ChecklistItemRequirement.Blocking, item.Requirement);
    }

    // ── DRIVERS ─────────────────────────────────────────────────────────────────────────────────────────────

    private static Task<Response<NoContent>> Complete(
        FakeTaskItemRepository tasks,
        FakeChecklistRunRepository runs,
        TaskItem task)
        => new TransitionTaskItemHandler(
                tasks,
                new TaskLifecycleService(),
                new FakeCurrentUserContext(TaskTestData.Me),
                runs,
                new TaskChecklistService(),
                new FakeWorkflowTransitionGate(),
                new FakeTaskDependencyRepository(),
                new FakeTaskNotificationService(),
                NullLogger<TransitionTaskItemHandler>.Instance)
            .Handle(
                new TransitionTaskItemCommand(
                    task.Id, TaskLifecycle.Done, new TaskTransitionRequest(task.Version, null, null), "corr"),
                CancellationToken.None);

    private static Task<Response<Guid>> Create(
        FakeChecklistRunRepository runs,
        CreateTaskItemRequest request,
        FakeChecklistTemplateRepository? templates = null,
        FakeTaskItemRepository? tasks = null)
        => new CreateTaskItemHandler(
                tasks ?? new FakeTaskItemRepository(),
                new FakeTaskAssignmentRepository(),
                new FakeTaskWatcherRepository(),
                new FakePositionRepository(ActivePosition()),
                new FakeOrganizationUnitRepository(LiveUnit()),
                new FakePositionAssignmentRepository(Holder(TaskTestData.Me)),
                new TaskFieldDefinitionService(new FakeTaskFieldDefinitionRepository(), TaskRecordSourceDoubles.None),
                new TaskLifecycleService(),
                new FakeTaskApprovalService(),
                templates ?? new FakeChecklistTemplateRepository(),
                runs,
                new TaskChecklistService(),
                new FakeTaskNotificationService(),
                new FakeCurrentUserContext(TaskTestData.Me),
                new FakeTenantContext(TaskTestData.Tenant),
                NullLogger<CreateTaskItemHandler>.Instance)
            .Handle(new CreateTaskItemCommand(request, "corr"), CancellationToken.None);

    private static CreateTaskItemRequest Request(
        IReadOnlyList<CreateChecklistItemRequest>? with,
        Guid? templateId = null) => new(
        Title: "Ay sonu kapanış kontrol listesi",
        Description: null,
        Priority: TaskPriority.Medium,
        AssignmentTarget: TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId: null,
        PoolPositionId: null,
        OrganizationUnitId: UnitId,
        DueAt: null,
        StartAt: null,
        PlannedDate: null,
        EstimateHours: null,
        Tags: null,
        ReviewRequired: false,
        ApprovalRequired: false,
        ApprovalManagerUserId: null,
        EmailNotificationsEnabled: false,
        DelegationAllowed: false,
        FieldValues: null,
        Watchers: null,
        ChecklistTemplateId: templateId,
        ChecklistItems: with);

    private static TaskItem SelfTask() => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "Kontrol listesi olmayan görev",
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        CreatedByUserId = TaskTestData.Me,
        OrganizationUnitId = UnitId,
        Lifecycle = TaskLifecycle.InProgress,
        Version = 1
    };

    private static PositionAssignment Holder(Guid userId) => new()
    {
        TenantId = TaskTestData.Tenant,
        PositionId = PositionId,
        UserId = userId,
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1)
    };

    private static Position ActivePosition() => new()
    {
        Id = PositionId,
        TenantId = TaskTestData.Tenant,
        Code = "QA-1",
        Name = "QA Specialist",
        OrganizationUnitId = UnitId,
        Status = PositionStatus.Active
    };

    private static OrganizationUnit LiveUnit() => new()
    {
        Id = UnitId,
        TenantId = TaskTestData.Tenant,
        Code = "OPS",
        Name = "Operations",
        LegalEntityId = Guid.NewGuid(),
        Status = OrgUnitStatus.Active
    };
}
