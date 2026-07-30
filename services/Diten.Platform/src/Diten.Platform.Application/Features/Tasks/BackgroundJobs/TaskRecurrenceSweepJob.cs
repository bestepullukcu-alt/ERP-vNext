using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Tasks.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Tasks.BackgroundJobs;

/// <summary>
/// MOD-0024 Phase 4 — the recurring sweep that turns recurrence rules into tasks. No new engine: the existing
/// Hangfire seam (<c>IRecurringJobRegistrar</c> + <c>IBackgroundJobHandler&lt;T&gt;</c>), pack §12 K8.
///
/// <para><b>Deliberately the same shape as <c>WorkflowEscalationSweepJob</c>.</b> Recurrence is tenant-scoped and
/// a recurring job runs with no tenant context, so this iterates every ACTIVE tenant and executes the
/// tenant-scoped command inside <c>TenantScope.Begin</c>. Without that scope a read would see another tenant's
/// rules and create their work in the wrong place — which is not a leak of information but a leak of WORK, and
/// far harder to notice. One tenant's failure is logged and never aborts the others.</para>
///
/// <para><b>Idempotency does not live here.</b> It lives in the command's claim on each rule
/// (<c>LastProcessInstanceId</c> under an expected-version write), for the same reason the escalation sweep
/// leaves it to the handler's transition-log keys: a guard that only exists in the scheduler protects nothing
/// when someone triggers the command by hand.</para>
///
/// <para><b>OFF unless switched on, twice.</b> The job runs only when <c>BackgroundJobs:RegisterStandardJobs</c>
/// is true AND <c>BackgroundJobs:EnabledJobs["Diten.Platform.MOD-0024.TaskRecurrenceSweepJob"]</c> is true. Both,
/// not either — see PlatformRecurringJobRegistrar. Say so wherever recurrence is documented: otherwise
/// "recurrence doesn't work" gets reported as a bug when it is a job nobody enabled.</para>
/// </summary>
public sealed class TaskRecurrenceSweepJob : IBackgroundJobHandler<TaskRecurrenceSweepJobArgs>
{
    private const int DefaultMaxRulesPerTenant = 200;

    private readonly ITenantRegistryRepository _tenantRegistry;
    private readonly ITenantContext _tenantContext;
    private readonly IMediator _mediator;
    private readonly ILogger<TaskRecurrenceSweepJob> _logger;

    public TaskRecurrenceSweepJob(
        ITenantRegistryRepository tenantRegistry,
        ITenantContext tenantContext,
        IMediator mediator,
        ILogger<TaskRecurrenceSweepJob> logger)
    {
        _tenantRegistry = tenantRegistry;
        _tenantContext = tenantContext;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task HandleAsync(
        TaskRecurrenceSweepJobArgs args,
        BackgroundJobContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(context);

        var maxRules = args.MaxRulesPerTenant <= 0 ? DefaultMaxRulesPerTenant : args.MaxRulesPerTenant;
        var correlationId = context.EffectiveCorrelationId.ToString();

        var tenants = await _tenantRegistry.GetActiveTenantsAsync(cancellationToken);
        var generated = 0;
        var alreadyGenerated = 0;
        var failedTenants = 0;

        foreach (var tenant in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // The tenant boundary. Every repository below reads and writes inside it.
                using (TenantScope.Begin(_tenantContext, tenant.Id))
                {
                    var response = await _mediator.Send(
                        // NowUtc null → the command reads the clock. The job runs UTC (TimeZoneId "UTC" on the
                        // descriptor) and every occurrence is derived in UTC — see TaskRecurrenceSchedule for why
                        // that is stated rather than assumed.
                        new GenerateDueRecurringTasksCommand(NowUtc: null, MaxRules: maxRules, correlationId),
                        cancellationToken);

                    if (response.Data is { } data)
                    {
                        generated += data.TasksGenerated;
                        alreadyGenerated += data.AlreadyGenerated;
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
                    "task.recurrence.sweep.tenant_failed TenantId={TenantId} ExceptionType={ExceptionType} CorrelationId={CorrelationId}",
                    tenant.Id,
                    ex.GetType().Name,
                    correlationId);
            }
        }

        _logger.LogInformation(
            "task.recurrence.sweep.completed Tenants={Tenants} Generated={Generated} AlreadyGenerated={AlreadyGenerated} FailedTenants={FailedTenants} CorrelationId={CorrelationId}",
            tenants.Count,
            generated,
            alreadyGenerated,
            failedTenants,
            correlationId);
    }
}
