using Diten.BuildingBlocks.BackgroundJobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.Workflow.BackgroundJobs;

/// <summary>
/// WP-WF-ENGINE-RECOVERY-01 FIX1 item 4 — THE GAUGES DO NOT DEPEND ON THE CLOSE BEING SWITCHED ON. The recovery job ships off;
/// while it is off this worker runs its count-only cycle (<see cref="WorkflowStartRecoverySweepJob.CountOnlyAsync"/>) every
/// <see cref="WorkflowStartRecoveryOptions.CountIntervalMinutes"/>: read-only, every tenant, the same gauges and cycle line.
/// When the job is on, the job records them and this worker stays idle (the MDM recovery's pattern: a hosted counter while
/// the worker is off).
/// </summary>
public sealed class WorkflowStartRecoveryCountingService(
    IServiceScopeFactory scopes,
    IOptions<BackgroundJobSchedulerOptions> scheduler,
    IOptions<WorkflowStartRecoveryOptions> options,
    ILogger<WorkflowStartRecoveryCountingService> logger) : BackgroundService
{
    public const string SweepJobId = "Diten.Platform.MOD-0023.WorkflowStartRecoverySweepJob";

    /// <summary>Whether the recovery job itself runs (and so records the gauges) — the registrar's own rule.</summary>
    public static bool SweepJobRuns(BackgroundJobSchedulerOptions scheduler) =>
        scheduler.Enabled && scheduler.RegisterStandardJobs
        && scheduler.EnabledJobs.TryGetValue(SweepJobId, out var enabled) && enabled;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMinutes(Math.Max(1, options.Value.CountIntervalMinutes));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                if (SweepJobRuns(scheduler.Value) || !options.Value.CountWhileSweepIsOff)
                {
                    continue;
                }

                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<WorkflowStartRecoverySweepJob>()
                    .CountOnlyAsync(Guid.NewGuid().ToString(), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning("workflow.start_recovery.count_failed ExceptionType={ExceptionType}", ex.GetType().Name);
            }
        }
    }
}
