using Diten.MdmService.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Api.Services.ProductItemSkuMaster;

public sealed class GskuRetirementRequestRecoveryWorker(GskuRetirementRequestRecoveryRunner runner,
    IOptions<GskuRetirementRequestWorkflowWorkerOptions> options, ILogger<GskuRetirementRequestRecoveryWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled) return;
        if (!settings.IsValid()) throw new InvalidOperationException("GSKU_RETIREMENT_REQUEST_WORKER_CONFIGURATION_INVALID");
        if (settings.MaxRetryDelaySeconds < settings.RetryDelaySeconds)
            logger.LogWarning("GSKU retirement request worker: MaxRetryDelaySeconds ({Ceiling}) is below RetryDelaySeconds ({Delay}); the ceiling is raised to the delay.",
                settings.MaxRetryDelaySeconds, settings.RetryDelaySeconds);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await runner.RunCycleAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
            catch (Exception exception) { logger.LogError(exception, "GSKU correction recovery cycle failed."); }
            await Task.Delay(TimeSpan.FromSeconds(settings.PollIntervalSeconds), stoppingToken);
        }
    }
}
