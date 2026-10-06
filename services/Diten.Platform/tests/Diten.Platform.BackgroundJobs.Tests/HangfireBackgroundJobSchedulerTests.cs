using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.BackgroundJobs;
using Diten.Platform.Infrastructure.BackgroundJobs;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Diten.Platform.BackgroundJobs.Tests;

public sealed class HangfireBackgroundJobSchedulerTests
{
    [Fact]
    public async Task Scheduler_maps_enqueue_and_schedule_to_hangfire_client()
    {
        var client = new Mock<IBackgroundJobClient>();
        client.Setup(x => x.Create(It.IsAny<Job>(), It.IsAny<IState>())).Returns("job-1");
        var recurring = new Mock<IRecurringJobManager>();
        var scheduler = new HangfireBackgroundJobScheduler(
            client.Object,
            recurring.Object,
            Options.Create(new BackgroundJobSchedulerOptions()));

        var enqueueId = await scheduler.EnqueueAsync<SchedulerSmokeTestJobArgs, SchedulerSmokeTestJob>(
            new SchedulerSmokeTestJobArgs(false));
        var scheduledId = await scheduler.ScheduleAsync<SchedulerSmokeTestJobArgs, SchedulerSmokeTestJob>(
            new SchedulerSmokeTestJobArgs(false),
            DateTimeOffset.UtcNow.AddMinutes(1));

        Assert.Equal("job-1", enqueueId);
        Assert.Equal("job-1", scheduledId);
        client.Verify(x => x.Create(It.IsAny<Job>(), It.IsAny<EnqueuedState>()), Times.Once);
        client.Verify(x => x.Create(It.IsAny<Job>(), It.IsAny<ScheduledState>()), Times.Once);
    }

    [Fact]
    public async Task Scheduler_registers_enabled_recurring_job_with_hangfire_manager()
    {
        var client = new Mock<IBackgroundJobClient>();
        var recurring = new Mock<IRecurringJobManager>();
        var scheduler = new HangfireBackgroundJobScheduler(
            client.Object,
            recurring.Object,
            Options.Create(new BackgroundJobSchedulerOptions()));
        var descriptor = new BackgroundJobDescriptor(
            "Diten.Platform.SchedulerSmokeTestJob",
            "Diten.Platform",
            "SchedulerSmokeTestJob",
            "MOD-0026",
            "* * * * *",
            IsEnabled: true);

        await scheduler.RegisterRecurringAsync(new RecurringJobRegistration(
            descriptor,
            typeof(SchedulerSmokeTestJob),
            typeof(SchedulerSmokeTestJobArgs),
            new SchedulerSmokeTestJobArgs(false)));

        recurring.Verify(x => x.AddOrUpdate(
            "Diten.Platform.SchedulerSmokeTestJob",
            It.IsAny<Job>(),
            "* * * * *",
            It.IsAny<RecurringJobOptions>()),
            Times.Once);
    }

    [Fact]
    public async Task Scheduler_removes_a_disabled_recurring_job_instead_of_leaving_it_scheduled()
    {
        var client = new Mock<IBackgroundJobClient>();
        var recurring = new Mock<IRecurringJobManager>();
        var scheduler = new HangfireBackgroundJobScheduler(
            client.Object,
            recurring.Object,
            Options.Create(new BackgroundJobSchedulerOptions()));
        var descriptor = new BackgroundJobDescriptor(
            "Diten.Platform.SchedulerSmokeTestJob",
            "Diten.Platform",
            "SchedulerSmokeTestJob",
            "MOD-0026",
            "* * * * *",
            IsEnabled: false);

        await scheduler.RegisterRecurringAsync(new RecurringJobRegistration(
            descriptor,
            typeof(SchedulerSmokeTestJob),
            typeof(SchedulerSmokeTestJobArgs),
            new SchedulerSmokeTestJobArgs(false)));

        // S10 live pass: a flag switched off must take the job out of Hangfire, not just skip re-adding it.
        recurring.Verify(x => x.RemoveIfExists("Diten.Platform.SchedulerSmokeTestJob"), Times.Once);
        recurring.Verify(x => x.AddOrUpdate(
            It.IsAny<string>(), It.IsAny<Job>(), It.IsAny<string>(), It.IsAny<RecurringJobOptions>()), Times.Never);
    }
}
