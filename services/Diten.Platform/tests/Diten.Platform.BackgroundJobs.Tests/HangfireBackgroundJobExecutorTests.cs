using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.BackgroundJobs;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Diten.Platform.BackgroundJobs.Tests;

public sealed class HangfireBackgroundJobExecutorTests
{
    [Fact]
    public async Task Executor_records_success_and_failure_logs_through_test_harness()
    {
        var repository = new InMemoryJobExecutionLogRepository();
        var services = new ServiceCollection()
            .AddLogging(builder => builder.AddDebug())
            .AddSingleton<IJobExecutionLogRepository>(repository)
            .AddScoped<IJobExecutionLogWriter, JobExecutionLogWriter>()
            .AddScoped<SchedulerSmokeTestJob>()
            .AddScoped<HangfireBackgroundJobExecutor>()
            .BuildServiceProvider();
        var executor = services.GetRequiredService<HangfireBackgroundJobExecutor>();
        var descriptor = new BackgroundJobDescriptor(
            "Diten.Platform.SchedulerSmokeTestJob",
            "Diten.Platform",
            "SchedulerSmokeTestJob",
            "MOD-0026",
            IsEnabled: true,
            TriggerType: BackgroundJobTriggerTypes.Manual);

        await executor.ExecuteAsync(
            typeof(SchedulerSmokeTestJob).AssemblyQualifiedName!,
            typeof(SchedulerSmokeTestJobArgs).AssemblyQualifiedName!,
            """{"shouldFail":false,"message":"ok"}""",
            descriptor,
            new BackgroundJobContext(TriggerType: BackgroundJobTriggerTypes.Manual, TriggeredBy: "test-harness"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(
            typeof(SchedulerSmokeTestJob).AssemblyQualifiedName!,
            typeof(SchedulerSmokeTestJobArgs).AssemblyQualifiedName!,
            """{"shouldFail":true,"message":"fail"}""",
            descriptor,
            new BackgroundJobContext(TriggerType: BackgroundJobTriggerTypes.Manual, TriggeredBy: "test-harness")));

        var logs = await repository.GetByServiceNameAsync("Diten.Platform");

        Assert.Contains(logs, log =>
            log.JobName == "SchedulerSmokeTestJob"
            && log.Status == JobExecutionStatus.Succeeded
            && log.FinishedAt is not null
            && log.DurationMs >= 0
            && log.CorrelationId != Guid.Empty);
        var failed = Assert.Single(logs.Where(log => log.Status == JobExecutionStatus.Failed));
        Assert.DoesNotContain("demo-secret", failed.Error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("demo-token", failed.Error, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mongodb://user:pass", failed.Error, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, failed.RetryCount);
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
