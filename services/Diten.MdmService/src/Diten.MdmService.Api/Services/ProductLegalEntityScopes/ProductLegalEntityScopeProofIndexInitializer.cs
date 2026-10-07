using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Persistence.Repositories;
using MongoDB.Driver;

namespace Diten.MdmService.Api.Services.ProductLegalEntityScopes;

/// <summary>
/// WP-MDM-SCOPE-ACCEPT-01 FIX3 (8) / FIX4 (3) — builds the indexes the write proof's queries need (abbreviation ledger /
/// history / register by product) when the service starts. Before, the first leased write built them, and on a large
/// collection that could outlast its lease.
/// <list type="bullet">
/// <item>It NEVER fails the start: every exception (a server-selection <c>TimeoutException</c> included — the driver throws
/// that, not a MongoException) is logged by its type and counted (<c>proof_index_build_failed{error}</c>).</item>
/// <item>The start waits at most <see cref="StartWait"/>; a longer build goes on in the background.</item>
/// <item>A failed build is tried again every <see cref="RetryInterval"/> until it succeeds; until then the
/// <c>proof_indexes_missing</c> gauge is 1 (the proof stays correct, it reads by scan).</item>
/// </list>
/// Not run by the one-shot operational commands (they return before the host starts).
/// </summary>
public sealed class ProductLegalEntityScopeProofIndexInitializer : IHostedService, IDisposable
{
    private readonly Func<CancellationToken, Task> _build;
    private readonly ILogger<ProductLegalEntityScopeProofIndexInitializer> _logger;
    private readonly CancellationTokenSource _stopping = new();
    private Task? _running;

    public ProductLegalEntityScopeProofIndexInitializer(
        IServiceScopeFactory scopes,
        ILogger<ProductLegalEntityScopeProofIndexInitializer> logger)
        : this(async cancellationToken =>
        {
            // The database is registered per scope; one scope per build attempt.
            await using var scope = scopes.CreateAsyncScope();
            await ProductLegalEntityScopeOperationalReadinessRepository.EnsureObjectIndexesAsync(
                scope.ServiceProvider.GetRequiredService<IMongoDatabase>(), cancellationToken);
        }, logger)
    {
    }

    /// <summary>The build itself is replaceable (a test makes it fail, then succeed).</summary>
    public ProductLegalEntityScopeProofIndexInitializer(
        Func<CancellationToken, Task> build,
        ILogger<ProductLegalEntityScopeProofIndexInitializer> logger)
    {
        _build = build;
        _logger = logger;
    }

    public TimeSpan StartWait { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan RetryInterval { get; init; } = TimeSpan.FromMinutes(1);

    /// <summary>The background build (tests await it).</summary>
    public Task Running => _running ?? Task.CompletedTask;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ProductLegalEntityScopeRolloutMetrics.RecordProofIndexesBuilt(false);
        _running = BuildUntilDoneAsync(_stopping.Token);
        try
        {
            await _running.WaitAsync(StartWait, cancellationToken);
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Product legal-entity scope proof indexes are still being built after {Wait}; the service starts and the build goes on.", StartWait);
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
                ProductLegalEntityScopeRolloutMetrics.RecordProofIndexesBuilt(true);
                return;
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                ProductLegalEntityScopeRolloutMetrics.CountProofIndexBuildFailed(exception.GetType().Name);
                _logger.LogWarning(exception,
                    "Product legal-entity scope proof indexes could not be built ({Error}); retrying in {Interval}.",
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
