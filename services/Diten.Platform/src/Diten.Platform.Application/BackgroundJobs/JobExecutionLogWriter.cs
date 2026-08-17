using System.Text.RegularExpressions;
using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.BackgroundJobs;

public sealed partial class JobExecutionLogWriter : IJobExecutionLogWriter
{
    private const int MaxErrorLength = 4000;
    private readonly IJobExecutionLogRepository _repository;

    public JobExecutionLogWriter(IJobExecutionLogRepository repository)
    {
        _repository = repository;
    }

    public Task<JobExecutionLog> StartedAsync(
        BackgroundJobDescriptor descriptor,
        BackgroundJobContext context,
        string? jobId = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var log = new JobExecutionLog
        {
            TenantId = context.TenantId,
            ServiceName = Normalize(descriptor.ServiceName, 128),
            JobName = Normalize(descriptor.JobName, 200),
            JobId = NormalizeNullable(jobId, 128),
            RecurringJobId = NormalizeNullable(descriptor.Id, 200),
            CorrelationId = context.CorrelationId ?? Guid.NewGuid(),
            CausationId = context.CausationId,
            StartedAt = now,
            RetryCount = Math.Max(0, context.RetryCount),
            TriggerType = Normalize(context.TriggerType, 64),
            TriggeredBy = NormalizeNullable(context.TriggeredBy, 200),
            EventName = NormalizeNullable(context.EventName, 200),
            EventId = context.EventId,
            Metadata = RedactMetadata(context.Metadata)
        };

        return _repository.CreateAsync(log, cancellationToken);
    }

    public Task<JobExecutionLog?> SucceededAsync(
        JobExecutionLog startedLog,
        DateTimeOffset finishedAt,
        CancellationToken cancellationToken = default)
    {
        var durationMs = ComputeDurationMs(startedLog.StartedAt, finishedAt);
        return _repository.MarkSucceededAsync(startedLog.Id, finishedAt, durationMs, cancellationToken);
    }

    public Task<JobExecutionLog?> FailedAsync(
        JobExecutionLog startedLog,
        Exception exception,
        int retryCount,
        DateTimeOffset finishedAt,
        CancellationToken cancellationToken = default)
    {
        var durationMs = ComputeDurationMs(startedLog.StartedAt, finishedAt);
        return _repository.MarkFailedAsync(
            startedLog.Id,
            finishedAt,
            durationMs,
            RedactError(exception.ToString()),
            Math.Max(0, retryCount),
            cancellationToken);
    }

    public static string RedactError(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return string.Empty;
        }

        var redacted = SecretPattern().Replace(error, "$1=[REDACTED]");
        redacted = ConnectionStringPattern().Replace(redacted, "$1=[REDACTED]");
        redacted = PayloadPattern().Replace(redacted, "$1=[REDACTED]");
        return redacted.Length <= MaxErrorLength ? redacted : redacted[..MaxErrorLength];
    }

    private static long ComputeDurationMs(DateTimeOffset startedAt, DateTimeOffset finishedAt)
    {
        return Math.Max(0, (long)(finishedAt - startedAt).TotalMilliseconds);
    }

    private static Dictionary<string, string>? RedactMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return null;
        }

        return metadata
            .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
            .Take(20)
            .ToDictionary(
                pair => Normalize(pair.Key, 128),
                pair => RedactError(Normalize(pair.Value, 500)),
                StringComparer.OrdinalIgnoreCase);
    }

    private static string Normalize(string value, int maxLength)
    {
        var normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string? NormalizeNullable(string? value, int maxLength)
    {
        return string.IsNullOrWhiteSpace(value) ? null : Normalize(value, maxLength);
    }

    [GeneratedRegex("(?i)\\b(password|pwd|secret|token|credential|api[_-]?key)\\s*=\\s*[^;\\s,}\\]]+")]
    private static partial Regex SecretPattern();

    [GeneratedRegex("(?i)\\b(connectionstring|connection string|mongodb|server|user id|uid)\\s*=\\s*[^;\\r\\n]+")]
    private static partial Regex ConnectionStringPattern();

    [GeneratedRegex("(?i)\\b(payload|entity|body)\\s*=\\s*\\{[^\\r\\n]*\\}")]
    private static partial Regex PayloadPattern();
}
