using Diten.Platform.Application.Common;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Services;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Application.Features.Tasks.Handlers.CommandHandlers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Enums.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// MOD-0024 — the organization unit is mandatory on every task (pack §12 K6) but the user never picks one, so the
/// server resolves it. A person holding no position (administrators, new joiners) previously could not create ANY
/// task: creation failed with ORGANIZATION_UNIT_UNRESOLVED. These cover the graded fallback.
/// </summary>
public sealed class CreateTaskItemOrganizationUnitTests
{
    private static readonly Guid PositionUnitId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid RootUnitId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid HqUnitId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid PositionId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    // ── Tier 1: the assignee's own position ───────────────────────────────────

    [Fact]
    public async Task A_user_holding_a_position_gets_that_positions_unit()
    {
        var tasks = new FakeTaskItemRepository();
        var handler = Handler(
            tasks,
            units: new[] { Unit(PositionUnitId, "B-UNIT"), Unit(RootUnitId, "A-ROOT") },
            positions: new[] { ActivePosition(PositionUnitId) },
            positionAssignments: new[] { Holder(TaskTestData.Me) });

        var result = await handler.Handle(SelfTask(), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        // The position's unit wins over the root, even though the root sorts first by code.
        Assert.Equal(PositionUnitId, tasks.Items.Single().OrganizationUnitId);
    }

    // ── Tier 2: the tenant root, when the person holds no position ────────────

    [Fact]
    public async Task A_user_with_no_position_falls_back_to_the_tenant_root_unit()
    {
        var tasks = new FakeTaskItemRepository();
        var handler = Handler(
            tasks,
            units: new[] { Unit(RootUnitId, "A-ROOT"), Child(PositionUnitId, "B-CHILD", RootUnitId) },
            positions: Array.Empty<Position>(),
            positionAssignments: Array.Empty<PositionAssignment>());

        var result = await handler.Handle(SelfTask(), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(RootUnitId, tasks.Items.Single().OrganizationUnitId);
    }

    [Fact]
    public async Task An_expired_assignment_does_not_count_as_held()
    {
        var tasks = new FakeTaskItemRepository();
        var expired = Holder(TaskTestData.Me);
        expired.EffectiveTo = DateTimeOffset.UtcNow.AddDays(-1);   // half-open interval: already over

        var handler = Handler(
            tasks,
            units: new[] { Unit(RootUnitId, "A-ROOT"), Child(PositionUnitId, "B-CHILD", RootUnitId) },
            positions: new[] { ActivePosition(PositionUnitId) },
            positionAssignments: new[] { expired });

        var result = await handler.Handle(SelfTask(), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(RootUnitId, tasks.Items.Single().OrganizationUnitId);
    }

    [Fact]
    public async Task A_cancelled_assignment_does_not_count_as_held()
    {
        var tasks = new FakeTaskItemRepository();
        var cancelled = Holder(TaskTestData.Me);
        cancelled.IsCancelled = true;

        var handler = Handler(
            tasks,
            units: new[] { Unit(RootUnitId, "A-ROOT") },
            positions: new[] { ActivePosition(PositionUnitId) },
            positionAssignments: new[] { cancelled });

        var result = await handler.Handle(SelfTask(), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(RootUnitId, tasks.Items.Single().OrganizationUnitId);
    }

    [Fact]
    public async Task The_root_choice_is_deterministic_when_a_tenant_has_several_roots()
    {
        // Several legal entities each own a root. HQ wins; otherwise the lowest code, so the same data always
        // yields the same unit regardless of storage order.
        var tasks = new FakeTaskItemRepository();
        var hq = Unit(HqUnitId, "Z-HQ");
        hq.OrgUnitType = OrgUnitType.HQ;

        var handler = Handler(
            tasks,
            units: new[] { Unit(RootUnitId, "A-ROOT"), hq },
            positions: Array.Empty<Position>(),
            positionAssignments: Array.Empty<PositionAssignment>());

        var result = await handler.Handle(SelfTask(), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(HqUnitId, tasks.Items.Single().OrganizationUnitId);
    }

    [Fact]
    public async Task Without_an_hq_the_lowest_code_root_wins_regardless_of_order()
    {
        var first = new FakeTaskItemRepository();
        var second = new FakeTaskItemRepository();
        var a = Unit(RootUnitId, "A-ROOT");
        var b = Unit(HqUnitId, "B-ROOT");

        await Handler(first, new[] { a, b }, Array.Empty<Position>(), Array.Empty<PositionAssignment>())
            .Handle(SelfTask(), CancellationToken.None);
        await Handler(second, new[] { b, a }, Array.Empty<Position>(), Array.Empty<PositionAssignment>())
            .Handle(SelfTask(), CancellationToken.None);

        Assert.Equal(RootUnitId, first.Items.Single().OrganizationUnitId);
        Assert.Equal(RootUnitId, second.Items.Single().OrganizationUnitId);
    }

    [Fact]
    public async Task An_archived_or_inactive_root_is_not_used()
    {
        var tasks = new FakeTaskItemRepository();
        var archived = Unit(HqUnitId, "A-ARCHIVED");
        archived.IsArchived = true;
        var inactive = Unit(Guid.Parse("55555555-5555-5555-5555-555555555555"), "B-INACTIVE");
        inactive.Status = OrgUnitStatus.Inactive;
        var usable = Unit(RootUnitId, "C-USABLE");

        var handler = Handler(
            tasks,
            units: new[] { archived, inactive, usable },
            positions: Array.Empty<Position>(),
            positionAssignments: Array.Empty<PositionAssignment>());

        var result = await handler.Handle(SelfTask(), CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(RootUnitId, tasks.Items.Single().OrganizationUnitId);
    }

    // ── Tier 3: nothing to resolve → a controlled, reason-coded failure ───────

    [Fact]
    public async Task With_no_position_and_no_root_the_request_fails_with_the_reason_code()
    {
        var tasks = new FakeTaskItemRepository();
        // Only a child unit exists — no root at all.
        var handler = Handler(
            tasks,
            units: new[] { Child(PositionUnitId, "B-CHILD", RootUnitId) },
            positions: Array.Empty<Position>(),
            positionAssignments: Array.Empty<PositionAssignment>());

        var result = await handler.Handle(SelfTask(), CancellationToken.None);

        Assert.False(result.IsSuccessful);
        Assert.Equal(TaskReasonCodes.OrganizationUnitUnresolved, result.ReasonCode);
        Assert.Empty(tasks.Items);
    }

    // ── Pool tasks: the unit comes from the position, no fallback involved ────

    [Fact]
    public async Task A_pool_task_takes_the_units_from_its_position_not_the_root()
    {
        var tasks = new FakeTaskItemRepository();
        var handler = Handler(
            tasks,
            units: new[] { Unit(PositionUnitId, "B-UNIT"), Unit(RootUnitId, "A-ROOT") },
            positions: new[] { ActivePosition(PositionUnitId) },
            // Nobody holds the position; a pool task does not need a holder.
            positionAssignments: Array.Empty<PositionAssignment>());

        var command = new CreateTaskItemCommand(
            Request(TaskAssignmentTarget.PositionPool, poolPositionId: PositionId), "corr");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(PositionUnitId, tasks.Items.Single().OrganizationUnitId);
    }

    // ── An explicit request value still wins over everything ─────────────────

    [Fact]
    public async Task An_explicitly_supplied_unit_is_honoured()
    {
        var tasks = new FakeTaskItemRepository();
        var handler = Handler(
            tasks,
            units: new[] { Unit(RootUnitId, "A-ROOT"), Unit(PositionUnitId, "B-UNIT") },
            positions: new[] { ActivePosition(PositionUnitId) },
            positionAssignments: new[] { Holder(TaskTestData.Me) });

        var command = new CreateTaskItemCommand(
            Request(TaskAssignmentTarget.SelfAssigned, organizationUnitId: RootUnitId), "corr");

        var result = await handler.Handle(command, CancellationToken.None);

        Assert.True(result.IsSuccessful);
        Assert.Equal(RootUnitId, tasks.Items.Single().OrganizationUnitId);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static CreateTaskItemCommand SelfTask()
        => new(Request(TaskAssignmentTarget.SelfAssigned), "corr");

    private static CreateTaskItemRequest Request(
        TaskAssignmentTarget target,
        Guid? poolPositionId = null,
        Guid? organizationUnitId = null)
        => new(
            Title: "Prepare filing",
            Description: null,
            Priority: TaskPriority.Medium,
            AssignmentTarget: target,
            AssigneeUserId: null,
            PoolPositionId: poolPositionId,
            OrganizationUnitId: organizationUnitId,
            DueAt: DateTimeOffset.UtcNow.AddDays(7),
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
            Watchers: null);

    private static CreateTaskItemHandler Handler(
        FakeTaskItemRepository tasks,
        IReadOnlyList<OrganizationUnit> units,
        IReadOnlyList<Position> positions,
        IReadOnlyList<PositionAssignment> positionAssignments)
        => new(
            tasks,
            new FakeTaskAssignmentRepository(),
            new FakeTaskWatcherRepository(),
            new FakePositionRepository(positions.ToArray()),
            new FakeOrganizationUnitRepository(units.ToArray()),
            new FakePositionAssignmentRepository(positionAssignments.ToArray()),
            new TaskFieldDefinitionService(new FakeTaskFieldDefinitionRepository()),
            new TaskLifecycleService(),
            new FakeTaskApprovalService(),
            new FakeChecklistTemplateRepository(),
            new FakeChecklistRunRepository(),
            new TaskChecklistService(),
            new NoOpNotificationDispatchAdapter(),
            new FakeCurrentUserContext(TaskTestData.Me),
            new FakeTenantContext(TaskTestData.Tenant),
            NullLogger<CreateTaskItemHandler>.Instance);

    private static OrganizationUnit Unit(Guid id, string code) => new()
    {
        Id = id,
        TenantId = TaskTestData.Tenant,
        Code = code,
        Name = code,
        LegalEntityId = Guid.NewGuid(),
        ParentOrganizationUnitId = null,
        Status = OrgUnitStatus.Active
    };

    private static OrganizationUnit Child(Guid id, string code, Guid parentId)
    {
        var unit = Unit(id, code);
        unit.ParentOrganizationUnitId = parentId;
        return unit;
    }

    private static Position ActivePosition(Guid unitId) => new()
    {
        Id = PositionId,
        TenantId = TaskTestData.Tenant,
        Code = "QA-1",
        Name = "QA Specialist",
        OrganizationUnitId = unitId,
        Status = PositionStatus.Active
    };

    private static PositionAssignment Holder(Guid userId) => new()
    {
        TenantId = TaskTestData.Tenant,
        PositionId = PositionId,
        UserId = userId,
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-30),
        EffectiveTo = null
    };
}

/// <summary>Creation must never depend on mail going out, so the adapter is a no-op here.</summary>
internal sealed class NoOpNotificationDispatchAdapter : INotificationEventDispatchAdapter
{
    public Task<Response<NotificationDispatchDto>> DispatchByEventCodeAsync(
        NotificationEventDispatchRequest request, CancellationToken ct = default)
        => Task.FromResult(Response<NotificationDispatchDto>.Success());
}
