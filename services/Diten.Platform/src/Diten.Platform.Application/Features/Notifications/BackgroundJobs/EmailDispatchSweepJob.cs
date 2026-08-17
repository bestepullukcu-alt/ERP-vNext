using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Notifications.BackgroundJobs;

/// <summary>
/// Recurring sweep that finds dispatches whose retry is due and enqueues a per-dispatch
/// targeted <see cref="EmailDispatchJob"/> through the MOD-0026 scheduler abstraction.
/// Owns no per-dispatch business logic itself — that lives in <see cref="EmailDispatchJob"/>.
/// </summary>
public sealed class EmailDispatchSweepJob : IBackgroundJobHandler<EmailDispatchSweepJobArgs>
{
    private const int DefaultBatchSize = 50;
    private const int DefaultMaxRetryCount = 5;
    private const int MaxBatchSize = 500;

    private readonly INotificationDispatchRepository _dispatchRepository;
    private readonly IBackgroundJobScheduler _scheduler;
    private readonly ILogger<EmailDispatchSweepJob> _logger;

    public EmailDispatchSweepJob(
        INotificationDispatchRepository dispatchRepository,
        IBackgroundJobScheduler scheduler,
        ILogger<EmailDispatchSweepJob> logger)
    {
        _dispatchRepository = dispatchRepository;
        _scheduler = scheduler;
        _logger = logger;
    }

    public async Task HandleAsync(EmailDispatchSweepJobArgs args, BackgroundJobContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(context);

        var batchSize = args.BatchSize <= 0 ? DefaultBatchSize : Math.Min(args.BatchSize, MaxBatchSize);
        var maxRetryCount = args.MaxRetryCount <= 0 ? DefaultMaxRetryCount : args.MaxRetryCount;
        var asOfUtc = DateTimeOffset.UtcNow;

        IReadOnlyList<NotificationDispatchRetryHandle> dueHandles;
        try
        {
            dueHandles = await _dispatchRepository.FindDueRetriesAsync(asOfUtc, maxRetryCount, batchSize, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "email.dispatch.sweep.query_failed ExceptionType={ExceptionType} CorrelationId={CorrelationId}",
                ex.GetType().Name,
                context.EffectiveCorrelationId);
            return;
        }

        if (dueHandles.Count == 0)
        {
            return;
        }

        var enqueued = 0;
        foreach (var handle in dueHandles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var jobContext = new BackgroundJobContext(
                    CorrelationId: context.CorrelationId,
                    CausationId: context.EventId,
                    TenantId: handle.TenantId,
                    TriggerType: BackgroundJobTriggerTypes.Recurring,
                    TriggeredBy: nameof(EmailDispatchSweepJob));

                await _scheduler.EnqueueAsync<EmailDispatchJobArgs, EmailDispatchJob>(
                    new EmailDispatchJobArgs(handle.TenantId, handle.DispatchId),
                    jobContext,
                    cancellationToken);

                enqueued++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "email.dispatch.sweep.enqueue_failed DispatchId={DispatchId} TenantId={TenantId} ExceptionType={ExceptionType}",
                    handle.DispatchId,
                    handle.TenantId,
                    ex.GetType().Name);
            }
        }

        _logger.LogInformation(
            "email.dispatch.sweep.completed Due={Due} Enqueued={Enqueued} BatchSize={BatchSize} MaxRetryCount={MaxRetryCount} CorrelationId={CorrelationId}",
            dueHandles.Count,
            enqueued,
            batchSize,
            maxRetryCount,
            context.EffectiveCorrelationId);
    }
}
