using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.TimeEntry;
using Diten.Platform.Application.Features.Tasks.Services;
using Diten.Platform.Application.Features.WorkAggregation;
using Diten.Platform.Application.Tests.Tasks;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// MOD-0280-FU01 T1b — the Task Center projection's side of time entry, on the REAL <see cref="TaskWorkItemProvider"/>:
/// <list type="bullet">
/// <item>D7 / T-15 — <c>SpentHours</c> (the effort card, <c>taskContext</c>) is the APPROVED time from
/// <see cref="ITaskSpentTimeSource"/>, never the entity's writer-less field (a decoy of 7 h here).</item>
/// <item>§5.1 item 3 — the stop rule: with the production default (off) no <c>timeTracking</c>, no <c>timeEntries</c>
/// and <c>timerState: notApplicable</c> throughout — the WC-1 contract refuses a running timer or the data block
/// without the capability.</item>
/// <item>§16 D2 / T-05 — with it on (T2): <c>running</c> only on the reader's own running item, <c>inactive</c> on
/// the reader's other InProgress items, <c>notApplicable</c> on anyone else's; never <c>paused</c> (BL-237).</item>
/// </list>
/// </summary>
public sealed class TaskTimeProjectionTests
{
    private static readonly Guid Running = Guid.NewGuid();
    private static readonly Guid AlsoInProgress = Guid.NewGuid();
    private static readonly Guid NotStarted = Guid.NewGuid();

    [Fact]
    public async Task Effort_spent_is_the_approved_time_never_the_entity_field()
    {
        var task = Work(Running, TaskLifecycle.InProgress);
        task.EstimateHours = 10m;
        task.SpentHours = 7m; // decoy — nothing writes it in production

        var withSource = await ProjectAsync(TaskTestData.Me, trackTime: false, source: new StubSpentTime(), task);
        Assert.Equal(2m, Assert.Single(withSource).SpentHours);

        var withoutSource = await ProjectAsync(TaskTestData.Me, trackTime: false, source: null, Work(Running, TaskLifecycle.InProgress, estimate: 10m, decoy: 7m));
        Assert.Equal(0m, Assert.Single(withoutSource).SpentHours); // absent source ⇒ no approved time, never the decoy
    }

    [Fact]
    public void The_module_registers_time_tracking_ON_now_that_T2b_ships_its_card()
    {
        using var provider = new ServiceCollection().AddTimeEntryModule().BuildServiceProvider();

        Assert.True(provider.GetRequiredService<TaskTimeTrackingOptions>().DeclareTimeTracking);
    }

    [Fact]
    public async Task With_time_tracking_off_nothing_about_time_tracking_is_declared()
    {
        Assert.False(new TaskTimeTrackingOptions().DeclareTimeTracking); // the class default; the module turns it on

        var items = await ProjectAsync(TaskTestData.Me, trackTime: false, source: new StubSpentTime(),
            Work(Running, TaskLifecycle.InProgress), Work(AlsoInProgress, TaskLifecycle.InProgress));

        Assert.All(items, item =>
        {
            Assert.DoesNotContain("timeTracking", item.WorkItemCapabilities);
            Assert.Null(item.TimeEntries);
            Assert.Equal(WorkItemContract.NotApplicable, item.TimerState);
        });
    }

    [Fact]
    public async Task With_time_tracking_on_the_timer_state_is_the_readers_own()
    {
        var items = (await ProjectAsync(TaskTestData.Me, trackTime: true, source: new StubSpentTime(),
                Work(Running, TaskLifecycle.InProgress), Work(AlsoInProgress, TaskLifecycle.InProgress), Work(NotStarted, TaskLifecycle.Open)))
            .ToDictionary(i => Guid.Parse(i.Id));

        Assert.Equal("running", items[Running].TimerState);
        Assert.Equal("inactive", items[AlsoInProgress].TimerState);
        Assert.Equal(WorkItemContract.NotApplicable, items[NotStarted].TimerState);
        Assert.All(items.Values, item =>
        {
            Assert.Contains("timeTracking", item.WorkItemCapabilities);   // container ⇔ capability
            Assert.NotNull(item.TimeEntries);
            Assert.NotEqual("paused", item.TimerState);
        });
        Assert.Equal(new WorkItemTimeEntriesDto(30, 60, 120), items[Running].TimeEntries);
    }

    [Fact]
    public async Task Another_readers_view_of_my_running_task_says_not_applicable_and_shows_their_own_drafts()
    {
        var provider = Provider(trackTime: true, new StubSpentTime(), Work(Running, TaskLifecycle.InProgress));
        var manager = new WorkItemActor(TaskTestData.Rival, IsPlatformActor: true, new HashSet<string>());

        var item = await provider.GetWorkItemAsync(Work(Running, TaskLifecycle.InProgress), manager);

        Assert.Equal(WorkItemContract.NotApplicable, item.TimerState);
        Assert.Equal(0, item.TimeEntries!.DraftMinutes);                      // never the holder's draft
        Assert.Equal(120, item.TimeEntries.ApprovedMinutes);                  // task totals are the task's
    }

    // ── T2b — startTimer / stopTimer on the Task Center card (pack §19.2, U1) ─────────────────────────────────────

    [Fact]
    public async Task The_holder_gets_start_on_an_inactive_InProgress_task_and_stop_on_the_running_one()
    {
        var items = (await ProjectAsync(TaskTestData.Me, trackTime: true, source: new StubSpentTime(), timerOn: true,
                Work(Running, TaskLifecycle.InProgress), Work(AlsoInProgress, TaskLifecycle.InProgress), Work(NotStarted, TaskLifecycle.Open)))
            .ToDictionary(i => Guid.Parse(i.Id));

        Assert.Equal(["stopTimer"], TimerCodes(items[Running]));
        Assert.Equal(["startTimer"], TimerCodes(items[AlsoInProgress]));
        Assert.Empty(TimerCodes(items[NotStarted]));                       // not InProgress
        Assert.True(items[AlsoInProgress].Actions.Single(a => a.Code == "startTimer").Enabled);
    }

    [Fact]
    public async Task Nobody_else_gets_a_timer_action_on_my_task()
    {
        var provider = Provider(trackTime: true, new StubSpentTime(), timerOn: true, Work(Running, TaskLifecycle.InProgress));
        var manager = new WorkItemActor(TaskTestData.Rival, IsPlatformActor: true, new HashSet<string>());

        var item = await provider.GetWorkItemAsync(Work(Running, TaskLifecycle.InProgress), manager);

        Assert.Empty(TimerCodes(item));
    }

    [Fact]
    public async Task With_the_timer_switched_off_for_the_legal_entity_there_is_no_timer_action()
    {
        var items = await ProjectAsync(TaskTestData.Me, trackTime: true, source: new StubSpentTime(), timerOn: false,
            Work(Running, TaskLifecycle.InProgress), Work(AlsoInProgress, TaskLifecycle.InProgress));

        Assert.All(items, item => Assert.Empty(TimerCodes(item)));
        Assert.All(items, item => Assert.Contains("timeTracking", item.WorkItemCapabilities)); // the card still shows time
    }

    [Fact]
    public async Task With_time_tracking_off_there_is_no_timer_action_even_with_the_switch_on()
    {
        var items = await ProjectAsync(TaskTestData.Me, trackTime: false, source: new StubSpentTime(), timerOn: true,
            Work(AlsoInProgress, TaskLifecycle.InProgress));

        Assert.Empty(TimerCodes(Assert.Single(items)));
    }

    [Fact]
    public async Task Without_the_update_key_the_timer_action_is_offered_disabled_not_hidden()
    {
        var provider = Provider(trackTime: true, new StubSpentTime(), timerOn: true, Work(AlsoInProgress, TaskLifecycle.InProgress));
        var holder = new WorkItemActor(TaskTestData.Me, IsPlatformActor: false, new HashSet<string> { TaskPermissions.Read });

        var item = await provider.GetWorkItemAsync(Work(AlsoInProgress, TaskLifecycle.InProgress), holder);

        var start = item.Actions.Single(a => a.Code == "startTimer");
        Assert.False(start.Enabled);
    }

    [Fact]
    public void The_provider_declares_the_key_its_timer_actions_check()
        => Assert.Contains(TimeEntryPermissions.TimesheetsUpdate, Provider(true, new StubSpentTime()).RequiredActionPermissions);

    private static List<string> TimerCodes(WorkItemProjectionDto item)
        => item.Actions.Select(a => a.Code).Where(c => c is "startTimer" or "stopTimer").ToList();

    private sealed class StubTimerAvailability(bool on) : ITimeEntryTimerAvailability
    {
        public Task<bool> IsTimerEnabledForAsync(Guid userId, CancellationToken ct = default) => Task.FromResult(on);
    }

    private static TaskItem Work(Guid id, TaskLifecycle lifecycle, decimal? estimate = null, decimal decoy = 0m) => new()
    {
        Id = id,
        TenantId = TaskTestData.Tenant,
        Title = "Work " + id.ToString("N")[..4],
        AssignmentTarget = TaskAssignmentTarget.SelfAssigned,
        AssigneeUserId = TaskTestData.Me,
        CreatedByUserId = TaskTestData.Me,
        OrganizationUnitId = Guid.NewGuid(),
        Lifecycle = lifecycle,
        EstimateHours = estimate,
        SpentHours = decoy,
        Version = 1
    };

    private static Task<IReadOnlyList<WorkItemProjectionDto>> ProjectAsync(
        Guid reader, bool trackTime, ITaskSpentTimeSource? source, params TaskItem[] tasks)
        => ProjectAsync(reader, trackTime, source, timerOn: false, tasks);

    private static async Task<IReadOnlyList<WorkItemProjectionDto>> ProjectAsync(
        Guid reader, bool trackTime, ITaskSpentTimeSource? source, bool timerOn, params TaskItem[] tasks)
        => await Provider(trackTime, source, timerOn, tasks)
            .GetWorkItemsAsync(new WorkItemActor(reader, IsPlatformActor: true, new HashSet<string>()), CancellationToken.None);

    private static TaskWorkItemProvider Provider(bool trackTime, ITaskSpentTimeSource? source, params TaskItem[] tasks)
        => Provider(trackTime, source, timerOn: false, tasks);

    private static TaskWorkItemProvider Provider(bool trackTime, ITaskSpentTimeSource? source, bool timerOn, params TaskItem[] tasks) => new(
        new FakeTaskItemRepository(tasks),
        new FakePositionAssignmentRepository(),
        new TaskLifecycleService(),
        new TaskAssignmentResolver(),
        new FakeUserDisplayNameResolver(),
        new FakeChecklistRunRepository(),
        new FakeTaskApprovalService(),
        new FakeTaskDependencyRepository(),
        new FakeTaskCommentRepository(), new FakeTaskTransitionRepository(), new FakeTaskPersonalOverlayRepository(),
        new FakeTaskWatcherRepository(), TaskActors.PermitAll(),
        new FakePositionRepository(),
        new FakeOrganizationUnitRepository(),
        SlaForTests.Real(),
        new FakeTaskFieldDefinitionRepository(), new FakeTaskTypeRepository(),
        spentTime: source,
        timeTracking: new TaskTimeTrackingOptions { DeclareTimeTracking = trackTime },
        timerAvailability: new StubTimerAvailability(timerOn));

    /// <summary>120 approved + 60 submitted on every task; the holder (<see cref="TaskTestData.Me"/>) runs a timer on
    /// <see cref="Running"/> and has 30 draft minutes there. Anybody else has neither.</summary>
    private sealed class StubSpentTime : ITaskSpentTimeSource
    {
        public Task<IReadOnlyDictionary<Guid, int>> ApprovedMinutesAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, int>>(taskIds.ToDictionary(id => id, _ => 120));

        public Task<IReadOnlyDictionary<Guid, TaskSpentTime>> SpentTimeAsync(IReadOnlyCollection<Guid> taskIds, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyDictionary<Guid, TaskSpentTime>>(taskIds.ToDictionary(id => id, _ => new TaskSpentTime(120, 60)));

        public Task<TaskReaderTime> ReaderTimeAsync(Guid readerUserId, IReadOnlyCollection<Guid> taskIds, CancellationToken ct = default)
            => Task.FromResult(readerUserId == TaskTestData.Me
                ? new TaskReaderTime(Running, new Dictionary<Guid, int> { [Running] = 30 })
                : TaskReaderTime.None);
    }
}
