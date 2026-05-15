using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Domain.Repositories;

public interface IJobExecutionLogRepository
{
    Task<JobExecutionLog> CreateAsync(JobExecutionLog log, CancellationToken cancellationToken = default);
    Task<JobExecutionLog?> MarkSucceededAsync(Guid id, DateTimeOffset finishedAt, long durationMs, CancellationToken cancellationToken = default);
    Task<JobExecutionLog?> MarkFailedAsync(Guid id, DateTimeOffset finishedAt, long durationMs, string error, int retryCount, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobExecutionLog>> GetByServiceNameAsync(string serviceName, int limit = 100, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<JobExecutionLog>> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default);
}
