using System.Text.Json;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Xunit;

namespace Diten.Platform.Application.Tests.Tasks;

/// <summary>
/// BL-439 — the contract half of "Bilgi bekle", the same golden-file pair the meeting invite has
/// (<c>MeetingInviteProviderContractGoldenTests</c>).
///
/// <para>The REAL <see cref="TaskWorkItemProvider"/> projects three cells — the QUESTION as its addressee sees it,
/// with and without the read key, and the HOLDER's task once the answer is in — serialized with the runtime's JSON
/// options into <c>frontend/Diten.Web/tests/fixtures/task-inquiry-provider-projection.json</c>, which
/// <c>tests/task-inquiry-provider-contract-conformance.test.js</c> runs through the REAL <c>validateWorkItem</c>.
/// Without it, the new <c>inquiry</c> intent and the <c>inquiryAnswer</c> block would reach the contract only in a
/// shape somebody typed by hand.</para>
///
/// <para>Every value is pinned: ids, versions, the due date (2099, so the SLA state never drifts) and the history's
/// timestamps.</para>
/// </summary>
public sealed class TaskInquiryProviderContractGoldenTests
{
    private const string RegenerateEnvVar = "REGENERATE_WCN_FIXTURE";
    private static readonly JsonSerializerOptions WireOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    private static readonly Guid TaskId = Guid.Parse("43943943-0000-4000-8000-0000000000a0");
    private static readonly Guid UnitId = Guid.Parse("43943943-0000-4000-8000-0000000000b0");
    private static readonly Guid Holder = Guid.Parse("43943943-0000-4000-8000-0000000000c1");
    private static readonly Guid Asked = Guid.Parse("43943943-0000-4000-8000-0000000000c2");
    private static readonly DateTimeOffset DueAt = new(2099, 3, 1, 17, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CreatedAt = new(2026, 9, 20, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Every_cell_of_the_inquiry_matrix_matches_the_golden_fixture()
    {
        var actual = new SortedDictionary<string, WorkItemProjectionDto?>(StringComparer.Ordinal)
        {
            ["asked_read"] = await ProjectAsync(Waiting(), Asked, hasRead: true),
            ["asked_none"] = await ProjectAsync(Waiting(), Asked, hasRead: false),
            ["holder_answered"] = await ProjectAsync(Answered(), Holder, hasRead: true)
        };

        Assert.All(actual, pair => Assert.True(pair.Value is not null, $"{pair.Key}: the provider emitted no item"));
        Assert.Equal(WorkItemContract.IntentInquiry, actual["asked_read"]!.WorkIntent);
        Assert.NotNull(actual["holder_answered"]!.InquiryAnswer);

        var path = FixturePath();
        if (Environment.GetEnvironmentVariable(RegenerateEnvVar) == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(actual, WireOptions));
            return;
        }

        Assert.True(
            File.Exists(path),
            $"No golden fixture at '{path}'. Generate it with:\n{RegenerateEnvVar}=1 dotnet test " +
            "services/Diten.Platform/tests/Diten.Platform.Application.Tests --filter FullyQualifiedName~TaskInquiryProviderContractGoldenTests");

        var golden = JsonSerializer.Deserialize<SortedDictionary<string, JsonElement>>(File.ReadAllText(path), WireOptions)!;
        var mismatched = actual.Keys.Union(golden.Keys)
            .Where(key => !golden.TryGetValue(key, out var expected)
                || !actual.TryGetValue(key, out var got)
                || JsonSerializer.Serialize(expected) != JsonSerializer.Serialize(
                    JsonDocument.Parse(JsonSerializer.Serialize(got, WireOptions)).RootElement))
            .OrderBy(key => key, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            mismatched.Count == 0,
            "Golden fixture is stale for cell(s): " + string.Join(", ", mismatched) + ". Regenerate with:\n" +
            $"{RegenerateEnvVar}=1 dotnet test services/Diten.Platform/tests/Diten.Platform.Application.Tests " +
            "--filter FullyQualifiedName~TaskInquiryProviderContractGoldenTests");
    }

    private static (TaskItem Task, TaskTransition[] History) Waiting()
    {
        var task = BaseTask();
        task.Lifecycle = TaskLifecycle.Waiting;
        task.WaitingReason = "Lot 42 sertifikası ne zaman gelir?";
        task.WaitingOnUserId = Asked;
        return (task, [Entry(1, TaskTransitionKind.Created, TaskLifecycle.InProgress, TaskLifecycle.InProgress, Holder, null),
            Entry(2, TaskTransitionKind.Waiting, TaskLifecycle.InProgress, TaskLifecycle.Waiting, Holder, task.WaitingReason)]);
    }

    private static (TaskItem Task, TaskTransition[] History) Answered()
    {
        var task = BaseTask();
        return (task, [Entry(1, TaskTransitionKind.Created, TaskLifecycle.InProgress, TaskLifecycle.InProgress, Holder, null),
            Entry(2, TaskTransitionKind.Waiting, TaskLifecycle.InProgress, TaskLifecycle.Waiting, Holder, "Lot 42 sertifikası ne zaman gelir?"),
            Entry(3, TaskTransitionKind.InquiryAnswered, TaskLifecycle.Waiting, TaskLifecycle.InProgress, Asked, "Cuma günü tedarikçiden.")]);
    }

    private static TaskItem BaseTask()
    {
        var task = new TaskItem
        {
            Id = TaskId,
            TenantId = TaskTestData.Tenant,
            Title = "Lot 42 serbest bırakma",
            AssignmentTarget = TaskAssignmentTarget.Person,
            AssigneeUserId = Holder,
            CreatedByUserId = Holder,
            OrganizationUnitId = UnitId,
            Lifecycle = TaskLifecycle.InProgress,
            Priority = TaskPriority.High,
            DueAt = DueAt,
            CreatedAt = CreatedAt,
            CreatedBy = "golden",
            Version = 4
        };
        task.CloseAcceptanceGate(Holder);
        return task;
    }

    private static TaskTransition Entry(
        int ordinal, TaskTransitionKind kind, TaskLifecycle from, TaskLifecycle to, Guid actor, string? reason) => new()
    {
        Id = Guid.Parse($"43943943-0000-4000-8000-00000000d00{ordinal}"),
        TenantId = TaskTestData.Tenant,
        TaskItemId = TaskId,
        Kind = kind,
        FromLifecycle = from,
        ToLifecycle = to,
        ActorUserId = actor,
        Reason = reason,
        CreatedAt = CreatedAt.AddHours(ordinal),
        CreatedBy = "golden"
    };

    private static async Task<WorkItemProjectionDto?> ProjectAsync(
        (TaskItem Task, TaskTransition[] History) world, Guid viewer, bool hasRead)
    {
        var transitions = new FakeTaskTransitionRepository();
        foreach (var entry in world.History)
        {
            await transitions.CreateAsync(entry);
        }

        var provider = new TaskWorkItemProvider(
            new FakeTaskItemRepository(world.Task),
            new FakePositionAssignmentRepository(),
            new TaskLifecycleService(),
            new TaskAssignmentResolver(),
            new FakeUserDisplayNameResolver((Holder, "Ali Tufanoğlu"), (Asked, "Ayşe Yılmaz")),
            new FakeChecklistRunRepository(),
            new FakeTaskApprovalService(),
            new FakeTaskDependencyRepository(),
            new FakeTaskCommentRepository(),
            transitions,
            new FakeTaskPersonalOverlayRepository(),
            new FakeTaskWatcherRepository(),
            TaskActors.PermitAll(),
            new FakePositionRepository(),
            new FakeOrganizationUnitRepository(),
            SlaForTests.Real(),
            new FakeTaskFieldDefinitionRepository(),
            new FakeTaskTypeRepository());

        var granted = hasRead
            ? new HashSet<string>([TaskPermissions.Read, TaskPermissions.Update, TaskPermissions.Complete], StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        var items = await provider.GetWorkItemsAsync(
            new WorkItemActor(viewer, IsPlatformActor: false, granted), CancellationToken.None);
        return items.FirstOrDefault(item => item.Id == TaskId.ToString());
    }

    /// <summary>The checkout this test runs in — the nearest directory holding a ".git" entry (BL-383).</summary>
    private static string FixturePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null
               && !File.Exists(Path.Combine(dir.FullName, ".git"))
               && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);
        return Path.Combine(dir!.FullName, "frontend", "Diten.Web", "tests", "fixtures", "task-inquiry-provider-projection.json");
    }
}
