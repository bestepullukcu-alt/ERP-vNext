using Diten.Platform.Application.Features.DocumentManagementContract;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// WP-WCN-TASK-REQUEST-01 FIX3 — the write gate's ONE decision (<see cref="TaskWriteGate.Judge"/>), measured as the table
/// on <see cref="TaskWriteKind"/> states it, and its two shapes (one task, many tasks) over the PRODUCTION read rule.
/// The HTTP suites prove the wiring endpoint by endpoint; this file is where the rule itself is pinned cell by cell.
/// </summary>
public sealed class TaskWriteGateTests
{
    private static readonly Guid Me = TaskTestData.Me;

    /// <summary>The relationships a writer can have to a task, each with the read ground the rule gives it.</summary>
    public enum Relation { Unreadable, PersonAsked, Observer, SenderOfAccepted, OrdinaryReader }

    public static TheoryData<TaskWriteKind, Relation, int?, string?> Table => new()
    {
        { TaskWriteKind.Change, Relation.Unreadable, 404, TaskReasonCodes.NotFound },
        { TaskWriteKind.Change, Relation.PersonAsked, 409, TaskReasonCodes.RequestNotAccepted },
        { TaskWriteKind.Change, Relation.Observer, 403, DocumentManagementReasonCodes.PermissionDenied },
        { TaskWriteKind.Change, Relation.SenderOfAccepted, 403, TaskReasonCodes.RequestSenderCannotDirect },
        { TaskWriteKind.Change, Relation.OrdinaryReader, null, null },

        { TaskWriteKind.Comment, Relation.Unreadable, 404, TaskReasonCodes.NotFound },
        { TaskWriteKind.Comment, Relation.PersonAsked, 409, TaskReasonCodes.RequestNotAccepted },
        { TaskWriteKind.Comment, Relation.Observer, null, null },
        { TaskWriteKind.Comment, Relation.SenderOfAccepted, null, null },
        { TaskWriteKind.Comment, Relation.OrdinaryReader, null, null },

        { TaskWriteKind.Administer, Relation.Unreadable, 404, TaskReasonCodes.NotFound },
        { TaskWriteKind.Administer, Relation.PersonAsked, 409, TaskReasonCodes.RequestNotAccepted },
        { TaskWriteKind.Administer, Relation.Observer, 403, DocumentManagementReasonCodes.PermissionDenied },
        { TaskWriteKind.Administer, Relation.SenderOfAccepted, null, null },
        { TaskWriteKind.Administer, Relation.OrdinaryReader, null, null },

        { TaskWriteKind.Personal, Relation.Unreadable, 404, TaskReasonCodes.NotFound },
        { TaskWriteKind.Personal, Relation.PersonAsked, null, null },
        { TaskWriteKind.Personal, Relation.Observer, null, null },
        { TaskWriteKind.Personal, Relation.SenderOfAccepted, null, null },
        { TaskWriteKind.Personal, Relation.OrdinaryReader, null, null }
    };

    /// <summary>MUTATION TARGET (every line of <see cref="TaskWriteGate.Judge"/>). Each kind answers each relationship
    /// exactly as the table says — and no two kinds answer all five alike, so a handler asking the wrong kind is caught.</summary>
    [Theory]
    [MemberData(nameof(Table))]
    public void The_four_kinds_answer_the_relationships_as_the_table_says(
        TaskWriteKind kind, Relation relation, int? status, string? reasonCode)
    {
        var (task, ground) = Case(relation);

        var refusal = TaskWriteGate.Judge(task, ground, kind, Me);

        Assert.Equal((status, reasonCode), (refusal?.StatusCode, refusal?.ReasonCode));
    }

    [Fact]
    public void No_two_kinds_answer_every_relationship_alike()
    {
        var answers = Enum.GetValues<TaskWriteKind>()
            .Select(kind => string.Join(",", Enum.GetValues<Relation>().Select(relation =>
            {
                var (task, ground) = Case(relation);
                return TaskWriteGate.Judge(task, ground, kind, Me)?.StatusCode.ToString() ?? "ok";
            })))
            .ToList();

        Assert.Equal(answers.Count, answers.Distinct().Count());
    }

    /// <summary>MUTATION TARGET (<see cref="TaskWriteGate.AllowedTaskIdsAsync"/> — the batched shape asks the same
    /// <see cref="TaskWriteGate.Judge"/>). Over the production read rule and one task per relationship, the many-task
    /// answer equals the one-task answer for every kind.</summary>
    [Fact]
    public async Task The_one_task_and_the_many_task_shapes_of_the_gate_give_one_answer()
    {
        var watched = NewTask(holder: TaskTestData.Rival, creator: TaskTestData.Other);
        var asking = NewTask(holder: TaskTestData.Rival, creator: TaskTestData.Other);
        asking.Lifecycle = TaskLifecycle.Waiting;
        asking.WaitingOnUserId = Me;
        var requested = NewTask(holder: null, creator: TaskTestData.Other);
        requested.AssignmentTarget = TaskAssignmentTarget.Offered;
        requested.OfferedToUserId = Me;
        requested.IsRequest = true;
        var accepted = NewTask(holder: TaskTestData.Rival, creator: Me);
        accepted.IsRequest = true;
        var mine = NewTask(holder: Me, creator: TaskTestData.Other);
        var hidden = NewTask(holder: TaskTestData.Rival, creator: TaskTestData.Other);
        var all = new[] { watched, asking, requested, accepted, mine, hidden };
        var tasks = new FakeTaskItemRepository(all);
        var watchers = new FakeTaskWatcherRepository();
        await watchers.CreateAsync(new TaskWatcher { TenantId = TaskTestData.Tenant, TaskItemId = watched.Id, UserId = Me });
        var gate = new TaskWriteGate(RealTaskReadRule.Over(tasks, Me, watchers), new FakeCurrentUserContext(Me));

        foreach (var kind in Enum.GetValues<TaskWriteKind>())
        {
            var batched = await gate.AllowedTaskIdsAsync(all, Me, kind, CancellationToken.None);
            var oneByOne = new HashSet<Guid>();
            foreach (var task in all)
            {
                if (await gate.CheckAsync(task, kind, CancellationToken.None) is null)
                {
                    oneByOne.Add(task.Id);
                }
            }

            Assert.Equal(oneByOne.OrderBy(id => id), batched.OrderBy(id => id));
        }

        // Non-vacuity: the observer cases are refused a change in both shapes, and allowed a comment.
        var changeable = await gate.AllowedTaskIdsAsync(all, Me, TaskWriteKind.Change, CancellationToken.None);
        Assert.Equal([mine.Id], changeable.ToList());
        var commentable = await gate.AllowedTaskIdsAsync(all, Me, TaskWriteKind.Comment, CancellationToken.None);
        Assert.Equal(new[] { watched.Id, asking.Id, accepted.Id, mine.Id }.OrderBy(id => id), commentable.OrderBy(id => id));
    }

    private static (TaskItem Task, TaskReadGround Ground) Case(Relation relation)
    {
        switch (relation)
        {
            case Relation.Unreadable:
                return (NewTask(holder: TaskTestData.Rival, creator: TaskTestData.Other), TaskReadGround.None);
            case Relation.PersonAsked:
                var requested = NewTask(holder: null, creator: TaskTestData.Other);
                requested.AssignmentTarget = TaskAssignmentTarget.Offered;
                requested.OfferedToUserId = Me;
                requested.IsRequest = true;
                return (requested, TaskReadGround.Full);
            case Relation.Observer:
                return (NewTask(holder: TaskTestData.Rival, creator: TaskTestData.Other), TaskReadGround.Observer);
            case Relation.SenderOfAccepted:
                var accepted = NewTask(holder: TaskTestData.Rival, creator: Me);
                accepted.IsRequest = true;
                return (accepted, TaskReadGround.Full);
            default:
                return (NewTask(holder: TaskTestData.Rival, creator: TaskTestData.Other), TaskReadGround.Full);
        }
    }

    private static TaskItem NewTask(Guid? holder, Guid creator) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TaskTestData.Tenant,
        Title = "Gate probe",
        Lifecycle = TaskLifecycle.InProgress,
        AssignmentTarget = TaskAssignmentTarget.Person,
        AssigneeUserId = holder,
        CreatedByUserId = creator,
        OrganizationUnitId = Guid.NewGuid(),
        CreatedBy = "fix3"
    };
}

/// <summary>
/// WP-WCN-TASK-REQUEST-01 FIX3 (item 8) — WHY a reader reads, as the read rule now names it. It decides nothing new: the
/// ground is <see cref="TaskReadGround.None"/> exactly when <see cref="ITaskReadAccessPolicy.CanReadAsync"/> says no, one
/// task at a time and in a batch; the watcher and the person a waiting task asks are the only OBSERVER legs, and a full
/// leg anywhere (scope, team, ReadAll) outranks them.
/// </summary>
public sealed class TaskReadGroundTests
{
    private static readonly Guid Me = TaskTestData.Me;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Every_leg_names_its_ground_and_the_ground_never_disagrees_with_CanReadAsync(bool readAll)
    {
        var (policy, tasks) = EveryLeg(readAll);

        var batched = await policy.ReadGroundsAsync(tasks.Values.ToList(), Me, CancellationToken.None);
        foreach (var (leg, task) in tasks)
        {
            var ground = await policy.ReadGroundAsync(task, Me, CancellationToken.None);
            Assert.True((ground != TaskReadGround.None) == await policy.CanReadAsync(task, Me, CancellationToken.None), leg);
            Assert.True(batched.GetValueOrDefault(task.Id) == ground, $"{leg}: batched {batched.GetValueOrDefault(task.Id)} vs {ground}");
        }

        var observers = tasks.Where(t => batched[t.Value.Id] == TaskReadGround.Observer).Select(t => t.Key).OrderBy(k => k).ToList();
        Assert.Equal(readAll ? [] : new List<string> { "askedOf", "watcher" }, observers);
        Assert.Equal(TaskReadGround.Full, batched[tasks["scopeAndWatcher"].Id]);     // a full leg outranks watching
    }

    [Fact]
    public async Task Asked_about_somebody_else_the_ground_comes_from_the_data_legs_alone()
    {
        var (policy, tasks) = EveryLeg(readAll: true);

        // The watcher of "watcher" is Me; asked about Rival (not the caller), scope / team / ReadAll do not answer.
        Assert.Equal(TaskReadGround.None, await policy.ReadGroundAsync(tasks["scope"], TaskTestData.Rival, CancellationToken.None));
        Assert.Equal(TaskReadGround.Full, await policy.ReadGroundAsync(tasks["creator"], Me, CancellationToken.None));
    }

    private static (ITaskReadAccessPolicy Policy, Dictionary<string, TaskItem> Tasks) EveryLeg(bool readAll)
    {
        var covered = new OrganizationUnit
        {
            Id = Guid.NewGuid(), TenantId = TaskTestData.Tenant, Code = "COVERED", Name = "Covered", LegalEntityId = Guid.NewGuid()
        };
        var units = new FakeOrganizationUnitRepository(covered);
        var tasks = new FakeTaskItemRepository();
        var watchers = new FakeTaskWatcherRepository();
        var notifications = new FakeTaskNotificationService();
        var policy = new TaskReadAccessPolicy(
            tasks, watchers, notifications, units,
            new TaskAssignmentScopeResolver(
                new FakeDataScopeResolver(new Diten.Platform.Common.Authorization.EntitlementDataScope(
                    Diten.Platform.Common.Authorization.EntitlementDataScopeKind.OrgUnit, covered.Id, "granted")),
                new FakePositionRepository(), units, new FakeTenantContext(TaskTestData.Tenant), new FakeCurrentUserContext(Me)),
            new FakeTaskTeamResolver(TaskTestData.Rival),
            readAll ? TaskActors.Holding(TaskPermissions.ReadAll) : TaskActors.None(),
            new FakeCurrentUserContext(Me));

        TaskItem New(Guid? holder = null, Guid? creator = null, Guid? unit = null) => new()
        {
            Id = Guid.NewGuid(), TenantId = TaskTestData.Tenant, Title = "Ground probe", Lifecycle = TaskLifecycle.Open,
            AssignmentTarget = TaskAssignmentTarget.Person, AssigneeUserId = holder ?? TaskTestData.Other,
            CreatedByUserId = creator ?? TaskTestData.Other, OrganizationUnitId = unit ?? Guid.NewGuid(), CreatedBy = "fix3"
        };

        var map = new Dictionary<string, TaskItem>
        {
            ["assignee"] = New(holder: Me),
            ["creator"] = New(creator: Me),
            ["pool"] = New(),
            ["watcher"] = New(),
            ["askedOf"] = New(),
            ["scope"] = New(unit: covered.Id),
            ["scopeAndWatcher"] = New(unit: covered.Id),
            ["team"] = New(holder: TaskTestData.Rival),
            ["request"] = New(),
            ["unrelated"] = New()
        };
        notifications.PoolHoldersByTaskId[map["pool"].Id] = [Me];
        foreach (var watched in new[] { "watcher", "scopeAndWatcher" })
        {
            watchers.CreateAsync(new TaskWatcher { TenantId = TaskTestData.Tenant, TaskItemId = map[watched].Id, UserId = Me })
                .GetAwaiter().GetResult();
        }

        map["askedOf"].Lifecycle = TaskLifecycle.Waiting;
        map["askedOf"].WaitingOnUserId = Me;
        map["request"].AssignmentTarget = TaskAssignmentTarget.Offered;
        map["request"].AssigneeUserId = null;
        map["request"].OfferedToUserId = Me;
        map["request"].IsRequest = true;
        return (policy, map);
    }
}
