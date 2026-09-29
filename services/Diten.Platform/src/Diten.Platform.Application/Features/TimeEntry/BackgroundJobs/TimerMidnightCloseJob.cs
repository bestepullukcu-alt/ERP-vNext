using Diten.BuildingBlocks.BackgroundJobs;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TimeEntry.Commands;
using Diten.Platform.Application.Features.TimeEntry.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.TimeEntry.BackgroundJobs;

/// <summary>
/// MOD-0280-FU01 D3 — closes timers left running past their person's tenant-local midnight and sends the ONE morning
/// notification per closed person-day. A convenience: the first read after midnight closes the same segment at the same
/// instant (<c>TimerReadModel.ReconcileAsync</c>), so correctness never depends on this job (pack §8.4).
///
/// <para>Same shape as <c>TimesheetDecisionSweepJob</c>: every active tenant inside <c>TenantScope.Begin</c>, one tenant's
/// failure logged and never stopping the others. Off unless <c>BackgroundJobs:RegisterStandardJobs</c> AND
/// <c>EnabledJobs["Diten.Platform.MOD-0280.TimerMidnightCloseJob"]</c> are both on.</para>
/// </summary>
public sealed class TimerMidnightCloseJob : IBackgroundJobHandler<TimerMidnightCloseJobArgs>
{
    private const int DefaultMaxSegmentsPerTenant = 500;

    private readonly ITenantRegistryRepository _tenantRegistry;
    private readonly ITenantContext _tenantContext;
    private readonly ITimerSegmentRepository _segments;
    private readonly IMediator _mediator;
    private readonly TimeProvider _clock;
    private readonly ILogger<TimerMidnightCloseJob> _logger;

    public TimerMidnightCloseJob(
        ITenantRegistryRepository tenantRegistry,
        ITenantContext tenantContext,
        ITimerSegmentRepository segments,
        IMediator mediator,
        TimeProvider clock,
        ILogger<TimerMidnightCloseJob> logger)
    {
        _tenantRegistry = tenantRegistry;
        _tenantContext = tenantContext;
        _segments = segments;
        _mediator = mediator;
        _clock = clock;
        _logger = logger;
    }

    public async Task HandleAsync(
        TimerMidnightCloseJobArgs args, BackgroundJobContext context, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(context);

        var max = args.MaxSegmentsPerTenant <= 0 ? DefaultMaxSegmentsPerTenant : args.MaxSegmentsPerTenant;
        var correlationId = context.EffectiveCorrelationId.ToString();
        var tenants = await _tenantRegistry.GetActiveTenantsAsync(cancellationToken);
        var closed = 0;
        var failedTenants = 0;

        foreach (var tenant in tenants)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using (TenantScope.Begin(_tenantContext, tenant.Id))
                {
                    var now = _clock.GetUtcNow();
                    // v2 F9 — the cap applies AFTER the "past its midnight" filter: running timers that are simply still
                    // running (today's) must never crowd the forgotten ones out of a run.
                    var due = (await _segments.ListRunningAsync(int.MaxValue, cancellationToken))
                        .Where(s => TimerRules.PastLocalMidnight(s.LocalDate, TimerService.Zone(s), now))
                        .Select(s => s.UserId)
                        .Distinct()
                        .Take(max)
                        .ToList();
                    foreach (var userId in due)
                    {
                        var response = await _mediator.Send(
                            new CloseTimersAtLocalMidnightCommand(userId, Notify: true, correlationId), cancellationToken);
                        closed += response.IsSuccessful ? response.Data : 0;
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
                    "time-entry.timer.midnight.tenant_failed TenantId={TenantId} ExceptionType={ExceptionType} CorrelationId={CorrelationId}",
                    tenant.Id, ex.GetType().Name, correlationId);
            }
        }

        _logger.LogInformation(
            "time-entry.timer.midnight.completed Tenants={Tenants} Closed={Closed} FailedTenants={Failed} CorrelationId={CorrelationId}",
            tenants.Count, closed, failedTenants, correlationId);
    }
}
