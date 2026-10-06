using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.Meetings.Commands;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Meetings.BackgroundJobs;

/// <summary>
/// MOD-0357 S11 — the recurring sweep that turns meeting-series rules into their next single instance. No new
/// engine: the existing Hangfire seam (<c>IRecurringJobRegistrar</c> + <c>IBackgroundJobHandler&lt;T&gt;</c>),
/// copied from <c>TaskRecurrenceSweepJob</c> (this WP's own reading list — that is the pattern, not a new one).
///
/// <para><b>Deliberately the same shape as <c>TaskRecurrenceSweepJob</c>.</b> A series rule is tenant-scoped and
/// a recurring job runs with no tenant context, so this iterates every ACTIVE tenant and executes the
/// tenant-scoped command inside <c>TenantScope.Begin</c> (KS4). One tenant's failure is logged and never aborts
/// the others.</para>
///
/// <para><b>Idempotency does not live here.</b> It lives in the command's claim on each series
/// (<c>LastProcessInstanceId</c> under an expected-version write, KS2), for the identical reason
/// <c>TaskRecurrenceSweepJob</c>'s own doc comment states: a guard that only exists in the scheduler protects
/// nothing when someone triggers the command by hand.</para>
///
/// <para><b>What actually keeps it off (KS7).</b> Two switches — <c>BackgroundJobs:RegisterStandardJobs</c> and
/// <c>EnabledJobs["Diten.Platform.MOD-0357.MeetingSeriesSweepJob"]</c> — but the first ships TRUE in both
/// appsettings.json and appsettings.Development.json, so the per-job flag is the ONLY thing holding it off in a
/// running Development environment. Production adds <c>BackgroundJobs:Enabled = false</c>, which stops the
/// whole scheduler. "The series doesn't generate" is a configuration question before it is a defect.</para>
/// </summary>
public sealed class MeetingSeriesSweepJob : IBackgroundJobHandler<MeetingSeriesSweepJobArgs>
{
    private const int DefaultMaxSeriesPerTenant = 200;

    private readonly ITenantRegistryRepository _tenantRegistry;
    private readonly ITenantContext _tenantContext;
    private readonly IMediator _mediator;
    private readonly ILogger<MeetingSeriesSweepJob> _logger;

    public MeetingSeriesSweepJob(
        ITenantRegistryRepository tenantRegistry,
        ITenantContext tenantContext,
        IMediator mediator,
        ILogger<MeetingSeriesSweepJob> logger)
    {
        _tenantRegistry = tenantRegistry;
        _tenantContext = tenantContext;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task HandleAsync(
        MeetingSeriesSweepJobArgs args, BackgroundJobContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(context);

        var maxSeries = args.MaxSeriesPerTenant <= 0 ? DefaultMaxSeriesPerTenant : args.MaxSeriesPerTenant;
        var correlationId = context.EffectiveCorrelationId.ToString();

        var tenants = await _tenantRegistry.GetActiveTenantsAsync(cancellationToken);
        var generated = 0;
        var alreadyGenerated = 0;
        var failedSeries = 0;
        var failedTenants = 0;

        foreach (var tenant in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // KS4 — the tenant boundary. Every repository read and write below happens inside it.
                using (TenantScope.Begin(_tenantContext, tenant.Id))
                {
                    var response = await _mediator.Send(
                        // NowUtc null → the command reads the clock. The job runs UTC (TimeZoneId "UTC" on the
                        // descriptor) and every occurrence is derived in UTC — see MeetingSeriesSchedule.
                        new GenerateDueMeetingSeriesCommand(NowUtc: null, MaxSeries: maxSeries, correlationId),
                        cancellationToken);

                    if (response.Data is { } data)
                    {
                        generated += data.MeetingsGenerated;
                        alreadyGenerated += data.AlreadyGenerated;
                        failedSeries += data.Failed;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // KS4 — one tenant's own failure never stops the others.
                failedTenants++;
                _logger.LogWarning(
                    ex,
                    "meetings.series.sweep.tenant_failed TenantId={TenantId} ExceptionType={ExceptionType} CorrelationId={CorrelationId}",
                    tenant.Id,
                    ex.GetType().Name,
                    correlationId);
            }
        }

        var lostWork = failedSeries + failedTenants;
        const string completedTemplate =
            "meetings.series.sweep.completed Tenants={Tenants} Generated={Generated} "
            + "AlreadyGenerated={AlreadyGenerated} FailedSeries={FailedSeries} FailedTenants={FailedTenants} "
            + "CorrelationId={CorrelationId}";

        if (lostWork > 0)
        {
            _logger.LogWarning(
                completedTemplate, tenants.Count, generated, alreadyGenerated, failedSeries, failedTenants, correlationId);
        }
        else
        {
            _logger.LogInformation(
                completedTemplate, tenants.Count, generated, alreadyGenerated, failedSeries, failedTenants, correlationId);
        }
    }
}
