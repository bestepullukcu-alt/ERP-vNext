using Diten.BuildingBlocks.BackgroundJobs;

namespace Diten.Platform.Application.BackgroundJobs;

public sealed class SchedulerSmokeTestJob : IBackgroundJobHandler<SchedulerSmokeTestJobArgs>
{
    public Task HandleAsync(
        SchedulerSmokeTestJobArgs args,
        BackgroundJobContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (args.ShouldFail)
        {
            throw new InvalidOperationException(
                "Controlled scheduler smoke failure. password=demo-secret token=demo-token connectionString=mongodb://user:pass@localhost payload={\"sample\":\"redacted\"}");
        }

        return Task.CompletedTask;
    }
}
