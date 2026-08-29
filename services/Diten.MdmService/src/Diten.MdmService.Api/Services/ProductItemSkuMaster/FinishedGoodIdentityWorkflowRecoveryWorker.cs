using Diten.MdmService.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Api.Services.ProductItemSkuMaster;

public sealed class FinishedGoodIdentityWorkflowRecoveryWorker(
    FinishedGoodIdentityWorkflowRecoveryRunner runner,
    IOptions<FinishedGoodIdentityWorkflowWorkerOptions> options,
    ILogger<FinishedGoodIdentityWorkflowRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled) return;
        settings.EnsureValidWhenEnabled();
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await runner.RunCycleAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
            catch (Exception exception) { logger.LogError("FINISHED_GOOD workflow recovery cycle failed: {ErrorCode}", exception.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(settings.PollIntervalSeconds), stoppingToken);
        }
    }
}
