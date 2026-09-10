using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Handlers.QueryHandlers;
using Diten.Platform.Application.Features.Tasks.Queries;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using static Diten.Platform.Application.Tests.Tasks.AssignmentWorld;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-057 at the WRITE — the server says what the picker says, no more and no less.
///
/// <para><b>The defect.</b> "Who may I hand work to" was enforced only by the pickers. Create checked nothing for a
/// person and half of it for a pool, reassign checked eligibility but not scope, and recurrence rules checked
/// nothing — so a client posting straight to the API could hand work to anybody in the tenant, including a person
/// in another company.</para>
///
/// <para>Every case runs the PRODUCTION handler over the PRODUCTION guard, scope resolver and eligibility rule; only
/// the stores are doubles. The matrix is <see cref="AssignmentWorld"/>: each person there passes or fails exactly one
/// leg of the rule.</para>
/// </summary>
public sealed class TaskAssignmentWriteGuardTests
{
    public static TheoryData<Guid, bool> People() => Matrix(EveryPerson, AssignablePeople);

    /// <summary>Reassign moves work away from me, so I cannot be the new holder of my own task.</summary>
    public static TheoryData<Guid, bool> PeopleOtherThanMe() => Matrix(EveryPerson.Where(p => p != Me), AssignablePeople);

    public static TheoryData<Guid, bool> Pools() => Matrix(EveryPosition, AssignablePools);

    // ── the picker and the server are ONE answer ─────────────────────────────

    [Fact]
    public async Task The_server_accepts_exactly_the_people_the_picker_lists()
    {
        var picker = new GetTaskAssignmentPersonLookupHandler(
            SeatRepository(), PositionRepository(), UnitRepository(), new FakeUserDisplayNameResolver(),
            ScopeResolver(PositionRepository(), UnitRepository(), Me));
        var listed = (await picker.Handle(
                new GetTaskAssignmentPersonLookupQuery("corr", TaskPersonLookupPurpose.Assignment),
                CancellationToken.None))
            .Data!.People.Select(row => row.UserId).ToHashSet();

        var guard = Guard(TaskActors.Holding(TaskPermissions.Assign));
        var accepted = new HashSet<Guid>();
        foreach (var person in EveryPerson)
        {
            if (await guard.CheckPersonAsync(person, CancellationToken.None) is null) { accepted.Add(person); }
        }

        Assert.Equal(Sorted(listed), Sorted(accepted));
        // Non-vacuity: two empty sets are equal too. This is the matrix the prompt named, stated as data.
        Assert.Equal(Sorted(AssignablePeople), Sorted(accepted));
    }

    [Fact]
    public async Task The_server_accepts_exactly_the_pools_the_picker_lists()
    {
        var picker = new GetTaskAssignmentPositionLookupHandler(
            PositionRepository(), UnitRepository(), SeatRepository(),
            ScopeResolver(PositionRepository(), UnitRepository(), Me));
        var listed = (await picker.Handle(new GetTaskAssignmentPositionLookupQuery("corr"), CancellationToken.None))
            .Data!.Select(row => row.PositionId).ToHashSet();

        // No ASSIGN: the pool picker does not ask for it, so neither may the server.
        var guard = Guard(TaskActors.None());
        var accepted = new HashSet<Guid>();
        foreach (var position in EveryPosition)
        {
            if (await guard.CheckPoolAsync(position, CancellationToken.None) is null) { accepted.Add(position); }
        }

        Assert.Equal(Sorted(listed), Sorted(accepted));
        Assert.Equal(Sorted(AssignablePools), Sorted(accepted));
    }

    // ── create ───────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(People))]
    public async Task Create_for_a_person(Guid person, bool accepted)
    {
        var run = new CreateRun();

        var response = await run.CreateAsync(TaskAssignmentTarget.Person, person: person);

        if (accepted)
        {
            Assert.Equal(201, response.StatusCode);
            Assert.Equal(person, Assert.Single(run.Tasks.Items).AssigneeUserId);
        }
        else
        {
            AssertRefused(response, 400, TaskReasonCodes.AssigneeNotAssignable);
            Assert.Empty(run.Tasks.Items);
        }
    }

    [Theory]
    [MemberData(nameof(Pools))]
    public async Task Create_for_a_pool(Guid position, bool accepted)
    {
        var run = new CreateRun(TaskActors.None());

        var response = await run.CreateAsync(TaskAssignmentTarget.PositionPool, pool: position);

        if (accepted)
        {
            Assert.Equal(201, response.StatusCode);
            Assert.Equal(position, Assert.Single(run.Tasks.Items).PoolPositionId);
        }
        else
        {
            AssertRefused(response, 400, TaskReasonCodes.PositionNotAssignable);
            Assert.Empty(run.Tasks.Items);
        }
    }

    [Fact]
    public async Task Create_for_somebody_else_WITHOUT_the_assign_permission_is_403_and_writes_nothing()
    {
        // The route only needs Create. Without this check a user who may not assign could name anybody in the body.
        var run = new CreateRun(TaskActors.Holding(TaskPermissions.Create));

        var response = await run.CreateAsync(TaskAssignmentTarget.Person, person: Colleague);

        AssertRefused(response, 403);
        Assert.Empty(run.Tasks.Items);
    }

    [Fact]
    public async Task Create_for_MYSELF_needs_neither_the_assign_permission_nor_a_seat()
    {
        // NoPosition is the actor here: somebody the picker would never list, creating their own work.
        var selfAssigned = new CreateRun(TaskActors.None(), actor: NoPosition);
        var namedSelf = new CreateRun(TaskActors.None(), actor: NoPosition);

        var viaTarget = await selfAssigned.CreateAsync(TaskAssignmentTarget.SelfAssigned);
        var viaPerson = await namedSelf.CreateAsync(TaskAssignmentTarget.Person, person: NoPosition);

        Assert.Equal(201, viaTarget.StatusCode);
        Assert.Equal(201, viaPerson.StatusCode);
        Assert.Equal(NoPosition, Assert.Single(namedSelf.Tasks.Items).AssigneeUserId);
    }

    [Fact]
    public async Task Create_for_my_superior_IN_MY_COMPANY_passes_and_opens_a_request()
    {
        // BL-023 stays where it was: scope first, and a superior who passes it becomes a request, not an order.
        var run = new CreateRun();

        var response = await run.CreateAsync(TaskAssignmentTarget.Person, person: HomeBoss);

        Assert.Equal(201, response.StatusCode);
        Assert.Single(run.Upward.Opened);
        Assert.NotNull(Assert.Single(run.Tasks.Items).RequestWorkflowInstanceId);
    }

    [Fact]
    public async Task Create_for_my_superior_in_ANOTHER_company_is_refused_and_opens_nothing()
    {
        // He is above me, but not reachable: no leg of the scope rule holds. Refused before any write or request.
        var run = new CreateRun();

        var response = await run.CreateAsync(TaskAssignmentTarget.Person, person: ForeignBoss);

        AssertRefused(response, 400, TaskReasonCodes.AssigneeNotAssignable);
        Assert.Empty(run.Tasks.Items);
        Assert.Empty(run.Upward.Opened);
    }

    [Fact]
    public async Task The_recurrence_SWEEP_is_not_asked_about_a_caller_it_does_not_have()
    {
        /*
         * Pinned so the sweep stays exactly as it was (out of this round's scope): it has no permissions and no
         * scope, and asking the guard for it would refuse every rule. The rule was checked when it was saved.
         */
        var run = new CreateRun(TaskActors.None());

        var response = await run.CreateAsync(TaskAssignmentTarget.Person, person: ForeignPeer, sweep: true);

        Assert.Equal(201, response.StatusCode);
    }

    // ── reassign ─────────────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(PeopleOtherThanMe))]
    public async Task Reassign_to_a_person(Guid person, bool accepted)
    {
        var run = new ReassignRun();

        var response = await run.ReassignAsync(person);

        if (accepted)
        {
            Assert.Equal(204, response.StatusCode);
            Assert.Equal(person, run.Stored.AssigneeUserId);
        }
        else
        {
            AssertRefused(response, 400, TaskReasonCodes.AssigneeNotAssignable);
            Assert.Equal(Me, run.Stored.AssigneeUserId);
            Assert.Equal(1, run.Stored.Version);
        }
    }

    [Fact]
    public async Task Reassign_WITHOUT_the_assign_permission_is_403_and_the_task_is_untouched()
    {
        // The route carries [HasPermission(Assign)] and so does the work-item dispatcher; the handler now says the
        // same thing itself, so a third caller cannot forget it.
        var run = new ReassignRun(TaskActors.Holding(TaskPermissions.Update));

        var response = await run.ReassignAsync(Colleague);

        AssertRefused(response, 403);
        Assert.Equal(Me, run.Stored.AssigneeUserId);
    }

    [Fact]
    public async Task The_requester_may_take_the_work_over_themselves()
    {
        var run = new ReassignRun(holder: Colleague);

        var response = await run.ReassignAsync(Me);

        Assert.Equal(204, response.StatusCode);
        Assert.Equal(Me, run.Stored.AssigneeUserId);
    }

    // ── recurrence rules ─────────────────────────────────────────────────────

    [Theory]
    [MemberData(nameof(People))]
    public async Task Recurrence_rule_create_for_a_person(Guid person, bool accepted)
    {
        var run = new RuleRun();

        var response = await run.CreateAsync(TaskAssignmentTarget.Person, person: person);

        if (accepted)
        {
            Assert.Equal(201, response.StatusCode);
            Assert.Equal(person, Assert.Single(run.Rules.All).AssigneeUserId);
        }
        else
        {
            AssertRefused(response, 400, TaskReasonCodes.AssigneeNotAssignable);
            Assert.Empty(run.Rules.All);
        }
    }

    [Theory]
    [MemberData(nameof(Pools))]
    public async Task Recurrence_rule_create_for_a_pool(Guid position, bool accepted)
    {
        var run = new RuleRun(TaskActors.None());

        var response = await run.CreateAsync(TaskAssignmentTarget.PositionPool, pool: position);

        if (accepted)
        {
            Assert.Equal(201, response.StatusCode);
        }
        else
        {
            AssertRefused(response, 400, TaskReasonCodes.PositionNotAssignable);
            Assert.Empty(run.Rules.All);
        }
    }

    [Theory]
    [MemberData(nameof(People))]
    public async Task Recurrence_rule_update_for_a_person(Guid person, bool accepted)
    {
        var run = new RuleRun(seeded: true);

        var response = await run.UpdateAsync(TaskAssignmentTarget.Person, person: person);

        if (accepted)
        {
            Assert.Equal(204, response.StatusCode);
            Assert.Equal(person, Assert.Single(run.Rules.All).AssigneeUserId);
        }
        else
        {
            AssertRefused(response, 400, TaskReasonCodes.AssigneeNotAssignable);
            Assert.Equal(Colleague, Assert.Single(run.Rules.All).AssigneeUserId);
        }
    }

    [Theory]
    [MemberData(nameof(Pools))]
    public async Task Recurrence_rule_update_for_a_pool(Guid position, bool accepted)
    {
        var run = new RuleRun(TaskActors.None(), seeded: true);

        var response = await run.UpdateAsync(TaskAssignmentTarget.PositionPool, pool: position);

        if (accepted)
        {
            Assert.Equal(204, response.StatusCode);
            Assert.Equal(position, Assert.Single(run.Rules.All).PoolPositionId);
        }
        else
        {
            AssertRefused(response, 400, TaskReasonCodes.PositionNotAssignable);
            var kept = Assert.Single(run.Rules.All);
            Assert.Equal(Colleague, kept.AssigneeUserId);
            Assert.Null(kept.PoolPositionId);
        }
    }

    [Fact]
    public async Task Recurrence_rule_for_somebody_else_WITHOUT_the_assign_permission_is_403()
    {
        var create = new RuleRun(TaskActors.None());
        var update = new RuleRun(TaskActors.None(), seeded: true);

        AssertRefused(await create.CreateAsync(TaskAssignmentTarget.Person, person: Colleague), 403);
        AssertRefused(await update.UpdateAsync(TaskAssignmentTarget.Person, person: GrantedUnitHolder), 403);
        Assert.Empty(create.Rules.All);
        Assert.Equal(Colleague, Assert.Single(update.Rules.All).AssigneeUserId);
    }

    [Fact]
    public async Task Recurrence_rule_for_MYSELF_needs_no_assign_permission()
    {
        var run = new RuleRun(TaskActors.None());

        var response = await run.CreateAsync(TaskAssignmentTarget.Person, person: Me);

        Assert.Equal(201, response.StatusCode);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static TheoryData<Guid, bool> Matrix(IEnumerable<Guid> candidates, IReadOnlySet<Guid> accepted)
    {
        var data = new TheoryData<Guid, bool>();
        foreach (var candidate in candidates) { data.Add(candidate, accepted.Contains(candidate)); }
        return data;
    }

    private static List<Guid> Sorted(IEnumerable<Guid> ids) => ids.OrderBy(id => id).ToList();

    private static void AssertRefused<T>(Response<T> response, int status, string? reasonCode = null)
    {
        Assert.False(response.IsSuccessful);
        Assert.Equal(status, response.StatusCode);
        if (reasonCode is not null) { Assert.Equal(reasonCode, response.ReasonCode); }
    }

    private static TaskAssignmentGuard GuardFor(
        FakePositionAssignmentRepository seats,
        FakePositionRepository positions,
        FakeOrganizationUnitRepository units,
        TaskAssignmentScopeResolver scopes,
        IActorPermissionContext permissions,
        Guid actor)
        => new(seats, positions, units, scopes, permissions, new FakeCurrentUserContext(actor));

    private sealed class CreateRun
    {
        private readonly CreateTaskItemHandler _handler;

        public CreateRun(IActorPermissionContext? permissions = null, Guid? actor = null)
        {
            var who = actor ?? Me;
            var seats = SeatRepository();
            var positions = PositionRepository();
            var units = UnitRepository();
            var scopes = ScopeResolver(positions, units, who);

            _handler = new CreateTaskItemHandler(
                Tasks,
                new FakeTaskAssignmentRepository(),
                new FakeTaskWatcherRepository(),
                positions,
                units,
                seats,
                new TaskFieldDefinitionService(
                    new FakeTaskFieldDefinitionRepository(), TaskRecordSourceDoubles.None, TaskActors.PermitAll()),
                new TaskLifecycleService(),
                new FakeTaskApprovalService(),
                new FakeChecklistTemplateRepository(),
                new FakeChecklistRunRepository(),
                new TaskChecklistService(),
                new FakeTaskNotificationService(),
                new FakeCurrentUserContext(who),
                new FakeTenantContext(TaskTestData.Tenant),
                NullLogger<CreateTaskItemHandler>.Instance,
                TaskDocumentFreezerDoubles.OverAnEmptyRegister(),
                GuardFor(seats, positions, units, scopes,
                    permissions ?? TaskActors.Holding(TaskPermissions.Create, TaskPermissions.Assign), who),
                new TaskAssignmentDirection(scopes, seats, new FakeCurrentUserContext(who)),
                Upward);
        }

        public FakeTaskItemRepository Tasks { get; } = new();

        public RecordingUpwardRequests Upward { get; } = new();

        public Task<Response<Guid>> CreateAsync(
            TaskAssignmentTarget target, Guid? person = null, Guid? pool = null, bool sweep = false)
            => _handler.Handle(
                new CreateTaskItemCommand(
                    new CreateTaskItemRequest(
                        Title: "Kapsam denemesi",
                        Description: null,
                        Priority: TaskPriority.Medium,
                        AssignmentTarget: target,
                        AssigneeUserId: person,
                        PoolPositionId: pool,
                        OrganizationUnitId: null,
                        DueAt: null,
                        StartAt: null,
                        PlannedDate: null,
                        EstimateHours: null,
                        Tags: null,
                        ReviewRequired: false,
                        ApprovalRequired: false,
                        ApprovalManagerUserId: null,
                        EmailNotificationsEnabled: false,
                        DelegationAllowed: true,
                        FieldValues: null,
                        Watchers: null),
                    "corr",
                    IsScheduledGeneration: sweep),
                CancellationToken.None);
    }

    private sealed class ReassignRun
    {
        private readonly ReassignTaskItemHandler _handler;
        private readonly FakeTaskItemRepository _tasks;
        private readonly TaskItem _task;

        /// <param name="holder">Who holds the task now. Me by default; the requester is Rival unless I am taking
        /// it over, in which case I am the requester.</param>
        public ReassignRun(IActorPermissionContext? permissions = null, Guid? holder = null)
        {
            _task = new TaskItem
            {
                TenantId = TaskTestData.Tenant,
                Title = "Devredilecek iş",
                AssignmentTarget = TaskAssignmentTarget.Person,
                AssigneeUserId = holder ?? Me,
                CreatedByUserId = holder is null ? TaskTestData.Rival : Me,
                OrganizationUnitId = HomeUnit,
                Lifecycle = TaskLifecycle.InProgress,
                DelegationAllowed = true,
                Version = 1
            };
            _tasks = new FakeTaskItemRepository(_task);

            var seats = SeatRepository();
            var positions = PositionRepository();
            var units = UnitRepository();
            _handler = new ReassignTaskItemHandler(
                _tasks,
                new FakeTaskAssignmentRepository(),
                GuardFor(seats, positions, units, ScopeResolver(positions, units, Me),
                    permissions ?? TaskActors.Holding(TaskPermissions.Assign), Me),
                new FakeCurrentUserContext(Me),
                new FakeTenantContext(TaskTestData.Tenant));
        }

        public TaskItem Stored => _tasks.Items.Single();

        public Task<Response<NoContent>> ReassignAsync(Guid to)
            => _handler.Handle(
                new ReassignTaskItemCommand(
                    _task.Id, new ReassignTaskItemRequest(_task.Version, to, "Devrediyorum."), "corr"),
                CancellationToken.None);
    }

    private sealed class RuleRun
    {
        private readonly CreateTaskRecurrenceRuleHandler _create;
        private readonly UpdateTaskRecurrenceRuleHandler _update;
        private readonly TaskRecurrenceRule? _seeded;

        public RuleRun(IActorPermissionContext? permissions = null, bool seeded = false)
        {
            var tenant = new FakeTenantContext(TaskTestData.Tenant);
            _seeded = seeded
                ? new TaskRecurrenceRule
                {
                    TenantId = TaskTestData.Tenant,
                    Name = "Haftalık kontrol",
                    Frequency = TaskRecurrenceFrequency.Weekly,
                    Interval = 1,
                    AssignmentTarget = TaskAssignmentTarget.Person,
                    AssigneeUserId = Colleague,
                    IsActive = true
                }
                : null;
            Rules = _seeded is null
                ? new FakeTaskRecurrenceRuleRepository(tenant)
                : new FakeTaskRecurrenceRuleRepository(tenant, _seeded);

            var seats = SeatRepository();
            var positions = PositionRepository();
            var units = UnitRepository();
            var guard = GuardFor(seats, positions, units, ScopeResolver(positions, units, Me),
                permissions ?? TaskActors.Holding(TaskPermissions.RecurrenceManage, TaskPermissions.Assign), Me);
            var user = new FakeCurrentUserContext(Me);

            _create = new CreateTaskRecurrenceRuleHandler(Rules, tenant, user, guard);
            _update = new UpdateTaskRecurrenceRuleHandler(Rules, user, guard);
        }

        public FakeTaskRecurrenceRuleRepository Rules { get; }

        public Task<Response<Guid>> CreateAsync(TaskAssignmentTarget target, Guid? person = null, Guid? pool = null)
            => _create.Handle(
                new CreateTaskRecurrenceRuleCommand(
                    new CreateTaskRecurrenceRuleRequest(
                        Name: "Haftalık kontrol",
                        Frequency: TaskRecurrenceFrequency.Weekly,
                        Interval: 1,
                        StartsAt: null,
                        EndsAt: null,
                        AssignmentTarget: target,
                        AssigneeUserId: person,
                        PoolPositionId: pool,
                        OrganizationUnitId: null,
                        TaskTemplateId: null,
                        IsActive: true),
                    "corr"),
                CancellationToken.None);

        public Task<Response<NoContent>> UpdateAsync(
            TaskAssignmentTarget target, Guid? person = null, Guid? pool = null)
            => _update.Handle(
                new UpdateTaskRecurrenceRuleCommand(
                    _seeded!.Id,
                    new UpdateTaskRecurrenceRuleRequest(
                        Name: "Haftalık kontrol",
                        Frequency: TaskRecurrenceFrequency.Weekly,
                        Interval: 1,
                        StartsAt: null,
                        EndsAt: null,
                        AssignmentTarget: target,
                        AssigneeUserId: person,
                        PoolPositionId: pool,
                        OrganizationUnitId: null,
                        TaskTemplateId: null,
                        IsActive: true,
                        ExpectedVersion: _seeded.Version),
                    "corr"),
                CancellationToken.None);
    }
}

/// <summary>BL-023's MOD-0023 handoff, recorded rather than performed: the tests ask only WHETHER it was opened.</summary>
internal sealed class RecordingUpwardRequests : ITaskUpwardRequestService
{
    public List<Guid> Opened { get; } = [];

    public Task<Guid?> TryStartRequestAsync(TaskItem task, CancellationToken ct)
    {
        Opened.Add(task.Id);
        return Task.FromResult<Guid?>(Guid.NewGuid());
    }
}
