using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.BackgroundJobs;
using Xunit;

namespace Diten.Platform.BackgroundJobs.Tests;

public sealed class SchedulerSmokeTestJobTests
{
    [Fact]
    public async Task Smoke_job_success_mode_completes()
    {
        var job = new SchedulerSmokeTestJob();

        await job.HandleAsync(new SchedulerSmokeTestJobArgs(false), new BackgroundJobContext());
    }

    [Fact]
    public async Task Smoke_job_failure_mode_throws_controlled_error()
    {
        var job = new SchedulerSmokeTestJob();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            job.HandleAsync(new SchedulerSmokeTestJobArgs(true), new BackgroundJobContext()));

        Assert.Contains("Controlled scheduler smoke failure", ex.Message);
    }
}
