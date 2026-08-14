using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WC-3 / BL-031 — WHICH queue a pooled item is waiting in.
///
/// <para>The Pool tab's entire question is "which queue is this in", and the projection could not answer it: an
/// item said only <c>assignmentMode: "groupQueue"</c>. The screen filled that silence with a fabricated team name
/// ("Operasyon Kuyruğu") for every pooled item, so genuine CFO-pool work was labelled with a queue that does not
/// exist. That label is gone; this is the field that replaces it with the truth.</para>
/// </summary>
public sealed class TaskPoolIdentityTests
{
    private static readonly Guid CfoPositionId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AccountingPositionId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid EngineerPositionId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid UnitId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task A_pooled_item_names_its_queue()
    {
        var projection = await ProjectSingleAsync(PoolTask(CfoPositionId));

        Assert.Equal("groupQueue", projection.AssignmentMode);
        Assert.NotNull(projection.Pool);
        Assert.Equal(CfoPositionId.ToString(), projection.Pool!.Id);
        // The position's name joined to its unit — "CFO" alone cannot be told apart across facilities, and
        // pooling is exactly where that ambiguity sends work to the wrong place.
        Assert.Equal("CFO — Genel Merkez", projection.Pool.Label!.Text);
        // DISPLAY, not resource: a position name is data someone typed, so a resource key would render as itself.
        Assert.Equal(WorkItemContract.LabelDisplay, projection.Pool.Label.Kind);
    }

    [Fact]
    public async Task Three_queues_stay_three_queues()
    {
        /*
         * BL-031 (b): the fabricated label collapsed every pooled item into ONE group, so a user could not see
         * which queue anything was in. Three real pools must project as three distinct identities.
         */
        var projection = await ProjectAsync(
            [PoolTask(CfoPositionId), PoolTask(AccountingPositionId), PoolTask(EngineerPositionId)]);

        var ids = projection.Select(item => item.Pool!.Id).ToHashSet();
        var labels = projection.Select(item => item.Pool!.Label!.Text).ToHashSet();

        Assert.Equal(3, ids.Count);
        Assert.Equal(
            new HashSet<string> { "CFO — Genel Merkez", "Muhasebe Müdürü — Genel Merkez", "E2E Engineer — Genel Merkez" },
            labels);
    }

    [Fact]
    public async Task Work_that_is_not_pooled_carries_no_queue()
    {
        // A directly-assigned task has no queue. Saying it belongs to one would be inventing a fact — the exact
        // shape of the defect this field exists to end.
        var projection = await ProjectSingleAsync(SelfTask());

        Assert.NotEqual("groupQueue", projection.AssignmentMode);
        Assert.Null(projection.Pool);
    }

    [Fact]
    public async Task A_claimed_pool_task_still_names_the_queue_it_came_from()
    {
        /*
         * Claiming changes WHERE the work is, not HOW it arrived. The resolver turns a claimed pool task into
         * owned + admitted — so it leaves the Pool TAB, which routes on admissionState — but leaves
         * assignmentMode as groupQueue, because "this came through a queue" stays true forever.
         *
         * The pool field therefore has to survive the claim. If it did not, a claimed pool task would be a
         * groupQueue item with no queue, which is precisely the state the contract rule below forbids: the rule
         * keys off assignmentMode, so the two must agree or every claim would produce an invalid item.
         */
        var claimed = PoolTask(CfoPositionId);
        claimed.AssigneeUserId = TaskTestData.Me;
        claimed.Lifecycle = TaskLifecycle.InProgress;

        var projection = await ProjectSingleAsync(claimed);

        Assert.Equal("owned", projection.OwnershipState);
        Assert.Equal("admitted", projection.AdmissionState);
        Assert.Equal("groupQueue", projection.AssignmentMode);
        Assert.NotNull(projection.Pool);
        Assert.Equal("CFO — Genel Merkez", projection.Pool!.Label!.Text);
    }

    [Fact]
    public async Task A_task_moved_out_of_the_pool_stops_claiming_its_old_queue()
    {
        /*
         * A pooled task handed to a PERSON keeps its PoolPositionId on the record — that is where it came from —
         * but its assignment target is no longer a pool, so the resolver stops calling it groupQueue. The pool
         * field must follow the TARGET, not the leftover id.
         *
         * This is load-bearing rather than cosmetic: the contract rejects a pool on non-queued work
         * (POOL_ON_NON_QUEUE_ITEM), and validateItems drops what it cannot validate — so emitting the stale
         * queue here would not merely mislabel the task, it would make it disappear from the surface.
         */
        var reassigned = PoolTask(CfoPositionId);
        reassigned.AssignmentTarget = TaskAssignmentTarget.Person;
        reassigned.AssigneeUserId = TaskTestData.Me;
        reassigned.Lifecycle = TaskLifecycle.InProgress;

        var projection = await ProjectSingleAsync(reassigned);

        Assert.NotEqual("groupQueue", projection.AssignmentMode);
        Assert.Null(projection.Pool);
    }

    // ── The unresolvable position: the third way ─────────────────────────────

    [Fact]
    public async Task An_unreadable_position_leaves_the_queue_unnamed_but_still_identified()
    {
        /*
         * Neither of the two tempting exits is taken. The GUID does NOT become the label — a raw id where a team
         * name belongs is a defect this codebase has already shipped once — and the field is NOT omitted, because
         * the contract requires it for pooled work and validateItems DROPS what it cannot validate, which would
         * make the task vanish from the Pool tab entirely.
         */
        var projection = await ProjectSingleAsync(
            PoolTask(CfoPositionId), positions: [], units: []);

        Assert.NotNull(projection.Pool);
        Assert.Equal(CfoPositionId.ToString(), projection.Pool!.Id);
        Assert.Null(projection.Pool.Label);
    }

    [Fact]
    public async Task A_position_whose_unit_cannot_be_read_is_unnamed_rather_than_half_named()
    {
        // "CFO — ???" is worse than no name: it looks like a real queue in an unknown place.
        var projection = await ProjectSingleAsync(PoolTask(CfoPositionId), units: []);

        Assert.NotNull(projection.Pool);
        Assert.Null(projection.Pool!.Label);
    }

    [Fact]
    public async Task An_archived_position_still_names_its_queue()
    {
        /*
         * Deliberately unlike the assignable-position lookup, which excludes archived and draft positions. That
         * one answers "where MAY this be pooled" — an unusable position must not be offered. This answers "where
         * IS this pooled", and work already sitting in a queue that has since been archived still needs its queue
         * named, or it silently loses its identity.
         */
        var archived = Position(CfoPositionId, "CFO");
        archived.IsArchived = true;

        var projection = await ProjectSingleAsync(PoolTask(CfoPositionId), positions: [archived]);

        Assert.Equal("CFO — Genel Merkez", projection.Pool!.Label!.Text);
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private static async Task<WorkItemProjectionDto> ProjectSingleAsync(
        TaskItem task,
        Position[]? positions = null,
        OrganizationUnit[]? units = null)
        => Assert.Single(await ProjectAsync([task], positions, units));

    private static async Task<IReadOnlyList<WorkItemProjectionDto>> ProjectAsync(
        TaskItem[] tasks,
        Position[]? positions = null,
        OrganizationUnit[]? units = null)
    {
        var provider = new TaskWorkItemProvider(
            new FakeTaskItemRepository(tasks),
            // The actor holds every pool position, so pooled work is visible to them.
            new FakePositionAssignmentRepository(
                Assignment(CfoPositionId), Assignment(AccountingPositionId), Assignment(EngineerPositionId)),
            new TaskLifecycleService(),
            new TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver(),
            new FakeChecklistRunRepository(),
            new FakeTaskApprovalService(),
            new FakeTaskDependencyRepository(),
            new FakeTaskCommentRepository(), new FakeTaskTransitionRepository(), new FakeTaskPersonalOverlayRepository(), new FakeTaskWatcherRepository(), TaskActors.PermitAll(),
            new FakePositionRepository(positions ?? DefaultPositions()),
            new FakeOrganizationUnitRepository(units ?? [Unit()]),
            SlaForTests.Real(), new FakeTaskFieldDefinitionRepository());

        var actor = new WorkItemActor(TaskTestData.Me, IsPlatformActor: false, new HashSet<string>(
            new[] { TaskPermissions.Update, TaskPermissions.Claim, TaskPermissions.Complete },
            StringComparer.OrdinalIgnoreCase));

        return await provider.GetWorkItemsAsync(actor, CancellationToken.None);
    }

    private static Position[] DefaultPositions() =>
    [
        Position(CfoPositionId, "CFO"),
        Position(AccountingPositionId, "Muhasebe Müdürü"),
        Position(EngineerPositionId, "E2E Engineer")
    ];

    private static Position Position(Guid id, string name) => new()
    {
        Id = id,
        TenantId = TaskTestData.Tenant,
        Code = name.ToUpperInvariant().Replace(' ', '-'),
        Name = name,
        OrganizationUnitId = UnitId
    };

    private static OrganizationUnit Unit() => new()
    {
        Id = UnitId,
        TenantId = TaskTestData.Tenant,
        Code = "HQ",
        Name = "Genel Merkez",
        LegalEntityId = Guid.NewGuid()
    };

    private static PositionAssignment Assignment(Guid positionId) => new()
    {
        TenantId = TaskTestData.Tenant,
        PositionId = positionId,
        UserId = TaskTestData.Me,
        EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-1),
        EffectiveTo = null
    };

    private static TaskItem PoolTask(Guid poolPositionId) => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "Havuzda bekleyen iş",
        AssignmentTarget = TaskAssignmentTarget.PositionPool,
        PoolPositionId = poolPositionId,
        AssigneeUserId = null,
        CreatedByUserId = TaskTestData.Rival,
        OrganizationUnitId = UnitId,
        Lifecycle = TaskLifecycle.Open,
        Version = 1
    };

    private static TaskItem SelfTask() => new()
    {
        TenantId = TaskTestData.Tenant,
        Title = "Kendi işim",
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        CreatedByUserId = TaskTestData.Me,
        OrganizationUnitId = UnitId,
        Lifecycle = TaskLifecycle.InProgress,
        Version = 1
    };
}
