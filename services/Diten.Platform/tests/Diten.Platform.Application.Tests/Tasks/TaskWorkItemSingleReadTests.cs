using System.Text.Json;
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
/// BL-414 — a task read BY ID is the same work item the list projects. Not "similar": the serialized item is
/// compared whole.
///
/// <para><b>What could drift.</b> The single read runs the list's own batch, so every container is shared by
/// construction. The one fact it cannot inherit is BL-016's "initiator only", which the list decides by WHICH of
/// its three reads produced a row; the single read restates it as the conditions those reads filter on. The
/// seeded set below crosses each boundary those conditions draw — holding, being offered, having opened, and the
/// precedence between them — so a restatement that disagreed with the reads on any of them turns this red.</para>
/// </summary>
public sealed class TaskWorkItemSingleReadTests
{
    private static readonly Guid Me = TaskTestData.Me;
    private static readonly Guid Rival = TaskTestData.Rival;
    private static readonly Guid Other = TaskTestData.Other;
    private static readonly Guid MyPosition = Guid.Parse("41400000-0000-0000-0000-0000000000b1");
    private static readonly Guid NotMyPosition = Guid.Parse("41400000-0000-0000-0000-0000000000b2");

    [Fact]
    public async Task Every_task_on_the_self_list_is_projected_identically_when_read_by_id()
    {
        TaskItem[] tasks =
        [
            NewTask("held — assigned to me by somebody else", assignee: Me, createdBy: Other),
            NewTask("self-assigned — opened AND held by me", assignee: Me, createdBy: Me),
            NewTask("offered — unclaimed on my position", pool: MyPosition, createdBy: Other),
            NewTask("opened by me AND offered to me — being offered outranks having opened", pool: MyPosition, createdBy: Me),
            NewTask("opened by me, handed to a colleague — initiator only", assignee: Rival, createdBy: Me),
            NewTask("opened by me, pooled on a position I do not hold — initiator only", pool: NotMyPosition, createdBy: Me),
            NewTask("held and finished", assignee: Me, createdBy: Other, lifecycle: TaskLifecycle.Done)
        ];
        var (provider, _) = Build(tasks);

        var list = await provider.GetWorkItemsAsync(Actor(), CancellationToken.None);

        // Non-vacuity: every seeded task reached the list, and BOTH answers to "initiator only" are represented —
        // a comparison over rows that all say the same thing could not catch a predicate that always said it.
        Assert.Equal(tasks.Length, list.Count);
        Assert.Contains(list, item => item.ViewerRelation == WorkItemContract.ViewerRelationInitiator);
        Assert.Contains(list, item => item.ViewerRelation is null);

        foreach (var listed in list)
        {
            var task = tasks.Single(t => t.Id.ToString() == listed.Id);

            var byId = await provider.GetWorkItemAsync(task, Actor(), CancellationToken.None);

            Assert.Equal(Json(listed), Json(byId));
        }
    }

    [Fact]
    public async Task A_task_the_reader_only_watches_is_projected_with_no_holder_act_and_no_initiator_relation()
    {
        var watched = NewTask("a colleague's work I watch", assignee: Rival, createdBy: Other);
        var (provider, watchers) = Build([watched]);
        await watchers.CreateAsync(new TaskWatcher { TenantId = TaskTestData.Tenant, TaskItemId = watched.Id, UserId = Me });

        // Non-vacuity: the list does not hold it — this is exactly the case the read by id exists for.
        Assert.Empty(await provider.GetWorkItemsAsync(Actor(), CancellationToken.None));

        var item = await provider.GetWorkItemAsync(watched, Actor(), CancellationToken.None);

        Assert.Equal(watched.Id.ToString(), item.Id);
        Assert.Empty(item.Actions);
        Assert.Null(item.PrimaryActionCode);
        Assert.Null(item.ViewerRelation);
    }

    [Fact]
    public async Task A_finished_task_I_opened_is_not_called_initiator_only_by_id_either()
    {
        // The list's creator read excludes finished work, so the list never states this relation for a closed task;
        // the read by id must not state it either.
        var closed = NewTask("opened by me, finished by a colleague", assignee: Rival, createdBy: Me, lifecycle: TaskLifecycle.Done);
        var (provider, _) = Build([closed]);

        Assert.Empty(await provider.GetWorkItemsAsync(Actor(), CancellationToken.None));

        var item = await provider.GetWorkItemAsync(closed, Actor(), CancellationToken.None);

        Assert.Null(item.ViewerRelation);
    }

    private static string Json(WorkItemProjectionDto item) => JsonSerializer.Serialize(item);

    /// <summary>An ordinary reader: the action keys, not a platform actor, so the projected actions mean something.</summary>
    private static WorkItemActor Actor() => new(
        Me,
        IsPlatformActor: false,
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            TaskPermissions.Update, TaskPermissions.Claim, TaskPermissions.Complete,
            TaskPermissions.Cancel, TaskPermissions.Assign
        });

    private static (TaskWorkItemProvider Provider, FakeTaskWatcherRepository Watchers) Build(TaskItem[] tasks)
    {
        var watchers = new FakeTaskWatcherRepository();
        var provider = new TaskWorkItemProvider(
            new FakeTaskItemRepository(tasks),
            new FakePositionAssignmentRepository(new PositionAssignment
            {
                TenantId = TaskTestData.Tenant,
                PositionId = MyPosition,
                UserId = Me,
                EffectiveFrom = DateTimeOffset.UtcNow.AddDays(-30)
            }),
            new TaskLifecycleService(),
            new TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver(),
            new FakeChecklistRunRepository(),
            new FakeTaskApprovalService(),
            new FakeTaskDependencyRepository(),
            new FakeTaskCommentRepository(),
            new FakeTaskTransitionRepository(),
            new FakeTaskPersonalOverlayRepository(),
            watchers,
            TaskActors.PermitAll(),
            new FakePositionRepository(),
            new FakeOrganizationUnitRepository(),
            SlaForTests.Real(),
            new FakeTaskFieldDefinitionRepository(),
            new FakeTaskTypeRepository());
        return (provider, watchers);
    }

    private static TaskItem NewTask(
        string title,
        Guid? assignee = null,
        Guid? createdBy = null,
        Guid? pool = null,
        TaskLifecycle lifecycle = TaskLifecycle.Open) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TaskTestData.Tenant,
        Title = title,
        Lifecycle = lifecycle,
        AssignmentTarget = pool is null ? TaskAssignmentTarget.Person : TaskAssignmentTarget.PositionPool,
        AssigneeUserId = assignee,
        PoolPositionId = pool,
        CreatedByUserId = createdBy,
        OrganizationUnitId = Guid.NewGuid(),
        CreatedBy = "tester",
        DelegationAllowed = true,
        Version = 1
    };
}
