using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// Phase 4 — generating tasks from recurrence rules, exactly once per period.
///
/// <para><b>The core of the slice is the absence of duplicates.</b> Not on a rerun, not when two sweeps overlap,
/// not when a rule is edited. The mechanism is a CLAIM — the period's deterministic name written onto the rule
/// under an expected-version update, BEFORE the task is created — and every test below either exercises that
/// claim or proves one of the four ways a cancelled rule could otherwise keep producing work forever.</para>
/// </summary>
public sealed class TaskRecurrenceGenerationTests
{
    private static readonly Guid TenantA = TaskTestData.Tenant;
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Unit = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTimeOffset Anchor = new(2026, 1, 5, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = new(2026, 1, 8, 10, 0, 0, TimeSpan.Zero);

    // ── One period, one task ─────────────────────────────────────────────────

    [Fact]
    public async Task A_due_rule_produces_exactly_one_task()
    {
        var harness = new Harness(DailyRule());
        var result = await harness.GenerateAsync(Now);

        Assert.Equal(1, result.TasksGenerated);
        Assert.Single(harness.Tasks.Items);
    }

    [Fact]
    public async Task A_SECOND_run_over_the_same_period_produces_NOTHING()
    {
        /*
         * The rerun case. The period's name is derived, so the second pass computes the same string, finds it
         * already stamped on the rule, and stops before it reaches the create.
         */
        var harness = new Harness(DailyRule());

        await harness.GenerateAsync(Now);
        var second = await harness.GenerateAsync(Now);

        Assert.Equal(0, second.TasksGenerated);
        Assert.Equal(1, second.AlreadyGenerated);
        Assert.Single(harness.Tasks.Items);
    }

    [Fact]
    public async Task Two_runs_a_MOMENT_apart_still_produce_one_task()
    {
        // Two sweeps inside the same period — the ordinary overlap, since the sweep runs on a timer and a slow
        // pass can still be finishing when the next begins.
        var harness = new Harness(DailyRule());

        await harness.GenerateAsync(Now);
        await harness.GenerateAsync(Now.AddMinutes(1));

        Assert.Single(harness.Tasks.Items);
    }

    [Fact]
    public async Task Two_passes_that_BOTH_read_the_rule_first_still_produce_one_task()
    {
        /*
         * The genuine race, not a sequential rerun: both passes read the rule before either wrote, so both hold
         * the same expected version and both believe the period is unclaimed. The conditional write is what
         * decides — exactly one succeeds, and the loser never reaches the create.
         *
         * Executed by driving two handlers that were each handed their own snapshot of the rule.
         */
        var harness = new Harness(DailyRule());

        var first = harness.NewHandler();
        var second = harness.NewHandler();

        var a = first.Handle(new GenerateDueRecurringTasksCommand(Now, 100, "corr-a"), CancellationToken.None);
        var b = second.Handle(new GenerateDueRecurringTasksCommand(Now, 100, "corr-b"), CancellationToken.None);
        await Task.WhenAll(a, b);

        Assert.Single(harness.Tasks.Items);
        Assert.Equal(1, a.Result.Data!.TasksGenerated + b.Result.Data!.TasksGenerated);
    }

    [Fact]
    public async Task The_NEXT_period_does_produce_another_task()
    {
        /*
         * Non-vacuity for every duplicate test above: a generator that produced nothing after the first task
         * would satisfy all of them while recurrence quietly stopped recurring.
         */
        var harness = new Harness(DailyRule());

        await harness.GenerateAsync(Now);
        var next = await harness.GenerateAsync(Now.AddDays(1));

        Assert.Equal(1, next.TasksGenerated);
        Assert.Equal(2, harness.Tasks.Items.Count);
    }

    // ── The three ways a rule stops owing work ───────────────────────────────

    [Fact]
    public async Task A_rule_whose_window_has_ENDED_produces_nothing()
    {
        var rule = DailyRule();
        rule.EndsAt = Now.AddDays(-1);

        var result = await new Harness(rule).GenerateAsync(Now);

        Assert.Equal(0, result.TasksGenerated);
    }

    [Fact]
    public async Task An_INACTIVE_rule_produces_nothing()
    {
        var rule = DailyRule();
        rule.IsActive = false;

        var result = await new Harness(rule).GenerateAsync(Now);

        Assert.Equal(0, result.TasksGenerated);
    }

    [Fact]
    public async Task A_DELETED_rule_produces_nothing()
    {
        var rule = DailyRule();
        rule.DeletedAt = Now.AddDays(-2);

        var result = await new Harness(rule).GenerateAsync(Now);

        Assert.Equal(0, result.TasksGenerated);
    }

    // ── The tenant boundary ──────────────────────────────────────────────────

    [Fact]
    public async Task One_tenants_rule_produces_NOTHING_in_another_tenant()
    {
        /*
         * The sweep runs with no user context and iterates every tenant, so the only thing standing between
         * tenant A's schedule and tenant B's task list is the scope the read happens inside. Without it a rule
         * would create work in the wrong tenant — a leak of WORK rather than of information, and much harder to
         * notice than a leaked field.
         */
        var harness = new Harness(DailyRule());

        // Run the whole pass as tenant B, exactly as TenantScope.Begin would.
        harness.Tenant.SetTenant(TenantB);
        var result = await harness.GenerateAsync(Now);

        Assert.Equal(0, result.RulesConsidered);
        Assert.Equal(0, result.TasksGenerated);
        Assert.Empty(harness.Tasks.Items);
    }

    [Fact]
    public async Task And_the_task_it_DOES_produce_belongs_to_its_own_tenant()
    {
        // Non-vacuity for the test above, and the half that matters: work must land where the rule lives.
        var harness = new Harness(DailyRule());

        await harness.GenerateAsync(Now);

        Assert.Equal(TenantA, Assert.Single(harness.Tasks.Items).TenantId);
    }

    // ── The work has an OWNER (the defect this slice fixes) ─────────────────

    [Fact]
    public async Task A_generated_task_lands_in_the_ASSIGNEES_list()
    {
        /*
         * THE DEFECT, live: a template-less rule produced a task assigned to nobody. The generator fell back to
         * SelfAssigned, and a background sweep has no "self" — the current-user context answers Guid.Empty with
         * no HTTP request behind it. The task was created, appeared in no list, and still consumed its period,
         * so it could never be regenerated either.
         */
        var harness = new Harness(DailyRule());

        await harness.GenerateAsync(Now);

        var task = Assert.Single(harness.Tasks.Items);
        Assert.Equal(TaskTestData.Rival, task.AssigneeUserId);
        // The value the defect produced. Named explicitly because "not null" would have passed on Guid.Empty.
        Assert.NotEqual(Guid.Empty, task.AssigneeUserId!.Value);
    }

    [Fact]
    public async Task A_POOLED_rule_puts_the_work_in_its_QUEUE()
    {
        // The pool half of the same defect: the queue has to survive from the rule onto the task, or a pooled
        // schedule produces work nobody can claim.
        var rule = DailyRule();
        rule.AssignmentTarget = TaskAssignmentTarget.PositionPool;
        rule.AssigneeUserId = null;
        rule.PoolPositionId = Harness.PoolPositionId;
        var harness = new Harness(rule);

        await harness.GenerateAsync(Now);

        var task = Assert.Single(harness.Tasks.Items);
        Assert.Equal(TaskAssignmentTarget.PositionPool, task.AssignmentTarget);
        Assert.Equal(Harness.PoolPositionId, task.PoolPositionId);
        // A pool task has no holder until it is claimed — that is what makes it a pool.
        Assert.Null(task.AssigneeUserId);
    }

    [Fact]
    public async Task A_generated_task_carries_an_ORGANIZATION_UNIT()
    {
        /*
         * Never null. Creation resolves it through the same graded fallback a manual create gets — the pool's
         * position, the assignee's position, then the tenant root — so the background path does NOT skip the
         * validation, it satisfies it. That is worth pinning: a task with no unit is misfiled work.
         */
        var harness = new Harness(DailyRule());

        await harness.GenerateAsync(Now);

        var task = Assert.Single(harness.Tasks.Items);
        Assert.NotEqual(Guid.Empty, task.OrganizationUnitId);
    }

    [Fact]
    public async Task A_LEGACY_rule_with_no_assignment_is_SKIPPED_without_burning_its_period()
    {
        /*
         * New rules cannot reach this state — the write path refuses them — but rules written before assignment
         * existed on the entity can. Skipping BEFORE the claim is what keeps them recoverable: the period is
         * still there, so fixing the rule regenerates it. Claiming first would have burnt it for good.
         */
        var rule = DailyRule();
        rule.AssignmentTarget = TaskAssignmentTarget.SelfAssigned;
        rule.AssigneeUserId = null;
        var harness = new Harness(rule);

        var result = await harness.GenerateAsync(Now);

        Assert.Equal(0, result.TasksGenerated);
        Assert.Equal(1, result.SkippedUnassigned);
        Assert.Empty(harness.Tasks.Items);
        // The period was NOT consumed.
        Assert.Null(harness.Rules.All.Single().LastProcessInstanceId);
    }

    [Fact]
    public async Task And_once_that_legacy_rule_is_FIXED_its_period_is_still_available()
    {
        // The point of skipping rather than claiming, stated as behaviour.
        var rule = DailyRule();
        rule.AssignmentTarget = TaskAssignmentTarget.SelfAssigned;
        rule.AssigneeUserId = null;
        var harness = new Harness(rule);

        await harness.GenerateAsync(Now);

        var stored = harness.Rules.All.Single();
        stored.AssignmentTarget = TaskAssignmentTarget.Person;
        stored.AssigneeUserId = TaskTestData.Rival;

        var afterFix = await harness.GenerateAsync(Now);

        Assert.Equal(1, afterFix.TasksGenerated);
        Assert.Equal(TaskTestData.Rival, Assert.Single(harness.Tasks.Items).AssigneeUserId);
    }

    // ── The generated task is recognisable ───────────────────────────────────

    [Fact]
    public async Task A_generated_task_carries_its_rule_and_its_period()
    {
        /*
         * The pack's requirement: a recurring instance must be distinguishable. Without these two fields it is
         * indistinguishable from a hand-made task, and nobody could answer "where did this come from?" or
         * "which period is this?".
         */
        var rule = DailyRule();
        var harness = new Harness(rule);

        await harness.GenerateAsync(Now);

        var task = Assert.Single(harness.Tasks.Items);
        Assert.Equal(rule.Id, task.RecurrenceRuleId);
        Assert.Equal(
            TaskRecurrenceSchedule.ProcessInstanceId(rule.Id, Anchor.AddDays(3)),
            task.ProcessInstanceId);
    }

    [Fact]
    public async Task Two_periods_of_one_rule_are_told_apart_by_their_stamps()
    {
        var harness = new Harness(DailyRule());

        await harness.GenerateAsync(Now);
        await harness.GenerateAsync(Now.AddDays(1));

        var stamps = harness.Tasks.Items.Select(t => t.ProcessInstanceId).ToList();
        Assert.Equal(2, stamps.Distinct().Count());
        // …and both name the same rule, so they can be grouped as one schedule's history.
        Assert.All(harness.Tasks.Items, t => Assert.Equal(harness.Rule.Id, t.RecurrenceRuleId));
    }

    [Fact]
    public async Task The_generated_task_is_due_when_the_next_occurrence_begins()
    {
        // Recurring work is expected to be finished before its replacement arrives — the recurrence's own
        // decision, made without consulting the working-time calculator (see the handler's note).
        var harness = new Harness(DailyRule());

        await harness.GenerateAsync(Now);

        Assert.Equal(Anchor.AddDays(4), Assert.Single(harness.Tasks.Items).DueAt);
    }

    // ── From a template ──────────────────────────────────────────────────────

    [Fact]
    public async Task A_TEMPLATE_rule_still_carries_the_rules_assignment()
    {
        /*
         * The rule's assignment is passed as an OVERRIDE to the from-template path, which already treats an
         * explicit value as winning over the template's default. That precedence is the right way round: a
         * template says how work is SHAPED, a rule says who this particular schedule is FOR.
         */
        var rule = DailyRule();
        rule.TaskTemplateId = Harness.TemplateId;
        var harness = new Harness(rule);

        await harness.GenerateAsync(Now);

        Assert.Equal(TaskTestData.Rival, Assert.Single(harness.Tasks.Items).AssigneeUserId);
    }

    [Fact]
    public async Task A_rule_with_a_TEMPLATE_produces_the_templates_shape()
    {
        /*
         * Through the ordinary from-template path, so the template's checklist is instantiated by the same code
         * that serves a manual create. A second create path here would be a second, subtly different task.
         */
        var rule = DailyRule();
        rule.TaskTemplateId = Harness.TemplateId;
        var harness = new Harness(rule);

        await harness.GenerateAsync(Now);

        var task = Assert.Single(harness.Tasks.Items);
        Assert.Equal("Aylık mutabakat", task.Title);
        // The checklist run the template carries, created alongside it.
        Assert.Single(harness.ChecklistRuns.Runs);
    }

    [Fact]
    public async Task A_rule_WITHOUT_a_template_produces_a_task_named_after_the_rule()
    {
        // Non-vacuity for the template test, and a legitimate shape in its own right: a simple recurring
        // reminder needs no template.
        var harness = new Harness(DailyRule());

        Assert.Null(harness.Rule.TaskTemplateId);
        await harness.GenerateAsync(Now);

        Assert.Equal("Günlük kontrol", Assert.Single(harness.Tasks.Items).Title);
    }

    // ── Every frequency reaches the generator ────────────────────────────────

    [Theory]
    [InlineData(TaskRecurrenceFrequency.Daily, 1, 3)]
    [InlineData(TaskRecurrenceFrequency.Daily, 2, 30)]
    [InlineData(TaskRecurrenceFrequency.Weekly, 1, 20)]
    [InlineData(TaskRecurrenceFrequency.Weekly, 3, 60)]
    [InlineData(TaskRecurrenceFrequency.Monthly, 1, 45)]
    [InlineData(TaskRecurrenceFrequency.Monthly, 2, 90)]
    [InlineData(TaskRecurrenceFrequency.Quarterly, 1, 120)]
    [InlineData(TaskRecurrenceFrequency.Yearly, 1, 400)]
    public async Task Every_frequency_and_interval_generates(
        TaskRecurrenceFrequency frequency, int interval, int daysLater)
    {
        var rule = DailyRule();
        rule.Frequency = frequency;
        rule.Interval = interval;
        var harness = new Harness(rule);

        var result = await harness.GenerateAsync(Anchor.AddDays(daysLater));

        Assert.Equal(1, result.TasksGenerated);
    }

    [Fact]
    public async Task A_rule_that_has_NOT_started_yet_generates_nothing()
    {
        var harness = new Harness(DailyRule());

        var result = await harness.GenerateAsync(Anchor.AddHours(-1));

        Assert.Equal(0, result.TasksGenerated);
    }

    // ── harness ──────────────────────────────────────────────────────────────

    private static TaskRecurrenceRule DailyRule() => new()
    {
        Id = Guid.Parse("11111111-2222-3333-4444-555555555555"),
        TenantId = TenantA,
        Name = "Günlük kontrol",
        AssignmentTarget = TaskAssignmentTarget.Person,
        AssigneeUserId = TaskTestData.Rival,
        Frequency = TaskRecurrenceFrequency.Daily,
        Interval = 1,
        StartsAt = Anchor,
        IsActive = true,
        Version = 1
    };

    /// <summary>
    /// The real generation handler over the real create paths. Only the stores and MOD-0023 are doubles — a
    /// harness that stubbed the create would prove nothing about the task that actually appears.
    /// </summary>
    private sealed class Harness
    {
        public static readonly Guid TemplateId = Guid.Parse("77777777-7777-7777-7777-777777777777");

        public static readonly Guid PoolPositionId = Guid.Parse("33333333-3333-3333-3333-333333333333");

        public Harness(TaskRecurrenceRule rule)
        {
            Rule = rule;
            Tenant = new FakeTenantContext(TenantA);
            Tasks = new FakeTaskItemRepository();
            ChecklistRuns = new FakeChecklistRunRepository();
            Rules = new FakeTaskRecurrenceRuleRepository(Tenant, rule);

            var templates = new FakeTaskTemplateRepository(new TaskTemplate
            {
                Id = TemplateId,
                TenantId = TenantA,
                Name = "Aylık mutabakat",
                Code = "MONTHLY-RECON",
                TitleTemplate = "Aylık mutabakat",
                IsActive = true,
                ChecklistTemplateId = FakeChecklistTemplateRepository.SeededId
            });

            Mediator = new RecurrenceMediator(this, templates);
        }

        public TaskRecurrenceRule Rule { get; }

        /// <summary>The stored rules, for assertions about what a pass DID or did not claim.</summary>
        public FakeTaskRecurrenceRuleRepository Rules { get; }

        public FakeTenantContext Tenant { get; }

        public FakeTaskItemRepository Tasks { get; }

        public FakeChecklistRunRepository ChecklistRuns { get; }

        public IMediator Mediator { get; }

        public GenerateDueRecurringTasksHandler NewHandler()
            => new(Rules, Tasks, Mediator, NullLogger<GenerateDueRecurringTasksHandler>.Instance);

        public async Task<GenerateDueRecurringTasksResponse> GenerateAsync(DateTimeOffset now)
        {
            var response = await NewHandler().Handle(
                new GenerateDueRecurringTasksCommand(now, 100, "corr"), CancellationToken.None);
            return response.Data!;
        }

        internal CreateTaskItemHandler CreateHandler() => new(
            Tasks,
            new FakeTaskAssignmentRepository(),
            new FakeTaskWatcherRepository(),
            // A genuinely assignable position, so a pooled rule reaches the create rather than failing the
            // position check for an unrelated reason.
            new FakePositionRepository(new Position
            {
                Id = PoolPositionId,
                TenantId = Tenant.TenantId,
                Code = "OPS",
                Name = "Operasyon",
                // Draft is the enum default, and creation refuses a position that is not assignable — so an
                // unset status here would fail the pool test for a reason that has nothing to do with recurrence.
                Status = Diten.Platform.Domain.Entities.Organization.PositionStatus.Active,
                OrganizationUnitId = Unit
            }),
            new FakeOrganizationUnitRepository(new OrganizationUnit
            {
                Id = Unit,
                TenantId = Tenant.TenantId,
                Code = "HQ",
                Name = "Genel Merkez",
                LegalEntityId = Guid.NewGuid()
            }),
            new FakePositionAssignmentRepository(),
            new TaskFieldDefinitionService(new FakeTaskFieldDefinitionRepository()),
            new TaskLifecycleService(),
            new FakeTaskApprovalService(),
            new FakeChecklistTemplateRepository(),
            ChecklistRuns,
            new TaskChecklistService(),
            new FakeTaskNotificationService(),
            new FakeCurrentUserContext(TaskTestData.Me),
            Tenant,
            NullLogger<CreateTaskItemHandler>.Instance);
    }

    /// <summary>
    /// Routes the two create commands the generator sends to the REAL handlers. Nothing else is accepted — an
    /// unexpected command means the generator grew a path this harness does not describe.
    /// </summary>
    private sealed class RecurrenceMediator : IMediator
    {
        private readonly Harness _harness;
        private readonly FakeTaskTemplateRepository _templates;

        public RecurrenceMediator(Harness harness, FakeTaskTemplateRepository templates)
        {
            _harness = harness;
            _templates = templates;
        }

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken ct = default)
            => request switch
            {
                CreateTaskItemCommand command
                    => (Task<TResponse>)(object)_harness.CreateHandler().Handle(command, ct),
                CreateTaskItemFromTemplateCommand command
                    => (Task<TResponse>)(object)new CreateTaskItemFromTemplateHandler(_templates, this).Handle(command, ct),
                _ => throw new InvalidOperationException($"Unexpected request {request.GetType().Name}.")
            };

        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();

        public Task Send<TRequest>(TRequest request, CancellationToken ct = default) where TRequest : IRequest
            => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request, CancellationToken ct = default) => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default)
            => throw new NotSupportedException();

        public Task Publish(object notification, CancellationToken ct = default) => throw new NotSupportedException();

        public Task Publish<TNotification>(TNotification notification, CancellationToken ct = default)
            where TNotification : INotification => throw new NotSupportedException();
    }
}
