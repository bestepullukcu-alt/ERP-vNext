using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.BackgroundJobs;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Xunit;

namespace Diten.Platform.BackgroundJobs.Tests;

public sealed class JobExecutionLogWriterTests
{
    [Fact]
    public async Task Writer_records_started_succeeded_and_failed_transitions()
    {
        var repository = new InMemoryJobExecutionLogRepository();
        var writer = new JobExecutionLogWriter(repository);
        var descriptor = new BackgroundJobDescriptor(
            "Diten.Platform.SchedulerSmokeTestJob",
            "Diten.Platform",
            "SchedulerSmokeTestJob",
            "MOD-0026",
            IsEnabled: true,
            TriggerType: BackgroundJobTriggerTypes.Manual);
        var context = new BackgroundJobContext(TriggerType: BackgroundJobTriggerTypes.Manual);

        var started = await writer.StartedAsync(descriptor, context, "job-1");
        var succeeded = await writer.SucceededAsync(started, DateTimeOffset.UtcNow.AddMilliseconds(3));
        var failedStart = await writer.StartedAsync(descriptor, context, "job-2");
        var failed = await writer.FailedAsync(
            failedStart,
            new InvalidOperationException("password=secret token=abc connectionString=mongodb://user:pass@localhost payload={\"x\":1}"),
            2,
            DateTimeOffset.UtcNow.AddMilliseconds(5));

        Assert.Equal(JobExecutionStatus.Succeeded, succeeded!.Status);
        Assert.True(succeeded.DurationMs >= 0);
        Assert.NotEqual(Guid.Empty, succeeded.CorrelationId);
        Assert.Equal(JobExecutionStatus.Failed, failed!.Status);
        Assert.Equal(2, failed.RetryCount);
        Assert.DoesNotContain("secret", failed.Error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("abc", failed.Error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mongodb://user:pass", failed.Error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("{\"x\":1}", failed.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Writer_queries_by_service_name()
    {
        var repository = new InMemoryJobExecutionLogRepository();
        var writer = new JobExecutionLogWriter(repository);
        var descriptor = new BackgroundJobDescriptor(
            "Diten.Platform.SchedulerSmokeTestJob",
            "Diten.Platform",
            "SchedulerSmokeTestJob",
            "MOD-0026");

        await writer.StartedAsync(descriptor, new BackgroundJobContext());

        var logs = await repository.GetByServiceNameAsync("Diten.Platform");

        Assert.Single(logs);
        Assert.Equal("SchedulerSmokeTestJob", logs[0].JobName);
    }

    private sealed class InMemoryJobExecutionLogRepository : IJobExecutionLogRepository
    {
        private readonly List<JobExecutionLog> _logs = [];

        public Task<JobExecutionLog> CreateAsync(JobExecutionLog log, CancellationToken cancellationToken = default)
        {
            _logs.Add(log);
            return Task.FromResult(log);
        }

        public Task<JobExecutionLog?> MarkSucceededAsync(Guid id, DateTimeOffset finishedAt, long durationMs, CancellationToken cancellationToken = default)
        {
            var log = _logs.SingleOrDefault(item => item.Id == id);
            if (log is not null)
            {
                log.Status = JobExecutionStatus.Succeeded;
                log.FinishedAt = finishedAt;
                log.DurationMs = durationMs;
            }

            return Task.FromResult(log);
        }

        public Task<JobExecutionLog?> MarkFailedAsync(Guid id, DateTimeOffset finishedAt, long durationMs, string error, int retryCount, CancellationToken cancellationToken = default)
        {
            var log = _logs.SingleOrDefault(item => item.Id == id);
            if (log is not null)
            {
                log.Status = JobExecutionStatus.Failed;
                log.FinishedAt = finishedAt;
                log.DurationMs = durationMs;
                log.Error = error;
                log.RetryCount = retryCount;
            }

            return Task.FromResult(log);
        }

        public Task<IReadOnlyList<JobExecutionLog>> GetByServiceNameAsync(string serviceName, int limit = 100, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<JobExecutionLog>>(
                _logs.Where(log => log.ServiceName == serviceName).Take(limit).ToList());
        }

        public Task<IReadOnlyList<JobExecutionLog>> GetByCorrelationIdAsync(Guid correlationId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<JobExecutionLog>>(
                _logs.Where(log => log.CorrelationId == correlationId).ToList());
        }
    }
}
