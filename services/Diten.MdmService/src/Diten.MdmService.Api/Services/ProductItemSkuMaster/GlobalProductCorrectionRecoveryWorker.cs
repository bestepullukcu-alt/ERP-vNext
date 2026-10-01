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
        if (!settings.Enabled) return;
        if (!settings.IsValid()) throw new InvalidOperationException(
            "GLOBAL_PRODUCT_CORRECTION_WORKER_CONFIGURATION_INVALID");
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
