namespace Diten.BuildingBlocks.BackgroundJobs;

public interface IBackgroundJobScheduler
{
    Task<string> EnqueueAsync<TArgs, THandler>(
        TArgs args,
        BackgroundJobContext? context = null,
        CancellationToken cancellationToken = default)
        where THandler : IBackgroundJobHandler<TArgs>;

    Task<string> ScheduleAsync<TArgs, THandler>(
        TArgs args,
        DateTimeOffset enqueueAtUtc,
        BackgroundJobContext? context = null,
        CancellationToken cancellationToken = default)
        where THandler : IBackgroundJobHandler<TArgs>;

    Task RegisterRecurringAsync(
        RecurringJobRegistration registration,
        CancellationToken cancellationToken = default);
}
