using Diten.CrmService.Application.Features.VisitNotifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diten.CrmService.Infrastructure.VisitNotifications;

/// <summary>
/// WP-VW-W5 (K-N7) — the CRM's notification scheduler (the CRM has no Hangfire): a hosted service whose
/// <see cref="PeriodicTimer"/> (<c>Crm:Notifications:IntervalMinutes</c>, default 15) runs one
/// <see cref="VisitNotificationWorker"/> tick — the overdue sweep, then the due retries — in its own DI scope. Off with
/// <c>Crm:Notifications:Enabled=false</c>. Several instances are safe: every row is taken by an atomic claim. A tick that
/// fails is logged and the next one runs; the host never stops for it. The clock is the injected TimeProvider.
/// </summary>
public sealed class VisitNotificationHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _clock;
    private readonly ILogger<VisitNotificationHostedService> _logger;
    private readonly bool _enabled;
    private readonly TimeSpan _interval;
    private readonly string _workerId = Environment.MachineName + ":" + Guid.NewGuid().ToString("N")[..8];

    public VisitNotificationHostedService(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<VisitNotificationHostedService> logger,
        TimeProvider? clock = null)
    {
        _scopes = scopes;
        _logger = logger;
        _clock = clock ?? TimeProvider.System;
        _enabled = configuration.GetValue("Crm:Notifications:Enabled", true);
        _interval = TimeSpan.FromMinutes(Math.Clamp(configuration.GetValue("Crm:Notifications:IntervalMinutes", 15), 1, 1440));
    }

    public bool Enabled => _enabled;
    public TimeSpan Interval => _interval;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            _logger.LogInformation("Visit notification scheduler is off (Crm:Notifications:Enabled=false).");
            return;
        }

        using var timer = new PeriodicTimer(_interval, _clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await TickAsync(stoppingToken);
        }
    }

    /// <summary>One tick in its own scope; never throws (the next tick runs).</summary>
    public async Task TickAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var worker = scope.ServiceProvider.GetRequiredService<VisitNotificationWorker>();
            var result = await worker.RunOnceAsync(_clock.GetUtcNow(), _workerId, ct);
            if (result.Overdue + result.Retried > 0)
            {
                _logger.LogInformation("Visit notifications tick: {Overdue} overdue, {Retried} retried.", result.Overdue, result.Retried);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Visit notification scheduler tick failed; the next tick will run.");
        }
    }
}
