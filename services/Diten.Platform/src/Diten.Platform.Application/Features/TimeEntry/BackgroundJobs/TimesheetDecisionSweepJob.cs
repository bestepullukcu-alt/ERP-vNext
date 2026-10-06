using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.TimeEntry.BackgroundJobs;

/// <summary>
/// MOD-0280-FU01 D7 — a CONVENIENCE sweep over submitted weeks, so a decision made in the Task Center reaches the task
/// totals even if nobody opens the week or the approvals page. Correctness never depends on it: the same finalizer runs
/// on every week read and on the approvals page (pack §8.4), and this job only calls that same path.
///
/// <para>Same shape as <c>MeetingSeriesSweepJob</c>: a recurring job runs with no tenant context, so it walks every
/// active tenant inside <c>TenantScope.Begin</c>; one tenant's failure is logged and never stops the others. It is off
/// unless <c>BackgroundJobs:RegisterStandardJobs</c> AND <c>EnabledJobs["Diten.Platform.MOD-0280.TimesheetDecisionSweepJob"]</c>
/// are both on.</para>
/// </summary>
public sealed class TimesheetDecisionSweepJob : IBackgroundJobHandler<TimesheetDecisionSweepJobArgs>
{
    private const int DefaultMaxWeeksPerTenant = 200;

    private readonly ITenantRegistryRepository _tenantRegistry;
    private readonly ITenantContext _tenantContext;
    private readonly ITimesheetWeekRepository _weeks;
    private readonly ITimesheetDecisionPuller _puller;
    private readonly ILogger<TimesheetDecisionSweepJob> _logger;

    public TimesheetDecisionSweepJob(
        ITenantRegistryRepository tenantRegistry,
        ITenantContext tenantContext,
        ITimesheetWeekRepository weeks,
        ITimesheetDecisionPuller puller,
        ILogger<TimesheetDecisionSweepJob> logger)
    {
        _tenantRegistry = tenantRegistry;
        _tenantContext = tenantContext;
        _weeks = weeks;
        _puller = puller;
        _logger = logger;
    }

    public async Task HandleAsync(
        TimesheetDecisionSweepJobArgs args, BackgroundJobContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(context);

        var maxWeeks = args.MaxWeeksPerTenant <= 0 ? DefaultMaxWeeksPerTenant : args.MaxWeeksPerTenant;
        var correlationId = context.EffectiveCorrelationId.ToString();
        var tenants = await _tenantRegistry.GetActiveTenantsAsync(cancellationToken);
        var finalizedTenants = 0;
        var failedTenants = 0;
        var appliedWeeks = 0;
        var failedWeeks = 0;

        foreach (var tenant in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using (TenantScope.Begin(_tenantContext, tenant.Id))
                {
                    var submitted = await _weeks.ListNeedingFinalizationAsync(maxWeeks, cancellationToken);
                    var pulled = await _puller.PullAsync(submitted, correlationId, cancellationToken);
                    appliedWeeks += pulled.Applied;
                    failedWeeks += pulled.Failed;

                    // BL-483 — a tenant counts only when a week's state was really written; a swallowed failure is
                    // counted on its own line below, never here.
                    if (pulled.Applied > 0)
                    {
                        finalizedTenants++;
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
                    "time-entry.decision.sweep.tenant_failed TenantId={TenantId} ExceptionType={ExceptionType} CorrelationId={CorrelationId}",
                    tenant.Id, ex.GetType().Name, correlationId);
            }
        }

        _logger.LogInformation(
            "time-entry.decision.sweep.completed Tenants={Tenants} TenantsWithDecisions={Finalized} FailedTenants={Failed} "
            + "AppliedWeeks={AppliedWeeks} FailedWeeks={FailedWeeks} CorrelationId={CorrelationId}",
            tenants.Count, finalizedTenants, failedTenants, appliedWeeks, failedWeeks, correlationId);
    }
}
