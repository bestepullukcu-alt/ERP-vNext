using Diten.InventoryService.Domain.Traceability.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Diten.InventoryService.Persistence.Traceability;

/// <summary>
/// Runs <see cref="ILotExpirySweeper"/> once at start and then every <see cref="LotExpirySweeperOptions.SweepIntervalSeconds"/>
/// (default 15 minutes, pack §8). A failed sweep is logged and retried on the next tick; it never stops the host.
/// </summary>
public sealed class LotExpirySweeperHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly LotExpirySweeperOptions _options;
    private readonly ILogger<LotExpirySweeperHostedService> _logger;

    public LotExpirySweeperHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<LotExpirySweeperOptions> options,
        ILogger<LotExpirySweeperHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.SweeperEnabled)
        {
            _logger.LogWarning("MOD-0174 lot expiry sweeper is disabled by configuration.");
            return;
        }

        await SweepOnceAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Max(5, _options.SweepIntervalSeconds)));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await SweepOnceAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Host shutdown: the wait for the next tick is cancelled — a normal stop, not a failure.
        }
    }

    private async Task SweepOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var expired = await scope.ServiceProvider.GetRequiredService<ILotExpirySweeper>().SweepAsync(DateTimeOffset.UtcNow, stoppingToken);
            if (expired > 0)
            {
                _logger.LogInformation("MOD-0174 sweeper expired {Count} lots.", expired);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MOD-0174 lot expiry sweep failed; retrying on the next tick.");
        }
    }
}
