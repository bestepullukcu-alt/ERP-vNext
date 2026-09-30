using System.Net;
using Diten.Platform.Application.Features.Tasks;
using Diten.Platform.Application.Features.Tasks.Providers;
using Diten.Platform.Application.Features.TimeEntry;
using Diten.Platform.Application.Features.WorkAggregation.Dispatch;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Platform.Application.Tests.TimeEntry;

/// <summary>
/// MOD-0280-FU01 T2b (pack §19.2, U1) — the Task Center card's <c>startTimer</c> / <c>stopTimer</c> on the wire, through
/// the SAME address every Task Center action takes (<c>POST api/v1/work-items/{id}/actions/{code}</c> →
/// <see cref="TaskWorkItemActionDispatcher"/>) against the disposable mongod. The dispatcher only translates: every timer
/// rule — the caller must hold the task, it must be InProgress, the timer must be switched on for their legal entity —
/// is the TimeEntry handler's, and its refusal code comes back through the dispatcher untouched.
/// </summary>
[Collection(TimeEntryMongoCollection.Name)]
public sealed class TaskCenterTimerDispatchHttpMongoTests : TimerScenario
{
    public TaskCenterTimerDispatchHttpMongoTests(TimeEntryMongoFixture fixture) : base(fixture)
    {
    }

    /// <summary>The Task Center's real controller and the task dispatcher, as production registers the dispatcher.</summary>
    protected override void ConfigureHost(IServiceCollection services)
        => services.AddScoped<IWorkItemActionDispatcher, TaskWorkItemActionDispatcher>();

    private Task<ApiResult> DispatchAsync(Guid taskId, string code, string? token = null)
        => Host.PostAsync($"/api/v1/work-items/{taskId}/actions/{code}", token ?? TaskToken(),
            new { providerCode = "tasks", payload = new { } });

    [Fact]
    public async Task The_holder_starts_and_stops_their_timer_from_the_card()
    {
        await EnableTimerAsync();

        var started = await DispatchAsync(TaskA, "startTimer");
        Assert.True(started.Status == HttpStatusCode.OK, started.ToString());
        var running = Assert.Single(await SegmentsAsync(), s => s.IsRunning);
        Assert.Equal(TaskA, running.TaskItemId);

        var stopped = await DispatchAsync(TaskA, "stopTimer");
        Assert.True(stopped.Status == HttpStatusCode.OK, stopped.ToString());
        Assert.Equal(0, await RunningCountAsync());
    }

    [Fact]
    public async Task A_person_who_does_not_hold_the_task_is_refused_through_the_dispatcher()
    {
        await EnableTimerAsync();
        var theirSeat = Guid.NewGuid();
        await SeedPositionAsync(theirSeat, reportsTo: ManagerSeat);
        await SeatAsync(SecondPerson, theirSeat); // same legal entity: the switch is ON for them too

        var refused = await DispatchAsync(TaskA, "startTimer", TaskToken(SecondPerson));

        Assert.Equal(HttpStatusCode.Conflict, refused.Status);
        Assert.Equal(TimeEntryReasonCodes.TimerTaskNotHeld, refused.ReasonCode);
        Assert.Empty(await SegmentsAsync(SecondPerson));
        Assert.Empty(await SegmentsAsync());
    }

    [Fact]
    public async Task With_the_timer_switched_off_for_the_legal_entity_the_card_start_is_refused()
    {
        // No switch row = off (D12).
        var refused = await DispatchAsync(TaskA, "startTimer");

        Assert.Equal(HttpStatusCode.Conflict, refused.Status);
        Assert.Equal(TimeEntryReasonCodes.TimerDisabledForLegalEntity, refused.ReasonCode);
        Assert.Empty(await SegmentsAsync());
    }

    [Fact]
    public async Task Without_the_timesheet_update_key_the_card_timer_is_forbidden()
    {
        await EnableTimerAsync();
        var readOnly = Host.Token(Person, Tenant, TaskPermissions.Read, TaskPermissions.Update, TimeEntryPermissions.TimesheetsRead);

        var refused = await DispatchAsync(TaskA, "startTimer", readOnly);

        Assert.Equal(HttpStatusCode.Forbidden, refused.Status);
        Assert.Empty(await SegmentsAsync());
    }
}
