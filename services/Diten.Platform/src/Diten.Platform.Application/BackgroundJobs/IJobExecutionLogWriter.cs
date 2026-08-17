using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Application.BackgroundJobs;

public interface IJobExecutionLogWriter
{
    Task<JobExecutionLog> StartedAsync(
        BackgroundJobDescriptor descriptor,
        BackgroundJobContext context,
        string? jobId = null,
        CancellationToken cancellationToken = default);

    Task<JobExecutionLog?> SucceededAsync(
        JobExecutionLog startedLog,
        DateTimeOffset finishedAt,
        CancellationToken cancellationToken = default);

    Task<JobExecutionLog?> FailedAsync(
        JobExecutionLog startedLog,
        Exception exception,
        int retryCount,
        DateTimeOffset finishedAt,
        CancellationToken cancellationToken = default);
}
