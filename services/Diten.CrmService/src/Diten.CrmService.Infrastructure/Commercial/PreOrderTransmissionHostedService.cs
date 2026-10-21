using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Commercial.PreOrders;
using Diten.CrmService.Domain.Repositories.Commercial;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diten.CrmService.Infrastructure.Commercial;

/// <summary>
/// WP-VW-W6 W6-BE-c1 — MOD-0168 §12: the pre-order transmission retry scheduler (the CRM has no Hangfire — the W5
/// <c>VisitNotificationHostedService</c> pattern). Every <c>Commercial:PreOrders:Transmission:IntervalMinutes</c> (default 5)
/// it lists the failed transmissions whose retry is due (across tenants), and runs each one as the audited system command
/// <see cref="RunPreOrderTransmissionRetryCommand"/> in its OWN scope with that order's tenant pinned. Off with
/// <c>Commercial:PreOrders:Transmission:RetryEnabled=false</c>. A failing order or tick is logged and the next one runs; two
/// instances are safe (the attempt number is part of the e-mail's idempotency key and the order write is versioned).
/// The clock is the injected TimeProvider.
/// </summary>
public sealed class PreOrderTransmissionHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _clock;
    private readonly ILogger<PreOrderTransmissionHostedService> _logger;
    private readonly bool _enabled;
    private readonly TimeSpan _interval;

    public PreOrderTransmissionHostedService(IServiceScopeFactory scopes, IConfiguration configuration,
        ILogger<PreOrderTransmissionHostedService> logger, TimeProvider? clock = null)
    {
        _scopes = scopes;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
        _enabled = configuration.GetValue("Commercial:PreOrders:Transmission:RetryEnabled", true);
        _interval = TimeSpan.FromMinutes(Math.Clamp(configuration.GetValue("Commercial:PreOrders:Transmission:IntervalMinutes", 5), 1, 1440));
    }

    public bool Enabled => _enabled;
    public TimeSpan Interval => _interval;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("Pre-order transmission retry scheduler is off (Commercial:PreOrders:Transmission:RetryEnabled=false).");
            return;
        }

        using var timer = new PeriodicTimer(_interval, _clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await TickAsync(stoppingToken);
        }
    }

    /// <summary>One tick; never throws. Returns how many retries ran.</summary>
    public async Task<int> TickAsync(CancellationToken ct)
    {
        IReadOnlyList<(Guid TenantId, Guid Id)> due;
        try
        {
            using var scope = _scopes.CreateScope();
            var orders = scope.ServiceProvider.GetRequiredService<IPreOrderRepository>();
            var now = _clock.GetUtcNow();
            var retries = await orders.ListDueTransmissionRetriesAsync(now.UtcTicks, PreOrderTransmissionRetry.BatchSize, ct);
            // W6-BE-c2 (FU-W6-APPROVED-BACKLOG) — the approved orders that never had an attempt get their first one
            var backlog = await orders.ListApprovedUntransmittedAsync(now - PreOrderTransmissionRetry.ApprovedGrace, PreOrderTransmissionRetry.BatchSize, ct);
            due = retries.Concat(backlog).Distinct().ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Pre-order transmission retry tick could not list the due orders; the next tick will run.");
            return 0;
        }

        var ran = 0;
        foreach (var (tenantId, id) in due)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                scope.ServiceProvider.GetRequiredService<TenantContext>().SetTenant(tenantId);
                var result = await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new RunPreOrderTransmissionRetryCommand(id), ct);
                ran += result.Data ? 1 : 0;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogWarning(ex, "Pre-order transmission retry failed for {PreOrderId}; the next tick will try again.", id);
            }
        }

        return ran;
    }
}
