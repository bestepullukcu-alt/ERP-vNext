using System.Text.Json;
using Diten.Platform.Application.Features.Meetings.RecordLinks;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Meetings;

/// <summary>
/// BL-379 — "sözleşme uyumu üretim koduyla ölçülür", not by a unit test that substitutes the validator or the
/// provider. This runs the REAL <see cref="TaskWorkItemProvider"/> across the full
/// {holder, requester, third party} × {open, closed} × {no meeting, meeting linked} × {Update yes, no} matrix,
/// serializes each cell with the SAME <see cref="JsonSerializerOptions"/> the runtime actually uses
/// (<see cref="Tasks.TaskJsonContractTests"/>'s own documented choice), and compares the result to a golden
/// fixture BOTH this test and <c>tests/task-provider-contract-conformance.test.js</c> read — the JS side runs
/// every cell through the REAL <c>validateWorkItem</c>, so a change here that violates the WC-1 contract fails
/// on the JS side even if this file's own comparison were to somehow miss it.
/// </summary>
public sealed class TaskProviderContractGoldenTests
{
    private const string RegenerateEnvVar = "REGENERATE_WCN_FIXTURE";
    private static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static readonly Guid TaskId = Guid.Parse("d1d1d1d1-0000-0000-0000-0000000000d1");
    private static readonly Guid MeetingId = Guid.Parse("d2d2d2d2-0000-0000-0000-0000000000d2");
    private static readonly Guid ActorId = Guid.Parse("d3d3d3d3-0000-0000-0000-0000000000d3");
    private static readonly Guid OtherId = Guid.Parse("d4d4d4d4-0000-0000-0000-0000000000d4");
    private static readonly Guid TeamMemberId = Guid.Parse("d5d5d5d5-0000-0000-0000-0000000000d5");
    private static readonly DateTimeOffset StartAt = new(2026, 11, 1, 9, 0, 0, TimeSpan.Zero);
    // A GOLDEN fixture cannot embed DateTimeOffset.UtcNow — two different processes (the one that generated it,
    // the one comparing against it) would never agree, and the test would look "stale" forever.
    private static readonly DateTimeOffset ClosedAt = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    private static readonly string[] Relationships = ["holder", "requester", "thirdParty"];
    private static readonly string[] Lifecycles = ["open", "closed"];
    private static readonly string[] Meetings = ["none", "linked"];
    private static readonly string[] Permissions = ["yes", "no"];

    [Fact]
    public async Task Every_cell_of_the_matrix_matches_the_golden_fixture()
    {
        var actual = new SortedDictionary<string, WorkItemProjectionDto?>(StringComparer.Ordinal);
        foreach (var cell in AllCells())
        {
            actual[CellKey(cell)] = await ProjectCellAsync(cell);
        }

        var path = FixturePath();
        if (Environment.GetEnvironmentVariable(RegenerateEnvVar) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(actual, WireOptions));
            return; // regeneration mode: writing the new golden file IS the assertion.
        }

        if (!File.Exists(path))
        {
            Assert.Fail(
                $"No golden fixture at '{path}'. Generate it with:\n" +
                $"{RegenerateEnvVar}=1 dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests " +
                "--filter FullyQualifiedName~TaskProviderContractGoldenTests");
        }

        var golden = JsonSerializer.Deserialize<SortedDictionary<string, JsonElement>>(
            File.ReadAllText(path), WireOptions)!;

        var actualNormalized = actual.ToDictionary(
            pair => pair.Key,
            pair => JsonDocument.Parse(JsonSerializer.Serialize(pair.Value, WireOptions)).RootElement,
            StringComparer.Ordinal);

        var mismatched = actualNormalized.Keys
            .Union(golden.Keys)
            .Where(key => !golden.TryGetValue(key, out var expected)
                || !actualNormalized.TryGetValue(key, out var got)
                || !JsonElementDeepEquals(expected, got))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            mismatched.Count == 0,
            "Golden fixture is stale for cell(s): " + string.Join(", ", mismatched) + ". Regenerate with:\n" +
            $"{RegenerateEnvVar}=1 dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests " +
            "--filter FullyQualifiedName~TaskProviderContractGoldenTests");
    }

    private static IEnumerable<(string Relationship, string Lifecycle, string Meeting, string Permission)> AllCells()
    {
        foreach (var relationship in Relationships)
        foreach (var lifecycle in Lifecycles)
        foreach (var meeting in Meetings)
        foreach (var permission in Permissions)
        {
            yield return (relationship, lifecycle, meeting, permission);
        }
    }

    private static string CellKey((string Relationship, string Lifecycle, string Meeting, string Permission) cell) =>
        $"{cell.Relationship}_{cell.Lifecycle}_{cell.Meeting}_{cell.Permission}";

    private static async Task<WorkItemProjectionDto?> ProjectCellAsync(
        (string Relationship, string Lifecycle, string Meeting, string Permission) cell)
    {
        var hasUpdate = cell.Permission == "yes";
        var isClosed = cell.Lifecycle == "closed";
        var hasMeeting = cell.Meeting == "linked";

        var task = new TaskItem
        {
            Id = TaskId,
            TenantId = TaskTestData.Tenant,
            Title = "Golden matrix task",
            AssignmentTarget = TaskAssignmentTarget.Person,
            OrganizationUnitId = Guid.NewGuid(),
            Lifecycle = isClosed ? TaskLifecycle.Done : TaskLifecycle.Open,
            CompletedAt = isClosed ? ClosedAt : null
        };

        ITaskTeamResolver? teamResolver = null;
        WorkItemActor actor;
        switch (cell.Relationship)
        {
            case "holder":
                task.AssigneeUserId = ActorId;
                task.CreatedByUserId = OtherId;
                actor = MakeActor(ActorId, hasUpdate, WorkItemScope.Self);
                break;
            case "requester":
                task.AssigneeUserId = OtherId;
                task.CreatedByUserId = ActorId;
                actor = MakeActor(ActorId, hasUpdate, WorkItemScope.Self);
                break;
            default: // thirdParty — a manager reading their TEAM's board (BL-023), the real path a viewer with
                     // no holder/requester relationship to a task sees it through at all (Self-scope reads are
                     // keyed by the actor's own assignee/creator ids and never surface someone else's task).
                task.AssigneeUserId = TeamMemberId;
                task.CreatedByUserId = OtherId;
                actor = MakeActor(ActorId, hasUpdate, WorkItemScope.Team);
                teamResolver = new FixedTeamResolver(TeamMemberId);
                break;
        }

        var links = new List<RecordLink>();
        var meetingSeeds = new List<Meeting>();
        if (hasMeeting)
        {
            links.Add(new RecordLink
            {
                TenantId = TaskTestData.Tenant,
                SourceModuleCode = RecordLinkModuleCodes.Meetings,
                SourceRecordId = MeetingId,
                TargetModuleCode = RecordLinkModuleCodes.Tasks,
                TargetRecordId = TaskId,
                LinkType = RecordLinkTypes.ReviewMeeting,
                CreatedByUserId = ActorId,
                CreatedBy = "test"
            });
            meetingSeeds.Add(new Meeting
            {
                Id = MeetingId,
                TenantId = TaskTestData.Tenant,
                Title = "Review",
                MeetingTypeId = Guid.NewGuid(),
                StartAt = StartAt,
                EndAt = StartAt.AddHours(1),
                OrganizerUserId = ActorId,
                IdempotencyKey = "irrelevant",
                CreatedBy = "test"
            });
        }

        var meetingRepository = new FakeMeetingRepository { Tenant = TaskTestData.Tenant };
        foreach (var meeting in meetingSeeds)
        {
            meetingRepository.Seed(meeting);
        }

        var provider = new TaskWorkItemProvider(
            new FakeTaskItemRepository([task]),
            new FakePositionAssignmentRepository(),
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
            new FakePositionRepository(),
            new FakeOrganizationUnitRepository(),
            SlaForTests.Real(),
            new FakeTaskFieldDefinitionRepository(),
            new FakeTaskTypeRepository(),
            teamResolver: teamResolver,
            recordLinks: new RecordLinkService(
                new FakeRecordLinkRepository([.. links]),
                new FakeTenantContext(TaskTestData.Tenant),
                new FakeCurrentUserContext(ActorId)),
            relatedRecordResolvers: new FakeRelatedRecordResolverRegistry([]),
            logger: null,
            meetingRepository: meetingRepository);

        var items = await provider.GetWorkItemsAsync(actor, CancellationToken.None);
        return items.FirstOrDefault(item => item.Id == TaskId.ToString());
    }

    /// <summary>Grants everything EXCEPT Update unconditionally, so the only thing that changes across the
    /// "Update yes/no" dimension of the matrix is the one action this WP is about — not a side effect on
    /// every other action's own PermissionDenied state.</summary>
    private static WorkItemActor MakeActor(Guid userId, bool hasUpdate, WorkItemScope scope)
    {
        var granted = new HashSet<string>
        {
            TaskPermissions.Read, TaskPermissions.Create, TaskPermissions.Assign,
            TaskPermissions.Claim, TaskPermissions.Complete, TaskPermissions.Cancel
        };
        if (hasUpdate)
        {
            granted.Add(TaskPermissions.Update);
        }

        return new WorkItemActor(userId, IsPlatformActor: false, granted) { Scope = scope };
    }

    private static string FixturePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && dir.Name != "ERP-vNext")
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "frontend", "Diten.Web", "tests", "fixtures", "task-provider-review-meeting-matrix.json");
    }

    private static bool JsonElementDeepEquals(JsonElement a, JsonElement b) =>
        JsonSerializer.Serialize(a) == JsonSerializer.Serialize(b);

    private sealed class FixedTeamResolver : ITaskTeamResolver
    {
        private readonly TaskTeamScope _scope;
        public FixedTeamResolver(params Guid[] userIds) => _scope = new TaskTeamScope(true, userIds);
        public Task<TaskTeamScope> ResolveTeamAsync(CancellationToken ct) => Task.FromResult(_scope);
    }
}
