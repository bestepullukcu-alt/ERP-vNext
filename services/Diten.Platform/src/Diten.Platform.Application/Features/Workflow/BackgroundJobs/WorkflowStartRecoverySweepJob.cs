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

    public async Task HandleAsync(
        WorkflowStartRecoverySweepJobArgs args,
        BackgroundJobContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(context);

        var abandonAfter = WorkflowStartRecoveryOptions.AbandonAfterOf(_options);
        var maxCloses = args.MaxClosesPerTenant > 0 ? args.MaxClosesPerTenant : Math.Max(1, _options.MaxClosesPerTenant);
        var correlationId = context.EffectiveCorrelationId.ToString();
        var tenants = await _tenantRegistry.GetActiveTenantsAsync(cancellationToken);
        var cycle = new Cycle();

        foreach (var tenant in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using (TenantScope.Begin(_tenantContext, tenant.Id))
                {
                    await RunTenantAsync(tenant.Id, abandonAfter, maxCloses, correlationId, cycle, cancellationToken);
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
            "workflow.start_recovery.cycle Tenants={Tenants} Abandoned={Abandoned} Closed={Closed} Stuck={Stuck} "
            + "IncompleteTrusted={IncompleteTrusted} FailedTenants={FailedTenants} AbandonAfterMinutes={AbandonAfterMinutes} CorrelationId={CorrelationId}",
            tenants.Count, cycle.Abandoned, cycle.Closed, cycle.Stuck, cycle.IncompleteTrusted, cycle.FailedTenants,
            (int)abandonAfter.TotalMinutes, correlationId);
    }

    private async Task RunTenantAsync(
        Guid tenantId,
        TimeSpan abandonAfter,
        int maxCloses,
        string correlationId,
        Cycle cycle,
        CancellationToken ct)
    {
        var cutoff = DateTimeOffset.UtcNow - abandonAfter;
        var abandoned = await _store.CountAbandonedGenericStartsAsync(cutoff, ct);
        var incompleteTrusted = await _store.CountIncompleteTrustedStartsAsync(cutoff, ct);
        var stuck = 0L;
        var candidates = (await _store.ListAbandonedGenericStartsAsync(cutoff, maxCloses, ct))
            .Concat(await _store.ListIncompleteTrustedStartsAsync(cutoff, maxCloses, ct));

        foreach (var instance in candidates)
        {
            var closed = await _mediator.Send(new CloseAbandonedWorkflowStartCommand(instance.Id, abandonAfter, correlationId), ct);
            if (closed.IsSuccessful)
            {
                cycle.Closed++;
                _metrics.Unstuck(instance.Id);
                continue;
            }

            if (closed.ReasonCode == WorkflowReasonCodes.WorkflowStartNotAbandoned)
            {
                continue; // a replay completed it (or it moved) between the read and the close: nothing to do
            }

            stuck++;
            if (_metrics.FirstStuckReport(instance.Id))
            {
                _logger.LogError(
                    "workflow.start_recovery.stuck Instance {InstanceId} of tenant {TenantId} could not be closed ({ReasonCode}); it stays Active without a task.",
                    instance.Id, tenantId, closed.ReasonCode);
            }
            else
            {
                _logger.LogInformation(
                    "workflow.start_recovery.still_stuck Instance {InstanceId} of tenant {TenantId} is still stuck ({ReasonCode}).",
                    instance.Id, tenantId, closed.ReasonCode);
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
        public long Stuck { get; set; }
        public long IncompleteTrusted { get; set; }
        public int FailedTenants { get; set; }
    }
}
