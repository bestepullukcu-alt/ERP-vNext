using Diten.MdmService.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Api.Services.ProductItemSkuMaster;

public sealed class LskuRetirementRequestRecoveryWorker(LskuRetirementRequestRecoveryRunner runner,
    IOptions<LskuRetirementRequestWorkflowWorkerOptions> options,
    ILogger<LskuRetirementRequestRecoveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var settings = options.Value;
        if (!settings.Enabled) return;
        if (!settings.IsValid()) throw new InvalidOperationException("LSKU_RETIREMENT_WORKER_CONFIGURATION_INVALID");
        while (!ct.IsCancellationRequested)
        {
            try { await runner.RunCycleAsync(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception e) { logger.LogError("LSKU retirement recovery cycle failed: {FailureType}", e.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(settings.PollIntervalSeconds), ct);
        }
    }
}
