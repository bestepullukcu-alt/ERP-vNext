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
/// `DelegationAllowed` DECIDES something now (2026-08-23).
///
/// <para>It has been collected by the create form since Phase 1 and was asked NOWHERE: a task explicitly marked
/// "may not be delegated" could be handed to anybody, and nothing said otherwise. This was not a missing screen
/// — it was a rule that existed only as a stored value.</para>
///
/// <para>The order matters and is asserted here: the SERVER refuses first, and the projection explains second.
/// A disabled button is a courtesy; a client posting straight to the route must meet the same answer.</para>
/// </summary>
public sealed class TaskDelegationPolicyTests
{
    /// <summary>
    /// MUTATION TARGET (the server rule). The front end cannot be the lock — the same sentence this module has
    /// had to write three times (cancel authority, dependencies, subtasks), each after a caller posted directly.
    /// </summary>
    [Fact]
    public async Task A_task_marked_not_delegable_REFUSES_the_write()
    {
        var fixture = new Fixture(delegationAllowed: false);

        var response = await fixture.ReassignAsync();

        Assert.Equal(409, response.StatusCode);
        Assert.Equal(TaskReasonCodes.DelegationNotAllowed, response.ReasonCode);
        // And nothing moved: the holder is who it was.
        Assert.Equal(TaskTestData.Me, fixture.Task.AssigneeUserId);
    }

    [Fact]
    public async Task A_delegable_task_still_reassigns()
    {
        // Non-vacuity: refusing everybody would pass the test above and break the feature.
        var fixture = new Fixture(delegationAllowed: true);

        var response = await fixture.ReassignAsync();

        Assert.Equal(204, response.StatusCode);
        Assert.Equal(TaskTestData.Other, fixture.Task.AssigneeUserId);
    }

    /// <summary>
    /// The policy is checked BEFORE "are you the holder". "Nobody may delegate this" outranks "you may not
    /// delegate it" — answering the second to a bystander would send them looking for an authority that would
    /// never help.
    /// </summary>
    [Fact]
    public async Task The_policy_answers_before_the_who_are_you_check()
    {
        var fixture = new Fixture(delegationAllowed: false);

        var response = await fixture.ReassignAsync(actingAs: TaskTestData.Rival);

        Assert.Equal(TaskReasonCodes.DelegationNotAllowed, response.ReasonCode);
        Assert.NotEqual(TaskReasonCodes.ReassignNotPermitted, response.ReasonCode);
    }

    // ── The projection explains it ───────────────────────────────────────────

    /// <summary>
    /// DISABLED WITH A REASON, never withheld. This card's rule is that an action whose reason cannot be stated
    /// is not drawn — and here the reason is plain, so the button is drawn, greyed, and explains itself.
    /// </summary>
    [Fact]
    public async Task The_action_is_offered_DISABLED_with_a_reason_a_reader_can_act_on()
    {
        var fixture = new Fixture(delegationAllowed: false);

        var reassign = Assert.Single(
            (await fixture.ProjectAsync()).Actions.Where(a => a.Code == "reassign"));

        Assert.False(reassign.Enabled);
        Assert.Equal(TaskReasonCodes.DelegationNotAllowed, reassign.DisabledReasonCode);
        // The SAME key shape as its five siblings — a resource key the shell translates, not a server sentence.
        Assert.Equal("resource", reassign.DisabledReason!.Kind);
        Assert.Equal("WorkAggregation_ActionDisabled_DelegationNotAllowed", reassign.DisabledReason.Key);
    }

    [Fact]
    public async Task A_delegable_task_offers_it_enabled()
    {
        var fixture = new Fixture(delegationAllowed: true);

        var reassign = Assert.Single(
            (await fixture.ProjectAsync()).Actions.Where(a => a.Code == "reassign"));

        Assert.True(reassign.Enabled);
        Assert.Null(reassign.DisabledReasonCode);
    }

    private sealed class Fixture
    {
        private readonly IReadOnlyList<Position> _positions;
        private readonly IReadOnlyList<OrganizationUnit> _units;
        private readonly IReadOnlyList<PositionAssignment> _seats;
        private readonly FakeTaskItemRepository _tasks;

        public Fixture(bool delegationAllowed)
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
                AssignmentTarget = TaskAssignmentTarget.Person,
                AssigneeUserId = TaskTestData.Me,
                // A SEPARATE requester, so the holder/requester split is real rather than collapsed.
                CreatedByUserId = TaskTestData.Rival,
                OrganizationUnitId = unit.Id,
                Lifecycle = TaskLifecycle.InProgress,
                DelegationAllowed = delegationAllowed,
                Version = 1
            };
            _tasks = new FakeTaskItemRepository(Task);
        }

        public TaskItem Task { get; }

        public Task<Application.Common.Response<Application.Common.NoContent>> ReassignAsync(Guid? actingAs = null)
            => new ReassignTaskItemHandler(
                    _tasks,
                    new FakeTaskAssignmentRepository(),
                    new FakePositionAssignmentRepository([.. _seats]),
                    new FakePositionRepository([.. _positions]),
                    new FakeOrganizationUnitRepository([.. _units]),
                    new FakeCurrentUserContext(actingAs ?? TaskTestData.Me),
                    new FakeTenantContext(TaskTestData.Tenant))
                .Handle(
                    new ReassignTaskItemCommand(
                        Task.Id,
                        new ReassignTaskItemRequest(Task.Version, TaskTestData.Other, "Devrediyorum."),
                        "corr"),
                    CancellationToken.None);

        public async Task<WorkItemProjectionDto> ProjectAsync()
        {
            var provider = new TaskWorkItemProvider(
                _tasks,
                new FakePositionAssignmentRepository([.. _seats]),
                new TaskLifecycleService(),
                new TaskAssignmentResolver(),
                new FakeUserDisplayNameResolver(),
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
            return Assert.Single(items.Where(i => i.Id == Task.Id.ToString()));
        }
    }
}
