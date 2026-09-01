using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.Tasks.SelfRegistration;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-054 — WHICH SHAPE OF THE TEMPLATE this task actually came from.
///
/// <para>A template is edited in place, so six months after a task was generated there was no way to tell whether
/// the steps it carries are the steps the template had then, or whether somebody has since rewritten it. The
/// question "why does this task have these items?" had no answer at all.</para>
///
/// <para>⚠ The stamp names the TEMPLATE's state, never the generation's. A "now" would agree with the task's own
/// creation timestamp and answer nothing — which is precisely the failure these tests are shaped to catch, and
/// why the template here is deliberately given timestamps far from the moment the test runs.</para>
/// </summary>
public sealed class TaskTemplateSnapshotTests
{
    private static readonly DateTimeOffset TemplateWritten = new(2026, 1, 5, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset TemplateLastEdited = new(2026, 6, 30, 14, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task A_task_built_from_a_template_records_HOW_OLD_that_template_was()
    {
        var harness = new Harness(edited: true);

        var created = await harness.CreateFromTemplateAsync();

        Assert.True(created.IsSuccessful);
        var task = harness.Tasks.Items.Single();
        Assert.Equal(TemplateLastEdited, task.TemplateSnapshotAt);
    }

    [Fact]
    public async Task An_UNEDITED_template_stamps_the_moment_it_was_written()
    {
        // `UpdatedAt ?? CreatedAt` — the same question asked of a template nobody has touched yet. Without the
        // fallback a brand-new template would stamp null, and a null would read as "not from a template at all".
        var harness = new Harness(edited: false);

        await harness.CreateFromTemplateAsync();

        Assert.Equal(TemplateWritten, harness.Tasks.Items.Single().TemplateSnapshotAt);
    }

    [Fact]
    public async Task The_stamp_is_the_TEMPLATE_clock_and_not_the_moment_of_generation()
    {
        /*
         * The test that makes the two above mean something. A handler that stamped UtcNow would satisfy "the
         * field is populated" and answer the opposite question — and it would be indistinguishable from the
         * task's own CreatedAt, which the task already carries.
         */
        var harness = new Harness(edited: true);

        await harness.CreateFromTemplateAsync();

        var task = harness.Tasks.Items.Single();
        Assert.NotEqual(task.CreatedAt.Date, task.TemplateSnapshotAt!.Value.Date);
        Assert.True(task.TemplateSnapshotAt < task.CreatedAt);
    }

    [Fact]
    public async Task A_task_created_by_HAND_carries_no_template_stamp()
    {
        /*
         * Non-vacuity, and a claim in its own right: the field's ABSENCE is what says "nothing here came from a
         * template". A default that populated it for every task would make the stamp meaningless.
         */
        var harness = new Harness(edited: true);

        await harness.CreateHandler().Handle(
            new CreateTaskItemCommand(harness.PlainRequest(), "corr"), CancellationToken.None);

        Assert.Null(harness.Tasks.Items.Single().TemplateSnapshotAt);
    }

    /// <summary>
    /// The MENU states the order the work actually has: define the checklist, then the task template that binds
    /// it. An administrator meeting these entries the other way round fills the picker's source in afterwards,
    /// having already saved a template with no gate — and the menu is the only instruction most people read.
    /// </summary>
    [Fact]
    public void The_checklist_template_page_sorts_BEFORE_the_task_template_page()
    {
        var pages = new TaskManifestProvider().GetManifest().Pages;

        var checklist = pages.Single(page => page.PageCode == "TASK_CHECKLIST_TEMPLATES");
        var template = pages.Single(page => page.PageCode == "TASK_TEMPLATES");

        Assert.True(checklist.SortOrder < template.SortOrder);
        // Both are findable: a settings screen reachable only by typing its URL is a defect, not a policy.
        Assert.True(checklist.IsNavigationVisible);
        Assert.True(template.IsNavigationVisible);
    }

    [Fact]
    public void Neither_template_page_is_a_personal_work_surface()
    {
        /*
         * The guard that lets both be nav-visible at all. Görev Merkezi is the single answer to "where is my
         * work"; a menu entry behind a personal-work key would be a second one. These two configure what work
         * LOOKS LIKE, so they are outside that set — asserted rather than assumed, because the whole nav-visible
         * decision rests on it.
         */
        Assert.DoesNotContain(TaskPermissions.ChecklistTemplatesManage, TaskPermissions.PersonalWorkSurfaceScoped);
        Assert.DoesNotContain(TaskPermissions.TemplatesManage, TaskPermissions.PersonalWorkSurfaceScoped);
    }

    // ── harness ──────────────────────────────────────────────────────────────

    private sealed class Harness
    {
        private static readonly Guid TemplateId = Guid.Parse("77777777-7777-7777-7777-777777777777");
        private static readonly Guid Unit = Guid.Parse("88888888-8888-8888-8888-888888888888");

        private readonly FakeTaskTemplateRepository _templates;
        private readonly FakeTenantContext _tenant = new(TaskTestData.Tenant);

        public Harness(bool edited)
        {
            Tasks = new FakeTaskItemRepository();
            _templates = new FakeTaskTemplateRepository(new TaskTemplate
            {
                Id = TemplateId,
                TenantId = TaskTestData.Tenant,
                Code = "MONTHLY-CLOSE",
                Name = "Ay sonu kapanış",
                TitleTemplate = "Ay sonu kapanış",
                IsActive = true,
                CreatedAt = TemplateWritten,
                // Null when nobody has edited it — which is what makes the `?? CreatedAt` fallback observable.
                UpdatedAt = edited ? TemplateLastEdited : null
            });
        }

        public FakeTaskItemRepository Tasks { get; }

        public Task<Diten.Platform.Application.Common.Response<Guid>> CreateFromTemplateAsync()
            => new CreateTaskItemFromTemplateHandler(_templates, new SnapshotMediator(this)).Handle(
                new CreateTaskItemFromTemplateCommand(
                    new CreateTaskFromTemplateRequest(
                        TaskTemplateId: TemplateId,
                        TitleOverride: null,
                        DueAt: null,
                        AssignmentTargetOverride: TaskAssignmentTarget.SelfAssigned,
                        AssigneeUserId: null,
                        PoolPositionId: null),
                    "corr"),
                CancellationToken.None);

        public CreateTaskItemRequest PlainRequest() => new(
            Title: "Elle açılmış görev",
            Description: null,
            Priority: TaskPriority.Medium,
            AssignmentTarget: TaskAssignmentTarget.SelfAssigned,
            AssigneeUserId: null,
            PoolPositionId: null,
            OrganizationUnitId: null,
            DueAt: null,
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
            Watchers: null);

        public CreateTaskItemHandler CreateHandler() => new(
            Tasks,
            new FakeTaskAssignmentRepository(),
            new FakeTaskWatcherRepository(),
            new FakePositionRepository(),
            new FakeOrganizationUnitRepository(new OrganizationUnit
            {
                Id = Unit,
                TenantId = TaskTestData.Tenant,
                Code = "HQ",
                Name = "Genel Merkez",
                LegalEntityId = Guid.NewGuid()
            }),
            new FakePositionAssignmentRepository(),
            new TaskFieldDefinitionService(
                new FakeTaskFieldDefinitionRepository(), TaskRecordSourceDoubles.None, TaskActors.PermitAll()),
            new TaskLifecycleService(),
            new FakeTaskApprovalService(),
            new FakeChecklistTemplateRepository(),
            new FakeChecklistRunRepository(),
            new TaskChecklistService(),
            new FakeTaskNotificationService(),
            new FakeCurrentUserContext(TaskTestData.Me),
            _tenant,
            NullLogger<CreateTaskItemHandler>.Instance,
            TaskDocumentFreezerDoubles.OverAnEmptyRegister());

        /// <summary>Routes the ONE command the from-template handler sends to the real create handler.</summary>
        private sealed class SnapshotMediator(Harness harness) : IMediator
        {
            public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
                => request switch
                {
                    CreateTaskItemCommand command
                        => (Task<TResponse>)(object)harness.CreateHandler().Handle(command, ct),
                    _ => throw new InvalidOperationException($"Unexpected request {request.GetType().Name}.")
                };

            public Task<object?> Send(object request, CancellationToken ct = default)
                => throw new NotSupportedException();

            public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest
                => throw new NotSupportedException();

            public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
                IStreamRequest<TResponse> request, CancellationToken ct = default)
                => throw new NotSupportedException();

            public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default)
                => throw new NotSupportedException();

            public Task Publish(object notification, CancellationToken ct = default)
                => throw new NotSupportedException();

            public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
                where TNotification : INotification => throw new NotSupportedException();
        }
    }
}
