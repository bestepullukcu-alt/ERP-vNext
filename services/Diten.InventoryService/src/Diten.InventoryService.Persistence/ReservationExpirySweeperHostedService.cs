using Diten.InventoryService.Domain.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.InventoryService.Persistence;

/// <summary>
/// Periodic expiry of due reservations (pack ASSUMPTION-0173-16). Expiry also happens inside every create / release /
/// decrease transaction of the SKU; this sweeper covers SKUs nobody touches. A failed sweep is logged and retried on
/// the next tick — it never stops the host, and correctness does not depend on it (reads already ignore due holds).
/// </summary>
public sealed class ReservationExpirySweeperHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ReservationSweeperOptions _options;
    private readonly ILogger<ReservationExpirySweeperHostedService> _logger;

    public ReservationExpirySweeperHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<ReservationSweeperOptions> options,
        ILogger<ReservationExpirySweeperHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.SweeperEnabled)
        {
            _logger.LogWarning("MOD-0173 reservation expiry sweeper is disabled by configuration.");
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, _options.SweepIntervalSeconds)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var expired = await scope.ServiceProvider.GetRequiredService<IReservationExpirySweeper>()
                    .SweepAsync(DateTimeOffset.UtcNow, stoppingToken);
                if (expired > 0)
                {
                    _logger.LogInformation("MOD-0173 sweeper expired {Count} reservations.", expired);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MOD-0173 reservation expiry sweep failed; retrying on the next tick.");
            }
        }
    }
}
