using Diten.MdmService.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Api.Services.ProductItemSkuMaster;

public sealed class GlobalProductCorrectionRecoveryWorker(
    GlobalProductCorrectionRecoveryRunner runner,
    IOptions<GlobalProductCorrectionWorkflowWorkerOptions> options,
    ILogger<GlobalProductCorrectionRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.IsValid()) throw new InvalidOperationException(
            "GLOBAL_PRODUCT_CORRECTION_WORKER_CONFIGURATION_INVALID");
        if (!settings.Enabled)
        {
            // BL-552 flow 3 FIX3 item 5 (the identity worker's FIX2 item 4 rule): switched off, it still records the
            // stuck gauges, at the gauge's own sparse interval — counts only, no request is claimed or written.
            while (!stoppingToken.IsCancellationRequested)
            {
                try { await runner.RunCycleAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                catch (Exception exception)
                {
                    logger.LogError("Global Product correction stuck-gauge cycle (worker off) failed with {ExceptionType}.",
                        exception.GetType().FullName);
                }
                await Task.Delay(TimeSpan.FromSeconds(settings.StuckGaugeIntervalSeconds), stoppingToken);
            }
            return;
        }
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await runner.RunCycleAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                logger.LogError(exception, "Global Product correction recovery cycle failed.");
            }
            await Task.Delay(TimeSpan.FromSeconds(settings.PollIntervalSeconds), stoppingToken);
        }
    }
}
