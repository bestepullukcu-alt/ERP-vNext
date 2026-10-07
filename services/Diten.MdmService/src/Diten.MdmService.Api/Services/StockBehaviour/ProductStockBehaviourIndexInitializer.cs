using System.Diagnostics.Metrics;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Driver;

namespace Diten.MdmService.Api.Services.StockBehaviour;

/// <summary>
/// WP-MDM-ITEM-MASTER (MOD-0290-FU04) S3, CT decision R4 — builds the Finished Good sales-box GTIN unique partial index
/// (<see cref="ProductStockBehaviourRepository.GtinIndexName"/>) when the service starts; the scope FIX4 initializer
/// pattern (ProductLegalEntityScopeProofIndexInitializer):
/// <list type="bullet">
/// <item>it NEVER fails the start: every exception is logged by its type and counted
/// (<c>mdm.stock_behaviour.gtin_index_build_failed{error}</c>);</item>
/// <item>the start waits at most <see cref="StartWait"/>; a longer build goes on in the background;</item>
/// <item>a failed build is tried again every <see cref="RetryInterval"/>; until it succeeds the
/// <c>mdm.stock_behaviour.gtin_index_missing</c> gauge is 1 and uniqueness rests on the pre-write check alone.</item>
/// </list>
/// </summary>
public sealed class ProductStockBehaviourIndexInitializer : IHostedService, IDisposable
{
    public const string MeterName = "Diten.MdmService.StockBehaviour";

    private static readonly Meter Meter = new(MeterName);
    private static int _missing = 1;
    private static readonly ObservableGauge<int> Missing = Meter.CreateObservableGauge(
        "mdm.stock_behaviour.gtin_index_missing", () => Volatile.Read(ref _missing), "{index}",
        "1 while the Finished Good GTIN unique index is not built (uniqueness then rests on the pre-write check).");
    private static readonly Counter<long> Failed = Meter.CreateCounter<long>(
        "mdm.stock_behaviour.gtin_index_build_failed", "{attempt}", "GTIN index builds that failed, by error type.");

    private readonly Func<CancellationToken, Task> _build;
    private readonly ILogger<ProductStockBehaviourIndexInitializer> _logger;
    private readonly CancellationTokenSource _stopping = new();
    private Task? _running;

    public ProductStockBehaviourIndexInitializer(
        IServiceScopeFactory scopes,
        ILogger<ProductStockBehaviourIndexInitializer> logger)
        : this(async cancellationToken =>
        {
            // The database is registered per scope; one scope per build attempt.
            await using var scope = scopes.CreateAsyncScope();
            await ProductStockBehaviourRepository.EnsureGtinIndexAsync(
                scope.ServiceProvider.GetRequiredService<IMongoDatabase>(), cancellationToken);
        }, logger)
    {
    }

    /// <summary>The build itself is replaceable (a test makes it fail, then succeed).</summary>
    public ProductStockBehaviourIndexInitializer(
        Func<CancellationToken, Task> build,
        ILogger<ProductStockBehaviourIndexInitializer> logger)
    {
        _build = build;
        _logger = logger;
    }

    public TimeSpan StartWait { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan RetryInterval { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>The background build (tests await it).</summary>
    public Task Running => _running ?? Task.CompletedTask;

    /// <summary>1 while the index is not built (what the gauge reports).</summary>
    public static int IndexMissing => Volatile.Read(ref _missing);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _ = Missing;
        Volatile.Write(ref _missing, 1);
        _running = BuildUntilDoneAsync(_stopping.Token);
        try
        {
            await _running.WaitAsync(StartWait, cancellationToken);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("The Finished Good GTIN index is still being built after {Wait}; the service starts and the build goes on.", StartWait);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The host is stopping before it started; the background build stops with StopAsync.
        }
    }

    private async Task BuildUntilDoneAsync(CancellationToken stopping)
    {
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                await _build(stopping);
                Volatile.Write(ref _missing, 0);
                return;
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                Failed.Add(1, new KeyValuePair<string, object?>("error", exception.GetType().Name));
                _logger.LogWarning(exception,
                    "The Finished Good GTIN index could not be built ({Error}); retrying in {Interval}.",
                    exception.GetType().Name, RetryInterval);
            }
            try
            {
                await Task.Delay(RetryInterval, stopping);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        await _stopping.CancelAsync();
        if (_running is not null)
            await Task.WhenAny(_running, Task.Delay(Timeout.Infinite, cancellationToken));
    }

    public void Dispose() => _stopping.Dispose();
}
