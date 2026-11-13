using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diten.InventoryService.Persistence.WavePlanning;

/// <summary>
/// MOD-0180's background service (pack §5 Persistence row, §8.8; AC-12). It only ever SENDS wave commands through <see cref="IMediator"/> — it
/// opens no transaction of its own and it never creates a wave (no periodic wave building, D-1): the commands it may send are
/// <c>CompleteWaveReleaseCommand</c>, <c>ReleasePendingOrdersCommand</c> (DL-2) and <c>ReconcileWaveCommand</c> (DL-3) — an architecture test
/// pins that list. <b>DL-1:</b> none of the three exists yet, so a round sends NOTHING; the loop, the interval setting
/// (<c>Warehouse:Wave:ReconcileSeconds</c>, default 60) and the single-round method the tests call are in place for the later slices.
/// </summary>
public sealed class WaveMaintenanceHostedService : BackgroundService
{
    public const string IntervalSetting = "Warehouse:Wave:ReconcileSeconds";
    public const int DefaultIntervalSeconds = 60;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WaveMaintenanceHostedService> _logger;
    private readonly TimeSpan _interval;

    public WaveMaintenanceHostedService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<WaveMaintenanceHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        var seconds = configuration.GetValue<int?>(IntervalSetting) ?? DefaultIntervalSeconds;
        _interval = TimeSpan.FromSeconds(seconds > 0 ? seconds : DefaultIntervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await RunOnceAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "MOD-0180 wave maintenance round failed; the next round retries.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // host shutdown
        }
    }

    /// <summary>
    /// One maintenance round (the method the tests drive). Returns how many commands it sent. DL-1 sends none: the release recovery and the
    /// pending-order round arrive with DL-2, the reconciliation with DL-3 — each adds its own send here, through the mediator.
    /// </summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        _ = scope.ServiceProvider.GetRequiredService<IMediator>();
        await Task.CompletedTask;
        return 0;
    }
}
