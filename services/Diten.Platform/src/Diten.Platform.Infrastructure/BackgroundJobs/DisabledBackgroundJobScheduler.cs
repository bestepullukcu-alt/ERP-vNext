using Diten.BuildingBlocks.BackgroundJobs;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Infrastructure.BackgroundJobs;

public sealed class DisabledBackgroundJobScheduler : IBackgroundJobScheduler
{
    private readonly ILogger<DisabledBackgroundJobScheduler> _logger;

    public DisabledBackgroundJobScheduler(ILogger<DisabledBackgroundJobScheduler> logger)
    {
        _logger = logger;
    }

    public Task<string> EnqueueAsync<TArgs, THandler>(
        TArgs args,
        BackgroundJobContext? context = null,
        CancellationToken cancellationToken = default)
        where THandler : IBackgroundJobHandler<TArgs>
    {
        var jobId = BuildDeferredId(typeof(THandler).Name);
        _logger.LogInformation(
            "Background job scheduler is disabled; enqueue request for {JobHandler} was deferred as {JobId}.",
            typeof(THandler).Name,
            jobId);

        return Task.FromResult(jobId);
    }

    public Task<string> ScheduleAsync<TArgs, THandler>(
        TArgs args,
        DateTimeOffset enqueueAtUtc,
        BackgroundJobContext? context = null,
        CancellationToken cancellationToken = default)
        where THandler : IBackgroundJobHandler<TArgs>
    {
        var jobId = BuildDeferredId(typeof(THandler).Name);
        _logger.LogInformation(
            "Background job scheduler is disabled; scheduled request for {JobHandler} at {EnqueueAtUtc} was deferred as {JobId}.",
            typeof(THandler).Name,
            enqueueAtUtc,
            jobId);

        return Task.FromResult(jobId);
    }

    public Task RegisterRecurringAsync(
        RecurringJobRegistration registration,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Background job scheduler is disabled; recurring job {JobId} was not registered.",
            registration.Descriptor.Id);

        return Task.CompletedTask;
    }

    private static string BuildDeferredId(string jobHandlerName)
    {
        return $"disabled:{jobHandlerName}:{Guid.NewGuid():N}";
    }
}
