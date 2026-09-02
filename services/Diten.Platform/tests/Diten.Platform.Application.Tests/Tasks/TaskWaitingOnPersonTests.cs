using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WHO a parked task is waiting on (2026-08-15).
///
/// <para>The projection has carried a <c>waitingOn</c> slot since WC-1 with a hard <c>null</c> in it, and the
/// comment beside it said why: "stays null until something can resolve a real identity to put there." The holder
/// now names the person when they park the task, so the slot has something to carry.</para>
///
/// <para><b>Optional, and that is the whole design.</b> A wait is often on somebody this system has never heard
/// of — a supplier, a customer, an authority — and the reason sentence already says so. Requiring a selection
/// would make the honest answer unreachable.</para>
/// </summary>
public sealed class TaskWaitingOnPersonTests
{
    /// <summary>
    /// MUTATION TARGET (optional). Parking with no person must behave EXACTLY as it did before this field
    /// existed — the request omits the key entirely, and the task stores nothing.
    /// </summary>
    [Fact]
    public async Task Parking_without_naming_anybody_still_works()
    {
        var fixture = new Fixture();

        var result = await fixture.InquireAsync("Muhasebeden ekstre bekleniyor.");

        Assert.Equal(204, result.StatusCode);
        Assert.Equal(TaskLifecycle.Waiting, fixture.Task.Lifecycle);
        Assert.Equal("Muhasebeden ekstre bekleniyor.", fixture.Task.WaitingReason);
        Assert.Null(fixture.Task.WaitingOnUserId);
    }

    [Fact]
    public async Task Naming_somebody_stores_them_beside_the_reason()
    {
        var fixture = new Fixture();

        await fixture.InquireAsync("Onayını bekliyorum.", TaskTestData.Other);

        Assert.Equal(TaskTestData.Other, fixture.Task.WaitingOnUserId);
        Assert.Equal("Onayını bekliyorum.", fixture.Task.WaitingReason);
    }

    [Fact]
    public async Task Somebody_the_tenant_cannot_assign_work_to_is_refused()
    {
        // The SAME eligibility rule the assignment picker uses. Without it a caller could park a task "waiting
        // on" an identity from another tenant, and the projection would then resolve and PRINT that name.
        var fixture = new Fixture();

        var result = await fixture.InquireAsync("Bekliyorum.", Guid.NewGuid());

        Assert.Equal(400, result.StatusCode);
        Assert.Equal(TaskReasonCodes.AssigneeNotAssignable, result.ReasonCode);
        Assert.NotEqual(TaskLifecycle.Waiting, fixture.Task.Lifecycle);
    }

    [Fact]
    public async Task Parking_a_second_time_without_a_person_does_not_inherit_the_first_one()
    {
        var fixture = new Fixture();
        await fixture.InquireAsync("İlk bekleyiş.", TaskTestData.Other);
        await fixture.ResumeAsync();

        await fixture.InquireAsync("İkinci bekleyiş, kimseyi beklemiyorum.");

        Assert.Null(fixture.Task.WaitingOnUserId);
    }

    // ── Leaving Waiting ──────────────────────────────────────────────────────

    /// <summary>
    /// MUTATION TARGET (clearing). ⚠ THIS WAS A DEFECT BEFORE IT WAS A FEATURE. Two comments claimed the reason
    /// was cleared on resume — <c>TaskItem.WaitingReason</c>'s own summary and the inquire handler's note about
    /// copying it into history "because WaitingReason is CLEARED when the task resumes". MEASURED 2026-08-15:
    /// nothing in the codebase ever set it back to null.
    /// </summary>
    /// <remarks>
    /// The two LEGAL exits, measured from the transition table rather than assumed: `Waiting → InProgress`
    /// (resume) and `Waiting → Cancelled`. `Done` is deliberately not among them — finished work leaves through
    /// InProgress — and a test asserting it would have been asserting a transition the product refuses.
    /// </remarks>
    [Theory]
    [InlineData(TaskLifecycle.InProgress)]
    [InlineData(TaskLifecycle.Cancelled)]
    public async Task Leaving_Waiting_drops_the_reason_AND_the_person(TaskLifecycle target)
    {
        var fixture = new Fixture();
        await fixture.InquireAsync("Bekliyorum.", TaskTestData.Other);

        await fixture.TransitionAsync(target);

        Assert.Null(fixture.Task.WaitingReason);
        Assert.Null(fixture.Task.WaitingOnUserId);
    }

    [Fact]
    public async Task The_reason_survives_in_the_history_after_it_is_cleared()
    {
        // The whole justification for clearing: "what was this blocked on in March" still has an answer.
        var fixture = new Fixture();
        await fixture.InquireAsync("Muhasebeden ekstre bekleniyor.", TaskTestData.Other);

        await fixture.ResumeAsync();

        Assert.Contains(
            fixture.Tasks.Transitions.Events,
            entry => entry.Kind == TaskTransitionKind.Waiting
                && entry.Reason == "Muhasebeden ekstre bekleniyor.");
    }

    // ── The projection ───────────────────────────────────────────────────────

    [Fact]
    public async Task The_projection_names_the_person_being_waited_on()
    {
        var fixture = new Fixture();
        await fixture.InquireAsync("Onayını bekliyorum.", TaskTestData.Other);

        var waiting = (await fixture.ProjectAsync((TaskTestData.Other, "Ayşe Yılmaz"))).WaitingContext;

        Assert.NotNull(waiting);
        Assert.Equal(TaskTestData.Other.ToString(), waiting!.WaitingOn!.Id);
        Assert.Equal("Ayşe Yılmaz", waiting.WaitingOn.DisplayName);
        // The reason is NOT replaced by the person — both facts travel.
        Assert.Equal("Onayını bekliyorum.", waiting.Reason!.Text);
    }

    /// <summary>
    /// MUTATION TARGET (no raw identity). The module's rule, stated on <c>AuthorDisplayName</c> and on
    /// <c>Person</c>: a name that cannot be resolved is NULL, never a GUID. An id is not a person.
    /// </summary>
    [Fact]
    public async Task An_unresolvable_person_is_named_null_and_never_as_a_GUID()
    {
        var fixture = new Fixture();
        await fixture.InquireAsync("Bekliyorum.", TaskTestData.Other);

        // The directory answers with nothing for this id — a deleted user, or a directory that is down.
        var waiting = (await fixture.ProjectAsync()).WaitingContext;

        Assert.NotNull(waiting!.WaitingOn);
        Assert.Null(waiting.WaitingOn!.DisplayName);
        Assert.NotEqual(TaskTestData.Other.ToString(), waiting.WaitingOn.DisplayName);
    }

    [Fact]
    public async Task A_wait_on_nobody_carries_no_person_at_all()
    {
        var fixture = new Fixture();
        await fixture.InquireAsync("Tedarikçiden fiyat bekleniyor.");

        var waiting = (await fixture.ProjectAsync()).WaitingContext;

        Assert.Null(waiting!.WaitingOn);
        Assert.Equal("Tedarikçiden fiyat bekleniyor.", waiting.Reason!.Text);
    }

    private sealed class Fixture
    {
        private readonly IReadOnlyList<Position> _positions;
        private readonly IReadOnlyList<OrganizationUnit> _units;
        private readonly IReadOnlyList<PositionAssignment> _seats;

        public Fixture()
        {
            var unit = new OrganizationUnit
            {
                TenantId = TaskTestData.Tenant, Code = "OU-1", Name = "Finans",
                LegalEntityId = Guid.NewGuid(), Status = OrgUnitStatus.Active
            };
            var position = new Position
            {
                TenantId = TaskTestData.Tenant, Code = "POS-1", Name = "Uzman",
                OrganizationUnitId = unit.Id, Status = PositionStatus.Active
            };
            _units = [unit];
            _positions = [position];
            // Both people hold an active seat, so both are assignable — the eligibility rule reads seats.
            _seats =
            [
                new PositionAssignment
                {
                    TenantId = TaskTestData.Tenant, PositionId = position.Id,
                    UserId = TaskTestData.Me, EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-30)
                },
                new PositionAssignment
                {
                    TenantId = TaskTestData.Tenant, PositionId = position.Id,
                    UserId = TaskTestData.Other, EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-30)
                }
            ];

            Task = new TaskItem
            {
                TenantId = TaskTestData.Tenant,
                Title = "CT probe",
                AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
                AssigneeUserId = TaskTestData.Me,
                CreatedByUserId = TaskTestData.Me,
                OrganizationUnitId = unit.Id,
                Lifecycle = TaskLifecycle.InProgress,
                Version = 1
            };
            Tasks = new FakeTaskItemRepository(Task);
        }

        public TaskItem Task { get; }

        public FakeTaskItemRepository Tasks { get; }

        public Task<Application.Common.Response<Application.Common.NoContent>> InquireAsync(
            string reason, Guid? waitingOn = null)
            => new InquireTaskItemHandler(
                    Tasks, new TaskLifecycleService(), new FakeCurrentUserContext(TaskTestData.Me),
                    new FakePositionAssignmentRepository([.. _seats]),
                    new FakePositionRepository([.. _positions]),
                    new FakeOrganizationUnitRepository([.. _units]))
                .Handle(
                    new InquireTaskItemCommand(
                        Task.Id, new InquireTaskItemRequest(Task.Version, reason, waitingOn), "corr"),
                    CancellationToken.None);

        public Task<Application.Common.Response<Application.Common.NoContent>> ResumeAsync()
            => TransitionAsync(TaskLifecycle.InProgress);

        public Task<Application.Common.Response<Application.Common.NoContent>> TransitionAsync(
            TaskLifecycle target)
            => new TransitionTaskItemHandler(
                    Tasks, new TaskLifecycleService(), new FakeCurrentUserContext(TaskTestData.Me),
                    new FakeChecklistRunRepository(), new TaskChecklistService(),
                    new FakeWorkflowTransitionGate(), new FakeTaskDependencyRepository(),
                    new FakeTaskTypeRepository(), new FakeTaskNotificationService(),
                    Microsoft.Extensions.Logging.Abstractions.NullLogger<TransitionTaskItemHandler>.Instance)
                .Handle(
                    new TransitionTaskItemCommand(
                        Task.Id, target, new TaskTransitionRequest(Task.Version, null, null), "corr",
                        ActorMayCancelAnyTask: true),
                    CancellationToken.None);

        public async Task<WorkItemProjectionDto> ProjectAsync(params (Guid Id, string Name)[] directory)
        {
            var provider = new TaskWorkItemProvider(
                Tasks,
                new FakePositionAssignmentRepository([.. _seats]),
                new TaskLifecycleService(),
                new TaskAssignmentResolver(),
                new FakeUserDisplayNameResolver(directory),
                new FakeChecklistRunRepository(),
                new FakeTaskApprovalService(),
                new FakeTaskDependencyRepository(),
                new FakeTaskCommentRepository(),
                new FakeTaskTransitionRepository(),
                new FakeTaskPersonalOverlayRepository(),
                new FakeTaskWatcherRepository(),
                TaskActors.PermitAll(),
                new FakePositionRepository([.. _positions]),
                new FakeOrganizationUnitRepository([.. _units]),
                SlaForTests.Real(),
                new FakeTaskFieldDefinitionRepository(), new FakeTaskTypeRepository());

            var items = await provider.GetWorkItemsAsync(
                new WorkItemActor(TaskTestData.Me, IsPlatformActor: true, new HashSet<string>()),
                CancellationToken.None);
            return Assert.Single(items.Where(item => item.Id == Task.Id.ToString()));
        }
    }
}
