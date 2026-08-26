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
/// <para><b>What actually keeps it off.</b> Two switches guard it — <c>BackgroundJobs:RegisterStandardJobs</c>
/// and <c>EnabledJobs["Diten.Platform.MOD-0024.TaskRecurrenceSweepJob"]</c> — but the first is TRUE in both
/// appsettings.json and appsettings.Development.json, so in a running Development environment the per-job flag is
/// the ONLY thing holding it. Production adds one more real gate: <c>BackgroundJobs:Enabled</c> is false there,
/// which stops the whole scheduler. Say so wherever recurrence is documented: "recurrence doesn't work" is a
/// configuration question before it is a defect.</para>
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
        // Per-RULE failures. The command counts them; this job used to drop the number, so a tenant whose every
        // rule failed was logged as a clean run. Fixed here rather than left as a faithful copy of a defect.
        var failedRules = 0;
        // Rules that owed work and could not say WHOSE it was. The command has always counted them and this job
        // dropped the number — the same hole the due-soon sweep had: work not done, reported as a clean run.
        var skippedUnassigned = 0;
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
                        failedRules += data.Failed;
                        skippedUnassigned += data.SkippedUnassigned;
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

        // Same rule as the due-soon sweep: a run that lost work is not logged at the level a quiet one is.
        var lostWork = failedRules + skippedUnassigned + failedTenants;
        const string completedTemplate =
            "task.recurrence.sweep.completed Tenants={Tenants} Generated={Generated} "
            + "AlreadyGenerated={AlreadyGenerated} FailedRules={FailedRules} "
            + "SkippedUnassigned={SkippedUnassigned} FailedTenants={FailedTenants} CorrelationId={CorrelationId}";

        if (lostWork > 0)
        {
            _logger.LogWarning(
                completedTemplate,
                tenants.Count, generated, alreadyGenerated, failedRules, skippedUnassigned, failedTenants,
                correlationId);
        }
        else
        {
            _logger.LogInformation(
                completedTemplate,
                tenants.Count, generated, alreadyGenerated, failedRules, skippedUnassigned, failedTenants,
                correlationId);
        }
    }
}
