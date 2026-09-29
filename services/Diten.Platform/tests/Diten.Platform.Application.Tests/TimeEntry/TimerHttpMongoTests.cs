using System.Net;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Infrastructure.Persistence.Schema;
using MongoDB.Driver;
using Xunit;
using TimeEntryRow = Diten.Platform.Domain.Entities.TimeEntry.TimeEntry;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// MOD-0280-FU01 T1b — the timer on the wire, over a real (disposable) MongoDB: the MOD-0024 hook (T-02, T-04, T-05's
/// "another person's act"), one running timer per person under a race (T-03), the legal-entity switch (T-20), timer
/// controls that never touch the task, switch + undo, and tenant isolation.
/// </summary>
[Collection(TimeEntryMongoCollection.Name)]
public sealed class TimerHttpMongoTests : TimerScenario
{
    public TimerHttpMongoTests(TimeEntryMongoFixture fixture) : base(fixture)
    {
    }

    // ── The hook: start ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Starting_a_task_on_the_wire_starts_the_holders_timer_on_it()
    {
        await EnableTimerAsync();
        var task = Guid.NewGuid();
        await SeedTaskAsync(Tenant, task, lifecycle: TaskLifecycle.Planned);

        Ok(await TaskVerbAsync(task, "start"));

        var segment = Assert.Single(await SegmentsAsync());
        Assert.True(segment.IsRunning);
        Assert.Equal(task, segment.TaskItemId);
        Assert.Equal(TimerStartSource.TaskStarted, segment.StartSource);
        Assert.Equal(Assert.Single(await TransitionsAsync(task), t => t.Kind == TaskTransitionKind.Started).Id, segment.StartTransitionId);
        Assert.Equal(Wednesday, segment.StartedAtUtc); // the server's clock, never the request's
        Assert.Equal(new DateOnly(2026, 10, 7), segment.LocalDate);

        var timer = await GetTimerAsync();
        Ok(timer);
        Assert.Equal(task, timer.Data.GetProperty("running").GetProperty("taskItemId").GetGuid());
    }

    [Fact]
    public async Task The_same_transition_observed_twice_starts_one_segment()
    {
        await EnableTimerAsync();
        var task = Guid.NewGuid();
        await SeedTaskAsync(Tenant, task, lifecycle: TaskLifecycle.Planned);
        Ok(await TaskVerbAsync(task, "start"));
        var started = Assert.Single(await TransitionsAsync(task), t => t.Kind == TaskTransitionKind.Started);

        // The replay arrives after the timer was stopped and a category was started: it must neither start the task's
        // timer again nor switch away from the category.
        Ok(await StopTimerAsync());
        Ok(await StartTimerAsync(category: Category));
        await ObserveAsync(new TaskTransitionObservation(
            Tenant, task, started.Id, TaskTransitionKind.Started, TaskLifecycle.Planned, TaskLifecycle.InProgress,
            Person, Person, Person, started.CreatedAt));

        var segments = await SegmentsAsync();
        Assert.Single(segments, s => s.StartTransitionId == started.Id);
        Assert.Equal(Category, Assert.Single(segments, s => s.IsRunning).CategoryCode);
    }

    [Fact]
    public async Task Another_persons_start_of_my_task_does_not_start_my_timer()
    {
        await EnableTimerAsync();
        var task = Guid.NewGuid();
        await SeedTaskAsync(Tenant, task, lifecycle: TaskLifecycle.Planned);

        await ChokePointAsync(task, TaskTransitionKind.Started, Manager, t => t.Lifecycle = TaskLifecycle.InProgress);

        Assert.Empty(await SegmentsAsync());
        Assert.Empty(await SegmentsAsync(Manager));
    }

    [Fact]
    public async Task Resuming_from_waiting_restarts_the_timer_as_a_resume()
    {
        await EnableTimerAsync();
        var task = Guid.NewGuid();
        await SeedTaskAsync(Tenant, task, lifecycle: TaskLifecycle.Waiting);

        await ChokePointAsync(task, TaskTransitionKind.Resumed, Person, t => t.Lifecycle = TaskLifecycle.InProgress);

        Assert.Equal(TimerStartSource.TaskResumed, Assert.Single(await SegmentsAsync()).StartSource);
    }

    // ── The hook: stop — state-based, every kind that leaves InProgress or moves the holder ─────────────────────

    public static TheoryData<TaskTransitionKind, TaskLifecycle, bool> StopKinds => new()
    {
        { TaskTransitionKind.Waiting, TaskLifecycle.Waiting, false },
        { TaskTransitionKind.SubmittedForReview, TaskLifecycle.PendingReview, false },
        { TaskTransitionKind.Completed, TaskLifecycle.Done, false },
        { TaskTransitionKind.Cancelled, TaskLifecycle.Cancelled, false },
        { TaskTransitionKind.Released, TaskLifecycle.InProgress, true },
        { TaskTransitionKind.Returned, TaskLifecycle.InProgress, true },
        { TaskTransitionKind.Reassigned, TaskLifecycle.InProgress, true },
        // An undeclared write that moved the task is still seen — the rule reads state, not the kind.
        { TaskTransitionKind.Unknown, TaskLifecycle.Waiting, false }
    };

    [Theory]
    [MemberData(nameof(StopKinds))]
    public async Task Every_write_that_ends_the_holders_run_stops_the_timer(
        TaskTransitionKind kind, TaskLifecycle to, bool holderMoves)
    {
        await EnableTimerAsync();
        Ok(await StartTimerAsync(TaskA));
        Host.Clock.UtcNow = Wednesday.AddMinutes(45);

        await ChokePointAsync(TaskA, kind, Person, t =>
        {
            t.Lifecycle = to;
            if (holderMoves)
            {
                t.AssigneeUserId = kind == TaskTransitionKind.Released ? null : SecondPerson;
            }
        });

        var segment = Assert.Single(await SegmentsAsync());
        Assert.False(segment.IsRunning);
        Assert.Equal(TimerStopReason.TaskTransition, segment.StopReason);
        Assert.Equal(45 * 60, segment.DurationSeconds);
        Assert.NotNull(segment.StopTransitionId);
        Assert.Equal(0, await RunningCountAsync(SecondPerson)); // reassigning never starts the new holder's timer
    }

    [Fact]
    public async Task Completing_the_task_on_the_wire_stops_the_timer_and_writes_its_draft()
    {
        await EnableTimerAsync();
        Ok(await StartTimerAsync(TaskA));
        Host.Clock.UtcNow = Wednesday.AddMinutes(50);

        Ok(await TaskVerbAsync(TaskA, "complete"));

        var segment = Assert.Single(await SegmentsAsync());
        Assert.Equal(TimerStopReason.TaskTransition, segment.StopReason);
        var week = await GetWeekAsync();
        var row = Assert.Single(week.Data.GetProperty("entries").EnumerateArray());
        Assert.Equal("Timer", row.GetProperty("source").GetString());
        Assert.Equal(45, row.GetProperty("durationMinutes").GetInt32()); // 50 min → nearest 15
    }

    [Fact]
    public async Task A_task_edit_that_moves_nothing_touches_no_timer_and_audits_nothing()
    {
        await EnableTimerAsync();
        Ok(await StartTimerAsync(TaskA));
        var audits = Host.Audit.Requests.Count;

        await ChokePointAsync(TaskA, TaskTransitionKind.Edited, Person, t => t.Title = "renamed");

        Assert.True(Assert.Single(await SegmentsAsync()).IsRunning);
        Assert.Equal(audits, Host.Audit.Requests.Count);
    }

    // ── Timer controls ──────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Timer_controls_never_write_a_task_transition_and_never_change_the_lifecycle()
    {
        await EnableTimerAsync();
        var before = await TransitionsAsync(TaskA);

        Ok(await StartTimerAsync(TaskA));
        Ok(await StopTimerAsync());

        Assert.Equal(before.Count, (await TransitionsAsync(TaskA)).Count);
        Assert.Equal(TaskLifecycle.InProgress,
            (await Collection<Domain.Entities.Tasks.TaskItem>(PlatformCollections.TaskItems).Find(t => t.Id == TaskA).SingleAsync()).Lifecycle);
    }

    [Fact]
    public async Task A_task_the_caller_does_not_hold_or_that_is_not_in_progress_is_refused()
    {
        await EnableTimerAsync();
        var foreign = Guid.NewGuid();
        await SeedTaskAsync(Tenant, foreign, assignee: SecondPerson);
        var planned = Guid.NewGuid();
        await SeedTaskAsync(Tenant, planned, lifecycle: TaskLifecycle.Planned);

        Assert.Equal(TimeEntryReasonCodes.TimerTaskNotHeld, (await StartTimerAsync(foreign)).ReasonCode);
        Assert.Equal(TimeEntryReasonCodes.TimerTaskNotHeld, (await StartTimerAsync(Guid.NewGuid())).ReasonCode); // same answer
        Assert.Equal(TimeEntryReasonCodes.TimerTaskNotInProgress, (await StartTimerAsync(planned)).ReasonCode);
        Assert.Equal(TimeEntryReasonCodes.TargetInvalid, (await StartTimerAsync(TaskA, Category)).ReasonCode);
        Assert.Empty(await SegmentsAsync());
    }

    [Fact]
    public async Task Starting_another_target_switches_and_the_switch_can_be_undone_from_now()
    {
        await EnableTimerAsync();
        Ok(await StartTimerAsync(TaskA));
        Host.Clock.UtcNow = Wednesday.AddMinutes(20);

        var switched = await StartTimerAsync(TaskB);
        Ok(switched);
        var token = switched.Data.GetProperty("timer").GetProperty("running").GetProperty("switchToken").GetGuid();
        var a = Assert.Single(await SegmentsAsync(), s => s.TaskItemId == TaskA);
        Assert.Equal(TimerStopReason.Switch, a.StopReason);

        Host.Clock.UtcNow = Wednesday.AddMinutes(20).AddSeconds(40);
        var undone = await Host.PostAsync("/api/v1/time-entry/timer/undo-switch", PersonToken(), new { switchToken = token });
        Ok(undone);

        var segments = await SegmentsAsync();
        var running = Assert.Single(segments, s => s.IsRunning);
        Assert.Equal(TaskA, running.TaskItemId);
        Assert.Equal(TimerStartSource.UndoSwitch, running.StartSource);
        Assert.Equal(Wednesday.AddMinutes(20).AddSeconds(40), running.StartedAtUtc); // not backdated
        Assert.Null(running.SwitchToken);                                              // an undo cannot be undone
        Assert.Equal(40, Assert.Single(segments, s => s.TaskItemId == TaskB).DurationSeconds);
    }

    [Fact]
    public async Task An_undo_after_its_window_is_refused()
    {
        await EnableTimerAsync();
        Ok(await StartTimerAsync(TaskA));
        var switched = await StartTimerAsync(TaskB);
        var token = switched.Data.GetProperty("timer").GetProperty("running").GetProperty("switchToken").GetGuid();

        Host.Clock.UtcNow = Wednesday.AddSeconds(61);
        var late = await Host.PostAsync("/api/v1/time-entry/timer/undo-switch", PersonToken(), new { switchToken = token });

        Assert.Equal(HttpStatusCode.Conflict, late.Status);
        Assert.Equal(TimeEntryReasonCodes.TimerUndoExpired, late.ReasonCode);
    }

    // ── T-03: one running timer per person under a race ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Twenty_parallel_starts_leave_exactly_one_running_segment()
    {
        await EnableTimerAsync();
        var token = PersonToken();

        var results = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(i => StartTimerAsync(i % 2 == 0 ? TaskA : TaskB, token: token)));

        Assert.All(results, r => Assert.Contains(r.Status, new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }));
        Assert.Equal(1, await RunningCountAsync());
    }

    // ── T-20 / D12: the legal-entity switch ─────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task With_no_switch_row_the_timer_is_off_task_start_still_works_and_manual_entry_still_works()
    {
        var task = Guid.NewGuid();
        await SeedTaskAsync(Tenant, task, lifecycle: TaskLifecycle.Planned);

        var start = await StartTimerAsync(TaskA);
        Assert.Equal(HttpStatusCode.Conflict, start.Status);
        Assert.Equal(TimeEntryReasonCodes.TimerDisabledForLegalEntity, start.ReasonCode);

        Ok(await TaskVerbAsync(task, "start"));                // the task moves…
        Assert.Empty(await SegmentsAsync());                  // …with no timer

        var timer = await GetTimerAsync();
        Assert.False(timer.Data.GetProperty("timerEnabled").GetBoolean());
        Ok(await SaveFreshAsync(CurrentWeek, Row(Monday, 60, TaskA)));
    }

    [Fact]
    public async Task Switching_the_legal_entity_off_closes_running_timers_at_the_switch_time()
    {
        await EnableTimerAsync();
        Ok(await StartTimerAsync(TaskA));
        Host.Clock.UtcNow = Wednesday.AddMinutes(30);

        var version = (await Collection<LegalEntityTimeSetting>(PlatformCollections.TimeEntryLegalEntitySettings)
            .Find(s => s.TenantId == Tenant).SingleAsync()).Version;
        Ok(await Host.PutAsync($"/api/v1/time-entry/settings/legal-entities/{LegalEntity}", AdminToken(),
            new { expectedVersion = version, timerEnabled = false, reason = (string?)null }));

        var segment = Assert.Single(await SegmentsAsync());
        Assert.Equal(TimerStopReason.SwitchedOff, segment.StopReason);
        Assert.Equal(30 * 60, segment.DurationSeconds);
    }

    // ── §13 "hook failed": the read reconciles ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_timer_whose_task_left_the_person_without_the_hook_is_closed_by_the_next_read()
    {
        await EnableTimerAsync();
        Ok(await StartTimerAsync(TaskA));
        Host.Clock.UtcNow = Wednesday.AddMinutes(30);

        // The task moved behind the hook's back (a raw write — what a hook that threw leaves behind).
        await Collection<Domain.Entities.Tasks.TaskItem>(PlatformCollections.TaskItems).UpdateOneAsync(
            t => t.Id == TaskA, Builders<Domain.Entities.Tasks.TaskItem>.Update.Set(t => t.Lifecycle, TaskLifecycle.Done));

        var timer = await GetTimerAsync();
        Ok(timer);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, timer.Data.GetProperty("running").ValueKind);
        Assert.Equal(TimerStopReason.Reconcile, Assert.Single(await SegmentsAsync()).StopReason);
    }

    // ── D11 / tenant isolation ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Nobody_reads_anyone_elses_timer_and_another_tenant_sees_nothing()
    {
        await EnableTimerAsync();
        Ok(await StartTimerAsync(TaskA));

        var manager = await GetTimerAsync(PersonToken(person: Manager));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, manager.Data.GetProperty("running").ValueKind);

        var sameUserOtherTenant = await GetTimerAsync(PersonToken(tenant: OtherTenant));
        Ok(sameUserOtherTenant);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, sameUserOtherTenant.Data.GetProperty("running").ValueKind);
        Assert.False(sameUserOtherTenant.Data.GetProperty("timerEnabled").GetBoolean());
        Assert.Equal(1, await RunningCountAsync()); // and the other tenant's read did not close it
    }

    [Fact]
    public async Task Every_timer_route_is_the_callers_own_no_route_takes_a_user_id()
    {
        var routes = typeof(Diten.Platform.API.Controllers.TimeEntryController).GetMethods()
            .SelectMany(m => m.GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute), false)
                .Cast<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>()
                .Select(a => (Method: m, a.Template)))
            .Where(x => x.Template?.StartsWith("timer", StringComparison.Ordinal) == true)
            .ToList();

        Assert.Equal(4, routes.Count);
        Assert.All(routes, r => Assert.DoesNotContain(r.Method.GetParameters(), p => p.Name!.Contains("user", StringComparison.OrdinalIgnoreCase)));
        await Task.CompletedTask;
    }

    // ── Audit ───────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Timer_state_changes_are_audited_and_a_quiet_read_audits_nothing()
    {
        await EnableTimerAsync();
        Ok(await StartTimerAsync(TaskA));
        Host.Clock.UtcNow = Wednesday.AddMinutes(30);
        Ok(await StopTimerAsync());
        var afterWrites = Host.Audit.Requests.Count(r => r.EntityType == "TimerSegment");

        Ok(await GetTimerAsync());
        Ok(await GetWeekAsync());

        Assert.Equal(2, afterWrites);
        Assert.Equal(afterWrites, Host.Audit.Requests.Count(r => r.EntityType == "TimerSegment"));
        Assert.Single(await Collection<TimeEntryRow>(PlatformCollections.TimeEntryEntries)
            .Find(e => e.TenantId == Tenant && !e.IsDeleted).ToListAsync(), e => e.Source == TimeEntrySource.Timer);
    }
}
