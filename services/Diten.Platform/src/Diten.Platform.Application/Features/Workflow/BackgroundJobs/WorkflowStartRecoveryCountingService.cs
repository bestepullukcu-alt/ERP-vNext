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
/// <para>FIX2 item 4 — bounded: the first count runs one interval after start (a restarted fleet does not count all at
/// once), the interval is held to 5 minutes – 24 hours, and each count is a server-side count capped per tenant. There is no
/// single-replica coordination: every Platform replica counts on its own, so the cost multiplies by the replica count — the
/// runbook says how to keep it to one.</para>
/// </summary>
public sealed class WorkflowStartRecoveryCountingService(
    IServiceScopeFactory scopes,
    IOptions<BackgroundJobSchedulerOptions> scheduler,
    IOptions<WorkflowStartRecoveryOptions> options,
    ILogger<WorkflowStartRecoveryCountingService> logger,
    Func<TimeSpan, CancellationToken, Task>? delay = null) : BackgroundService
{
    public const string SweepJobId = "Diten.Platform.MOD-0023.WorkflowStartRecoverySweepJob";

    private readonly Func<TimeSpan, CancellationToken, Task> _delay = delay ?? Task.Delay;

    /// <summary>What one turn of the worker did.</summary>
    public enum Turn
    {
        Counted,
        SkippedJobRuns,
        SkippedSwitchedOff,
        Failed
    }

    /// <summary>Whether the recovery job itself runs (and so records the gauges) — the registrar's own rule.</summary>
    public static bool SweepJobRuns(BackgroundJobSchedulerOptions scheduler) =>
        scheduler.Enabled && scheduler.RegisterStandardJobs
        && scheduler.EnabledJobs.TryGetValue(SweepJobId, out var enabled) && enabled;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = WorkflowStartRecoveryOptions.CountIntervalOf(options.Value);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _delay(interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }

            await RunOnceAsync(stoppingToken);
        }
    }

    /// <summary>One turn: count, unless the job runs (it records the gauges itself) or the count is switched off. A failure
    /// is logged by its type only and the worker goes on.</summary>
    public async Task<Turn> RunOnceAsync(CancellationToken ct)
    {
        if (SweepJobRuns(scheduler.Value))
        {
            return Turn.SkippedJobRuns;
        }

        if (!options.Value.CountWhileSweepIsOff)
        {
            return Turn.SkippedSwitchedOff;
        }

        try
        {
            using var scope = scopes.CreateScope();
            await scope.ServiceProvider.GetRequiredService<WorkflowStartRecoverySweepJob>()
                .CountOnlyAsync(Guid.NewGuid().ToString(), ct);
            return Turn.Counted;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning("workflow.start_recovery.count_failed ExceptionType={ExceptionType}", ex.GetType().Name);
            return Turn.Failed;
        }
    }
}
