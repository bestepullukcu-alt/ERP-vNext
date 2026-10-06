using System.Net;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.TimeEntry;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Entities.TimeEntry;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Enums.TimeEntry;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.Persistence.Schema;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// MOD-0280-FU01 T1b — the timer's world on top of <see cref="TimeEntryScenario"/>: the timer switched ON for the
/// person's legal entity (no row = off, D12), task tokens that may drive MOD-0024 transitions, and the two real doors a
/// task moves through — the Tasks HTTP endpoint and, for the transition kinds whose handlers this host does not wire, the
/// production choke point itself (<c>TaskItemRepository.UpdateAsync</c> with a declared intent, which every MOD-0024
/// handler goes through).
/// </summary>
public abstract class TimerScenario : TimeEntryScenario
{
    protected TimerScenario(TimeEntryMongoFixture fixture) : base(fixture)
    {
    }

    /// <summary>12:00 on Wednesday 2026-10-07 in Istanbul (UTC+3) — the base clock.</summary>
    protected static DateTimeOffset IstanbulLocal(int year, int month, int day, int hour, int minute)
        => new DateTimeOffset(year, month, day, hour, minute, 0, TimeSpan.FromHours(3)).ToUniversalTime();

    protected Task EnableTimerAsync(Guid? legalEntity = null, Guid? tenant = null)
        => Collection<LegalEntityTimeSetting>(PlatformCollections.TimeEntryLegalEntitySettings).InsertOneAsync(new LegalEntityTimeSetting
        {
            TenantId = tenant ?? Tenant,
            LegalEntityId = legalEntity ?? LegalEntity,
            TimerEnabled = true,
            ChangedAtUtc = Wednesday.AddDays(-30),
            ChangedByUserId = PoolAdmin,
            Reason = "test: legal basis recorded"
        });

    /// <summary>The person's token with the task keys a holder uses, plus their own timesheet keys.</summary>
    protected string TaskToken(Guid? user = null, Guid? tenant = null)
        => Host.Token(user ?? Person, tenant ?? Tenant,
            TaskPermissions.Read, TaskPermissions.Update, TaskPermissions.Complete, TaskPermissions.Cancel,
            TimeEntryPermissions.TimesheetsRead, TimeEntryPermissions.TimesheetsUpdate);

    protected async Task<int> TaskVersionAsync(Guid taskId)
        => (await Collection<TaskItem>(PlatformCollections.TaskItems).Find(t => t.Id == taskId).SingleAsync()).Version;

    /// <summary>POST /api/v1/tasks/{id}/{verb} — MOD-0024's own route.</summary>
    protected async Task<ApiResult> TaskVerbAsync(Guid taskId, string verb, string? token = null)
        => await Host.PostAsync($"/api/v1/tasks/{taskId}/{verb}", token ?? TaskToken(),
            new { expectedVersion = await TaskVersionAsync(taskId), reasonCode = (string?)null, note = (string?)null });

    /// <summary>
    /// A MOD-0024 write at the production choke point: the task is read, a transition is DECLARED, the change is applied
    /// and <c>TaskItemRepository.UpdateAsync</c> commits it — the one place a transition is recorded and observed.
    /// </summary>
    protected async Task ChokePointAsync(Guid taskId, TaskTransitionKind kind, Guid? actor, Action<TaskItem> change)
    {
        using var scope = Host.Services.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        using (TenantScope.Begin(tenantContext, Tenant))
        {
            var tasks = scope.ServiceProvider.GetRequiredService<ITaskItemRepository>();
            var task = (await tasks.GetByIdAsync(taskId))!;
            task.Declare(kind, actor);
            change(task);
            Assert.True(await tasks.UpdateAsync(task, task.Version), "the task write lost its version race");
        }
    }

    /// <summary>Calls the observer directly with a given observation — how a replayed transition is staged.</summary>
    protected async Task ObserveAsync(TaskTransitionObservation observation)
    {
        using var scope = Host.Services.CreateScope();
        var tenantContext = scope.ServiceProvider.GetRequiredService<ITenantContext>();
        using (TenantScope.Begin(tenantContext, observation.TenantId))
        {
            await scope.ServiceProvider.GetRequiredService<ITaskTransitionObserver>().OnTransitionRecordedAsync(observation);
        }
    }

    protected Task<List<TimerSegment>> SegmentsAsync(Guid? user = null, Guid? tenant = null)
        => Collection<TimerSegment>(PlatformCollections.TimeEntryTimerSegments)
            .Find(s => s.TenantId == (tenant ?? Tenant) && s.UserId == (user ?? Person)).ToListAsync();

    protected Task<long> RunningCountAsync(Guid? user = null)
        => Collection<TimerSegment>(PlatformCollections.TimeEntryTimerSegments)
            .CountDocumentsAsync(s => s.TenantId == Tenant && s.UserId == (user ?? Person) && s.IsRunning);

    protected Task<List<TaskTransition>> TransitionsAsync(Guid taskId)
        => Collection<TaskTransition>(PlatformCollections.TaskTransitions).Find(t => t.TaskItemId == taskId).ToListAsync();

    protected async Task<ApiResult> StartTimerAsync(Guid? task = null, string? category = null, string? token = null)
        => await Host.PostAsync("/api/v1/time-entry/timer/start", token ?? PersonToken(), new { taskItemId = task, categoryCode = category });

    protected Task<ApiResult> StopTimerAsync(string? token = null)
        => Host.PostAsync("/api/v1/time-entry/timer/stop", token ?? PersonToken());

    protected Task<ApiResult> GetTimerAsync(string? token = null)
        => Host.GetAsync("/api/v1/time-entry/timer", token ?? PersonToken());

    /// <summary>A closed segment written straight to the store — how a test stages timer history of a given length.</summary>
    protected Task SeedClosedSegmentAsync(DateOnly localDate, int seconds, Guid? task = null, string? category = null,
        DateTimeOffset? startedAtUtc = null, string zone = Zone)
        => Collection<TimerSegment>(PlatformCollections.TimeEntryTimerSegments).InsertOneAsync(new TimerSegment
        {
            TenantId = Tenant,
            UserId = Person,
            TaskItemId = task,
            CategoryCode = task is null ? category ?? Category : null,
            IsRunning = false,
            StartedAtUtc = startedAtUtc ?? Wednesday.AddHours(-1),
            StoppedAtUtc = (startedAtUtc ?? Wednesday.AddHours(-1)).AddSeconds(seconds),
            DurationSeconds = seconds,
            LocalDate = localDate,
            TimeZoneId = zone,
            WeekKey = Application.Features.TimeEntry.Services.WeekCalendar.KeyOf(localDate),
            StartSource = TimerStartSource.TimerControl,
            StopReason = TimerStopReason.TimerControl
        });

    protected static void Ok(ApiResult result)
        => Assert.True(result.Status is HttpStatusCode.OK or HttpStatusCode.NoContent, result.ToString());
}
