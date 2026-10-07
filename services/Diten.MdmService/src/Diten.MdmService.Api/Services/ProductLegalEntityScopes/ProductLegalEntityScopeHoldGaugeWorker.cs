using Diten.MdmService.Application.Features.ProductLegalEntityScopes;
using Diten.MdmService.Domain.Repositories;

namespace Diten.MdmService.Api.Services.ProductLegalEntityScopes;

/// <summary>
/// WP-MDM-SCOPE-ACCEPT-01 FIX2 (3) — principle B for the scope rollout: a fence has no expiry and a kept lease waits for
/// its command or its expiry, so what holds writes NOW must be visible. Once a minute this worker reads the holds of every
/// tenant (read-only, counts only) into the gauges of <see cref="ProductLegalEntityScopeRolloutMetrics"/>: the oldest fence's
/// age, the fences up, the leases held. A failed read leaves the last values and is logged; it never stops the service.
/// Not started by the one-shot operational commands (they return before the host runs).
/// </summary>
public sealed class ProductLegalEntityScopeHoldGaugeWorker(
    IServiceScopeFactory scopes,
    TimeProvider clock,
    ILogger<ProductLegalEntityScopeHoldGaugeWorker> logger) : BackgroundService
{
    internal static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RefreshAsync(stoppingToken);
            await Task.Delay(Interval, clock, stoppingToken);
        }
    }

    public async Task RefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopes.CreateAsyncScope();
            var now = clock.GetUtcNow();
            var holds = await scope.ServiceProvider.GetRequiredService<IProductLegalEntityScopeHoldInventoryRepository>()
                .ReadAsync(now, cancellationToken);
            ProductLegalEntityScopeRolloutMetrics.RecordHolds(new(
                holds.OldestFenceAcquiredAtUtc, holds.FencesUp, holds.RunningLeases, holds.ExpiredLeases), now);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            // FIX3 (7): the gauges keep their last values — so the failure is counted, and the observed-age gauge
            // (holds_observed_age_seconds) shows how stale they are.
            ProductLegalEntityScopeRolloutMetrics.CountRefreshFailed();
            logger.LogWarning(exception, "Product legal-entity scope hold gauges could not be refreshed.");
        }
    }
}
