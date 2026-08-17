using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.BackgroundJobs;
using Diten.Platform.Domain.Entities;
using Microsoft.Extensions.Options;
using Prometheus;

namespace Diten.Platform.API.Observability;

public sealed class BackgroundJobExecutionLogMetricsDecorator : IJobExecutionLogWriter
{
    private static readonly Counter JobStarted = Metrics.CreateCounter(
        "background_job_started",
        "Background job execution attempts started.",
        new CounterConfiguration { LabelNames = new[] { "service", "environment", "job_name" } });

    private static readonly Counter JobSucceeded = Metrics.CreateCounter(
        "background_job_succeeded",
        "Background job execution attempts succeeded.",
        new CounterConfiguration { LabelNames = new[] { "service", "environment", "job_name" } });

    private static readonly Counter JobFailed = Metrics.CreateCounter(
        "background_job_failed",
        "Background job execution attempts failed.",
        new CounterConfiguration { LabelNames = new[] { "service", "environment", "job_name" } });

    private static readonly Counter JobRetried = Metrics.CreateCounter(
        "background_job_retried",
        "Background job execution attempts observed with retry metadata.",
        new CounterConfiguration { LabelNames = new[] { "service", "environment", "job_name" } });

    private static readonly Histogram JobDuration = Metrics.CreateHistogram(
        "background_job_duration_seconds",
        "Background job execution duration in seconds.",
        new HistogramConfiguration
        {
            LabelNames = new[] { "service", "environment", "job_name", "result" },
            Buckets = Histogram.ExponentialBuckets(0.005, 2, 12)
        });

    private readonly IJobExecutionLogWriter _inner;
    private readonly Diten.Platform.Common.Observability.ObservabilityOptions _options;
    private readonly ILogger<BackgroundJobExecutionLogMetricsDecorator> _logger;

    public BackgroundJobExecutionLogMetricsDecorator(
        IJobExecutionLogWriter inner,
        IOptions<Diten.Platform.Common.Observability.ObservabilityOptions> options,
        ILogger<BackgroundJobExecutionLogMetricsDecorator> logger)
    {
        _inner = inner;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<JobExecutionLog> StartedAsync(
        BackgroundJobDescriptor descriptor,
        BackgroundJobContext context,
        string? jobId = null,
        CancellationToken cancellationToken = default)
    {
        var startedLog = await _inner.StartedAsync(descriptor, context, jobId, cancellationToken);
        var labels = GetJobLabels(startedLog);
        JobStarted.WithLabels(labels).Inc();

        _logger.LogInformation(
            "background_job.started JobName={JobName} JobId={JobId} Operation={Operation} Result={Result} CorrelationId={CorrelationId}",
            startedLog.JobName,
            startedLog.JobId,
            "execute",
            "started",
            startedLog.CorrelationId);

        return startedLog;
    }

    public async Task<JobExecutionLog?> SucceededAsync(
        JobExecutionLog startedLog,
        DateTimeOffset finishedAt,
        CancellationToken cancellationToken = default)
    {
        var succeededLog = await _inner.SucceededAsync(startedLog, finishedAt, cancellationToken);
        var observedLog = succeededLog ?? startedLog;
        var labels = GetJobLabels(observedLog);
        var duration = ResolveDurationSeconds(observedLog, finishedAt);

        JobSucceeded.WithLabels(labels).Inc();
        JobDuration
            .WithLabels(_options.ServiceName, ResolveEnvironment(), observedLog.JobName, "succeeded")
            .Observe(duration);

        _logger.LogInformation(
            "background_job.succeeded JobName={JobName} JobId={JobId} Operation={Operation} Result={Result} DurationMs={DurationMs} CorrelationId={CorrelationId}",
            observedLog.JobName,
            observedLog.JobId,
            "execute",
            "succeeded",
            ResolveDurationMilliseconds(observedLog, finishedAt),
            observedLog.CorrelationId);

        return succeededLog;
    }

    public async Task<JobExecutionLog?> FailedAsync(
        JobExecutionLog startedLog,
        Exception exception,
        int retryCount,
        DateTimeOffset finishedAt,
        CancellationToken cancellationToken = default)
    {
        var failedLog = await _inner.FailedAsync(startedLog, exception, retryCount, finishedAt, cancellationToken);
        var observedLog = failedLog ?? startedLog;
        var labels = GetJobLabels(observedLog);
        var duration = ResolveDurationSeconds(observedLog, finishedAt);

        JobFailed.WithLabels(labels).Inc();
        if (retryCount > 0)
        {
            JobRetried.WithLabels(labels).Inc();
        }

        JobDuration
            .WithLabels(_options.ServiceName, ResolveEnvironment(), observedLog.JobName, "failed")
            .Observe(duration);

        _logger.LogWarning(
            "background_job.failed JobName={JobName} JobId={JobId} Operation={Operation} Result={Result} DurationMs={DurationMs} RetryCount={RetryCount} CorrelationId={CorrelationId} ErrorType={ErrorType}",
            observedLog.JobName,
            observedLog.JobId,
            "execute",
            "failed",
            ResolveDurationMilliseconds(observedLog, finishedAt),
            Math.Max(0, retryCount),
            observedLog.CorrelationId,
            exception.GetType().Name);

        return failedLog;
    }

    private string[] GetJobLabels(JobExecutionLog log)
    {
        return new[] { _options.ServiceName, ResolveEnvironment(), log.JobName };
    }

    private string ResolveEnvironment()
    {
        return string.IsNullOrWhiteSpace(_options.Environment)
            ? "Unknown"
            : _options.Environment;
    }

    private static double ResolveDurationSeconds(JobExecutionLog log, DateTimeOffset finishedAt)
    {
        return ResolveDurationMilliseconds(log, finishedAt) / 1000d;
    }

    private static long ResolveDurationMilliseconds(JobExecutionLog log, DateTimeOffset finishedAt)
    {
        return Math.Max(0, log.DurationMs ?? (long)(finishedAt - log.StartedAt).TotalMilliseconds);
    }
}
