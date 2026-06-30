using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Workflow.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Workflow.BackgroundJobs;

/// <summary>
/// MOD-0023 — recurring sweep that runs the workflow SLA escalation/timeout logic automatically (no manual
/// "Run Escalations" button). Escalation is TENANT-SCOPED, but a recurring job runs without a tenant context,
/// so this iterates every active tenant and executes <see cref="RunWorkflowEscalationsCommand"/> inside each
/// tenant's scope (TenantScope.Begin). Idempotency is preserved by the handler's per-task transition-log keys,
/// so repeated runs never double-escalate. A failure in one tenant is logged and does NOT abort the others.
/// The manual endpoint remains for test/explicit triggering.
/// </summary>
public sealed class WorkflowEscalationSweepJob : IBackgroundJobHandler<WorkflowEscalationSweepJobArgs>
{
    private const int DefaultMaxItemsPerTenant = 100;

    private readonly ITenantRegistryRepository _tenantRegistry;
    private readonly ITenantContext _tenantContext;
    private readonly IMediator _mediator;
    private readonly ILogger<WorkflowEscalationSweepJob> _logger;

    public WorkflowEscalationSweepJob(
        ITenantRegistryRepository tenantRegistry,
        ITenantContext tenantContext,
        IMediator mediator,
        ILogger<WorkflowEscalationSweepJob> logger)
    {
        _tenantRegistry = tenantRegistry;
        _tenantContext = tenantContext;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task HandleAsync(WorkflowEscalationSweepJobArgs args, BackgroundJobContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(context);

        var maxItems = args.MaxItemsPerTenant <= 0 ? DefaultMaxItemsPerTenant : args.MaxItemsPerTenant;
        var correlationId = context.EffectiveCorrelationId.ToString();

        var tenants = await _tenantRegistry.GetActiveTenantsAsync(cancellationToken);
        var escalated = 0;
        var timedOut = 0;
        var failedTenants = 0;

        foreach (var tenant in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Run the existing tenant-scoped escalation logic inside this tenant's context.
                using (TenantScope.Begin(_tenantContext, tenant.Id))
                {
                    var response = await _mediator.Send(
                        new RunWorkflowEscalationsCommand(
                            // IdempotencyKey null → the handler's per-task key (workflow-sla:{taskId}:{action})
                            // keeps repeated runs idempotent.
                            new RunWorkflowEscalationsRequest(NowUtc: null, MaxItems: maxItems, IdempotencyKey: null),
                            correlationId),
                        cancellationToken);

                    if (response.Data is { } data)
                    {
                        escalated += data.EscalatedCount;
                        timedOut += data.TimedOutCount;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                failedTenants++;
                _logger.LogWarning(
                    ex,
                    "workflow.escalation.sweep.tenant_failed TenantId={TenantId} ExceptionType={ExceptionType} CorrelationId={CorrelationId}",
                    tenant.Id,
                    ex.GetType().Name,
                    correlationId);
            }
        }

        _logger.LogInformation(
            "workflow.escalation.sweep.completed Tenants={Tenants} Escalated={Escalated} TimedOut={TimedOut} FailedTenants={FailedTenants} CorrelationId={CorrelationId}",
            tenants.Count,
            escalated,
            timedOut,
            failedTenants,
            correlationId);
    }
}
