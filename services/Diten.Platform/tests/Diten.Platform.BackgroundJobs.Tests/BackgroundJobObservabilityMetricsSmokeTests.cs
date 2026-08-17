using System.Text;
using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.API.Observability;
using Diten.Platform.Application.BackgroundJobs;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Diten.Platform.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Prometheus;
using Xunit;

namespace Diten.Platform.BackgroundJobs.Tests;

public sealed class BackgroundJobObservabilityMetricsSmokeTests
{
    [Fact]
    public async Task InternalOnlySmoke_ExposesBackgroundJobMetricFamilies_WithoutPublicEndpoint()
    {
        var correlationId = Guid.Parse("11111111-2222-3333-4444-555555555555");
        const string jobId = "job-raw-id-proof";
        const string tenantId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
        const string payloadMarker = "payload-secret-proof";

        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<Diten.Platform.Common.Observability.ObservabilityOptions>(options =>
        {
            options.ServiceName = "Diten.Platform.Tests";
            options.Environment = "Test";
        });
        services.AddSingleton<IJobExecutionLogRepository, InMemoryJobExecutionLogRepository>();
        services.AddScoped<IJobExecutionLogWriter, JobExecutionLogWriter>();
        services.AddScoped<SchedulerSmokeTestJob>();
        services.AddScoped<HangfireBackgroundJobExecutor>();
        services.AddBackgroundJobObservabilityMetrics();

        await using var provider = services.BuildServiceProvider();
        var executor = provider.GetRequiredService<HangfireBackgroundJobExecutor>();
        var descriptor = new BackgroundJobDescriptor(
            "Diten.Platform.SchedulerSmokeTestJob",
            "Diten.Platform",
            "SchedulerSmokeTestJob",
            "MOD-0026",
            IsEnabled: true,
            TriggerType: BackgroundJobTriggerTypes.Manual);
        var context = new BackgroundJobContext(
            CorrelationId: correlationId,
            TenantId: Guid.Parse(tenantId),
            TriggerType: BackgroundJobTriggerTypes.Manual,
            Metadata: new Dictionary<string, string>
            {
                ["payload"] = $$"""{"marker":"{{payloadMarker}}"}"""
            });

        await executor.ExecuteAsync(
            typeof(SchedulerSmokeTestJob).AssemblyQualifiedName!,
            typeof(SchedulerSmokeTestJobArgs).AssemblyQualifiedName!,
            $$"""{"shouldFail":false,"message":"{{payloadMarker}}"}""",
            descriptor,
            context);

        await Assert.ThrowsAsync<InvalidOperationException>(() => executor.ExecuteAsync(
            typeof(SchedulerSmokeTestJob).AssemblyQualifiedName!,
            typeof(SchedulerSmokeTestJobArgs).AssemblyQualifiedName!,
            $$"""{"shouldFail":true,"message":"{{payloadMarker}}"}""",
            descriptor,
            context with { RetryCount = 2 }));

        var writer = provider.GetRequiredService<IJobExecutionLogWriter>();
        var retryStarted = await writer.StartedAsync(descriptor, context, jobId);
        await writer.FailedAsync(
            retryStarted,
            new InvalidOperationException("password=secret token=abc connectionString=mongodb://user:pass@localhost payload={\"x\":1}"),
            retryCount: 2,
            finishedAt: DateTimeOffset.UtcNow.AddMilliseconds(4));

        var metrics = await ExportMetricsAsync();
        var repository = provider.GetRequiredService<IJobExecutionLogRepository>();
        var logs = await repository.GetByCorrelationIdAsync(correlationId);
        var succeeded = Assert.Single(logs, log => log.Status == JobExecutionStatus.Succeeded);
        var failed = Assert.Single(logs, log => log.Status == JobExecutionStatus.Failed && log.RetryCount == 2);

        Assert.Equal(correlationId, succeeded!.CorrelationId);
        Assert.Equal(correlationId, failed!.CorrelationId);
        Assert.Equal(JobExecutionStatus.Succeeded, succeeded.Status);
        Assert.Equal(JobExecutionStatus.Failed, failed.Status);
        Assert.Equal(2, failed.RetryCount);

        Assert.Contains("background_job_started", metrics);
        Assert.Contains("background_job_succeeded", metrics);
        Assert.Contains("background_job_failed", metrics);
        Assert.Contains("background_job_retried", metrics);
        Assert.Contains("background_job_duration_seconds", metrics);
        Assert.Contains("job_name=\"SchedulerSmokeTestJob\"", metrics);
        Assert.Contains("result=\"succeeded\"", metrics);
        Assert.Contains("result=\"failed\"", metrics);

        Assert.DoesNotContain(correlationId.ToString(), metrics, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(jobId, metrics, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(tenantId, metrics, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(payloadMarker, metrics, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("mongodb://user:pass", metrics, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", metrics, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", metrics, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("connectionString", metrics, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string> ExportMetricsAsync()
    {
        await using var stream = new MemoryStream();
        await Metrics.DefaultRegistry.CollectAndExportAsTextAsync(
            stream,
            ExpositionFormat.PrometheusText,
            CancellationToken.None);
        return Encoding.UTF8.GetString(stream.ToArray());
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
