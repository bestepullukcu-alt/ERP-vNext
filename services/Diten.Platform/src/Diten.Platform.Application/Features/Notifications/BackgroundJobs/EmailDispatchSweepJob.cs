using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Features.Notifications.Commands;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.Notifications.BackgroundJobs;

/// <summary>
/// Recurring sweep that finds dispatches whose retry is due and enqueues a per-dispatch
/// targeted <see cref="EmailDispatchJob"/> through the MOD-0026 scheduler abstraction.
/// Owns no per-dispatch business logic itself — that lives in <see cref="EmailDispatchJob"/>.
///
/// <para>BL-454 — it also CLOSES what can no longer be retried: a dispatch still waiting (Queued or Failed, not yet
/// permanent) whose retry window (<see cref="EmailDispatchRetentionOptions.RetryWindowHours"/>) has passed is marked
/// a permanent failure through the same command a last failed retry uses, and its variables are released in that same
/// write. That covers the rows no retry would ever pick up: a process that died between the queue write and the first
/// send, and a row whose retry count is already at a since-lowered maximum.</para>
/// </summary>
public sealed class EmailDispatchSweepJob : IBackgroundJobHandler<EmailDispatchSweepJobArgs>
{
    /// <summary>The job's EnabledJobs configuration key (PlatformRecurringJobRegistrar registers it under this id).</summary>
    public const string JobId = "Diten.Platform.MOD-0027.EmailDispatchJob";

    /// <summary>The error code a dispatch closed by the retry window carries.</summary>
    public const string RetryWindowExpiredCode = "RetryWindowExpired";

    private const int DefaultBatchSize = 50;
    private const int DefaultMaxRetryCount = 5;
    private const int MaxBatchSize = 500;

    private readonly INotificationDispatchRepository _dispatchRepository;
    private readonly IBackgroundJobScheduler _scheduler;
    private readonly ILogger<EmailDispatchSweepJob> _logger;
    // BL-454 — trailing and OPTIONAL, the EmailDispatchJob precedent: both are registered in DI, so production always
    // has them. A sweep built the old 3-argument way (existing tests) retries as before and closes nothing.
    private readonly IMediator? _mediator;
    private readonly EmailDispatchRetentionOptions _retention;

    public EmailDispatchSweepJob(
        INotificationDispatchRepository dispatchRepository,
        IBackgroundJobScheduler scheduler,
        ILogger<EmailDispatchSweepJob> logger,
        IMediator? mediator = null,
        IOptions<EmailDispatchRetentionOptions>? retention = null)
    {
        _dispatchRepository = dispatchRepository;
        _scheduler = scheduler;
        _logger = logger;
        _mediator = mediator;
        _retention = retention?.Value ?? new EmailDispatchRetentionOptions();
    }

    /// <summary>
    /// BL-454 — does THIS server ever retry a failed e-mail? The scheduler is on, the standard jobs are registered and
    /// this job's own EnabledJobs flag is on — the same three gates <c>PlatformRecurringJobRegistrar</c> and the
    /// scheduler apply (the TimesheetReminderJob.IsScheduled precedent). When it is false, a failed first send is never
    /// tried again, so QueueEmailNotificationHandler keeps no variables and calls the first failure permanent.
    /// </summary>
    public static bool IsScheduled(BackgroundJobSchedulerOptions options)
        => options.Enabled
           && options.RegisterStandardJobs
           && options.EnabledJobs.TryGetValue(JobId, out var enabled)
           && enabled;

    public async Task HandleAsync(EmailDispatchSweepJobArgs args, BackgroundJobContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(context);

        var batchSize = args.BatchSize <= 0 ? DefaultBatchSize : Math.Min(args.BatchSize, MaxBatchSize);
        var maxRetryCount = args.MaxRetryCount <= 0 ? DefaultMaxRetryCount : args.MaxRetryCount;
        var asOfUtc = DateTimeOffset.UtcNow;

        // First close what the window has given up on, so the same pass never also enqueues a retry for it.
        await CloseExpiredAsync(asOfUtc, batchSize, context, cancellationToken);

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
                    new EmailDispatchJobArgs(handle.TenantId, handle.DispatchId, maxRetryCount),
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

    private async Task CloseExpiredAsync(DateTimeOffset asOfUtc, int batchSize, BackgroundJobContext context, CancellationToken ct)
    {
        if (_mediator is null)
        {
            return;
        }

        var cutoff = asOfUtc - TimeSpan.FromHours(Math.Max(1, _retention.RetryWindowHours));
        IReadOnlyList<NotificationDispatchRetryHandle> expired;
        try
        {
            expired = await _dispatchRepository.FindRetryWindowExpiredAsync(cutoff, batchSize, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                "email.dispatch.sweep.expiry_query_failed ExceptionType={ExceptionType} CorrelationId={CorrelationId}",
                ex.GetType().Name,
                context.EffectiveCorrelationId);
            return;
        }

        var closed = 0;
        foreach (var handle in expired)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var response = await _mediator.Send(
                    new MarkNotificationDispatchFailedCommand(
                        handle.TenantId,
                        handle.DispatchId,
                        RetryWindowExpiredCode,
                        "The retry window passed before the message could be sent.",
                        IsPermanentFailure: true),
                    ct);
                if (response?.IsSuccessful == true)
                {
                    closed++;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "email.dispatch.sweep.expiry_failed DispatchId={DispatchId} TenantId={TenantId} ExceptionType={ExceptionType}",
                    handle.DispatchId,
                    handle.TenantId,
                    ex.GetType().Name);
            }
        }

        if (expired.Count > 0)
        {
            _logger.LogInformation(
                "email.dispatch.sweep.expired Found={Found} Closed={Closed} RetryWindowHours={RetryWindowHours} CorrelationId={CorrelationId}",
                expired.Count,
                closed,
                _retention.RetryWindowHours,
                context.EffectiveCorrelationId);
        }
    }
}

/// <summary>
/// BL-454 — how long a dispatch may keep its variables while waiting for a retry (configuration section
/// <c>Notifications:EmailDispatch</c>). Longer than the whole retry schedule by design: the schedule is minutes, the
/// window is a day, so the window only ever closes rows the schedule has already lost.
/// </summary>
public sealed class EmailDispatchRetentionOptions
{
    public const string SectionName = "Notifications:EmailDispatch";

    public int RetryWindowHours { get; set; } = 24;
}
