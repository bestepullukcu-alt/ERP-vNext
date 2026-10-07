using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Application.Features.Workflow.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.Platform.Application.Features.Workflow.BackgroundJobs;

/// <summary>
/// BL-549 + BL-539 — THE ENGINE ENDS WHAT IT LEFT HALF-DONE. Each cycle, tenant by tenant (TenantScope.Begin, as the SLA
/// sweep):
/// <list type="number">
/// <item>generic starts that crashed after writing their instance — Active, no task at all, not moved for
/// <see cref="WorkflowStartRecoveryOptions.AbandonAfterMinutes"/> — are closed through
/// <see cref="CloseAbandonedWorkflowStartCommand"/> (the engine's terminal write: reason, log, completion event, audit row).
/// A start whose key is replayed before that completes instead (StartWorkflowInstanceHandler); an instance with a task is
/// never touched;</item>
/// <item>trusted reservations that never completed — Pending short of Completed, not moved for the same age — are closed
/// through the same command (the engine's own close of a reservation: reason, stray task withdrawn, log, audit row); a replay
/// that advances one meanwhile wins the race (fenced on checkpoint and version), and the closed key then reads as not found;</item>
/// <item>the counts go to <see cref="WorkflowStartRecoveryMetrics"/>, one cycle line is written, and an instance the cycle
/// could not close is named once, with its reason.</item>
/// </list>
/// Registered OFF (PlatformRecurringJobRegistrar): it runs only where the operator enables its id.
/// </summary>
public sealed class WorkflowStartRecoverySweepJob : IBackgroundJobHandler<WorkflowStartRecoverySweepJobArgs>
{
    private readonly ITenantRegistryRepository _tenantRegistry;
    private readonly ITenantContext _tenantContext;
    private readonly IWorkflowStartRecoveryStore _store;
    private readonly IMediator _mediator;
    private readonly WorkflowStartRecoveryMetrics _metrics;
    private readonly WorkflowStartRecoveryOptions _options;
    private readonly ILogger<WorkflowStartRecoverySweepJob> _logger;

    public WorkflowStartRecoverySweepJob(
        ITenantRegistryRepository tenantRegistry,
        ITenantContext tenantContext,
        IWorkflowStartRecoveryStore store,
        IMediator mediator,
        WorkflowStartRecoveryMetrics metrics,
        ILogger<WorkflowStartRecoverySweepJob> logger,
        IOptions<WorkflowStartRecoveryOptions>? options = null)
    {
        _tenantRegistry = tenantRegistry;
        _tenantContext = tenantContext;
        _store = store;
        _mediator = mediator;
        _metrics = metrics;
        _logger = logger;
        _options = options?.Value ?? new WorkflowStartRecoveryOptions();
    }

    public Task HandleAsync(
        WorkflowStartRecoverySweepJobArgs args,
        BackgroundJobContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(context);
        var maxCloses = args.MaxClosesPerTenant > 0 ? args.MaxClosesPerTenant : Math.Max(1, _options.MaxClosesPerTenant);
        return RunCycleAsync(close: true, maxCloses, context.EffectiveCorrelationId.ToString(), cancellationToken);
    }

    /// <summary>
    /// FIX1 item 4 — THE COUNT WITHOUT THE CLOSE. While the job is off (its default) the gauges are still fed: this cycle
    /// reads and records exactly what the job would have found, and closes nothing
    /// (<see cref="WorkflowStartRecoveryCountingService"/> runs it).
    /// </summary>
    public Task CountOnlyAsync(string correlationId, CancellationToken cancellationToken = default) =>
        RunCycleAsync(close: false, Math.Max(1, _options.MaxClosesPerTenant), correlationId, cancellationToken);

    private async Task RunCycleAsync(bool close, int maxCloses, string correlationId, CancellationToken cancellationToken)
    {
        var abandonAfter = WorkflowStartRecoveryOptions.AbandonAfterOf(_options);
        var tenants = await _tenantRegistry.GetActiveTenantsAsync(cancellationToken);
        var cycle = new Cycle();

        foreach (var tenant in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using (TenantScope.Begin(_tenantContext, tenant.Id))
                {
                    await RunTenantAsync(tenant.Id, close, abandonAfter, maxCloses, correlationId, cycle, cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                cycle.FailedTenants++;
                _logger.LogWarning(
                    ex,
                    "workflow.start_recovery.tenant_failed TenantId={TenantId} ExceptionType={ExceptionType} CorrelationId={CorrelationId}",
                    tenant.Id, ex.GetType().Name, correlationId);
            }
        }

        var level = cycle.Stuck > 0 || cycle.FailedTenants > 0 ? LogLevel.Warning
            : cycle.Abandoned == 0 && cycle.IncompleteTrusted == 0 ? LogLevel.Debug
            : LogLevel.Information;
        _logger.Log(
            level,
            "workflow.start_recovery.cycle Mode={Mode} Tenants={Tenants} Abandoned={Abandoned} Closed={Closed} Raced={Raced} "
            + "Deferred={Deferred} Stuck={Stuck} IncompleteTrusted={IncompleteTrusted} FailedTenants={FailedTenants} "
            + "AbandonAfterMinutes={AbandonAfterMinutes} CorrelationId={CorrelationId}",
            close ? "close" : "count-only", tenants.Count, cycle.Abandoned, cycle.Closed, cycle.Raced, cycle.Deferred, cycle.Stuck,
            cycle.IncompleteTrusted, cycle.FailedTenants, (int)abandonAfter.TotalMinutes, correlationId);
    }

    private async Task RunTenantAsync(
        Guid tenantId,
        bool close,
        TimeSpan abandonAfter,
        int maxCloses,
        string correlationId,
        Cycle cycle,
        CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow - abandonAfter;
        var (abandonedPage, abandoned) = await _store.FindAbandonedGenericStartsAsync(cutoff, maxCloses, ct);
        var incompleteTrusted = await _store.CountIncompleteTrustedStartsAsync(cutoff, ct);
        var stuck = 0L;

        if (close)
        {
            var candidates = abandonedPage.Concat(await _store.ListIncompleteTrustedStartsAsync(cutoff, maxCloses, ct));
            foreach (var instance in candidates)
            {
                string? failure;
                try
                {
                    var closed = await _mediator.Send(new CloseAbandonedWorkflowStartCommand(instance.Id, abandonAfter, correlationId), ct);
                    if (closed.IsSuccessful)
                    {
                        cycle.Closed++;
                        _metrics.Unstuck(instance.Id);
                        continue;
                    }

                    // FIX1 item 4 — only a real failure is "stuck". A start that moved between the read and the close (a replay
                    // completed it, it is not abandoned any more) and a close that lost its versioned race are the recovery
                    // working as designed; a store that was unavailable is retried next cycle. Neither is named at Error.
                    if (closed.ReasonCode == WorkflowReasonCodes.WorkflowStartNotAbandoned)
                    {
                        continue;
                    }

                    if (closed.StatusCode == 409 && closed.ReasonCode == WorkflowReasonCodes.WorkflowTransitionConflict)
                    {
                        cycle.Raced++;
                        _logger.LogInformation(
                            "workflow.start_recovery.raced Instance {InstanceId} of tenant {TenantId} moved while it was being closed; left to the replay.",
                            instance.Id, tenantId);
                        continue;
                    }

                    if (closed.StatusCode == 503)
                    {
                        cycle.Deferred++;
                        _logger.LogInformation(
                            "workflow.start_recovery.deferred Instance {InstanceId} of tenant {TenantId} could not be written now; next cycle.",
                            instance.Id, tenantId);
                        continue;
                    }

                    failure = closed.ReasonCode ?? closed.StatusCode.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failure = ex.GetType().Name;
                }

                stuck++;
                if (_metrics.FirstStuckReport(instance.Id))
                {
                    _logger.LogError(
                        "workflow.start_recovery.stuck Instance {InstanceId} of tenant {TenantId} could not be closed ({ReasonCode}); it stays open with nothing to decide.",
                        instance.Id, tenantId, failure);
                }
                else
                {
                    _logger.LogInformation(
                        "workflow.start_recovery.still_stuck Instance {InstanceId} of tenant {TenantId} is still stuck ({ReasonCode}).",
                        instance.Id, tenantId, failure);
                }
            }
        }

        cycle.Abandoned += abandoned;
        cycle.Stuck += stuck;
        cycle.IncompleteTrusted += incompleteTrusted;
        _metrics.Record(tenantId, abandoned, stuck, incompleteTrusted);
    }

    private sealed class Cycle
    {
        public long Abandoned { get; set; }
        public long Closed { get; set; }
        public long Raced { get; set; }
        public long Deferred { get; set; }
        public long Stuck { get; set; }
        public long IncompleteTrusted { get; set; }
        public int FailedTenants { get; set; }
    }
}
