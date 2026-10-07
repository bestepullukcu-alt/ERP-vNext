using Diten.MdmService.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Api.Services.ProductItemSkuMaster;

public sealed class ProductIdentityWorkflowRecoveryWorker(
    ProductIdentityWorkflowRecoveryRunner runner,
    IOptions<ProductIdentityWorkflowWorkerOptions> options,
    ILogger<ProductIdentityWorkflowRecoveryWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            // BL-552 flow 3 FIX2 item 4: switched off, the worker still records the stuck gauges on every poll — a
            // worker switched off is exactly when what is stuck stays stuck. The runner's off branch reads counts
            // only: it claims, retries or writes no operation.
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await runner.RunCycleAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        "Product identity workflow stuck-gauge cycle (worker off) failed with {ExceptionType}.",
                        exception.GetType().FullName);
                }
                await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(settings.PollIntervalSeconds, 1, 3_600)), stoppingToken);
            }
            return;
        }

        settings.EnsureValidWhenEnabled();
        long cycle = 0;
        while (!stoppingToken.IsCancellationRequested)
        {
            cycle++;
            try
            {
                await runner.RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // The type and the cycle only — the same rule the runner keeps for one operation: an exception's
                // message and stack can carry whatever the failing layer put into them.
                logger.LogError(
                    "Product identity workflow recovery cycle {Cycle} failed with {ExceptionType}.",
                    cycle, exception.GetType().FullName);
            }
            await Task.Delay(TimeSpan.FromSeconds(settings.PollIntervalSeconds), stoppingToken);
        }
    }
}
