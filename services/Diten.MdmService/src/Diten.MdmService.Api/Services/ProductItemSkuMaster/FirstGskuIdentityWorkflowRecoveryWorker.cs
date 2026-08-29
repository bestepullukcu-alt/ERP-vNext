using Diten.MdmService.Api.Configuration;
using Microsoft.Extensions.Options;

namespace Diten.MdmService.Api.Services.ProductItemSkuMaster;

public class FirstGskuIdentityWorkflowRecoveryWorker(
    FirstGskuIdentityWorkflowRecoveryRunner runner,
    IOptions<FirstGskuIdentityWorkflowWorkerOptions> options,
    ILogger<FirstGskuIdentityWorkflowRecoveryWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled)
        {
            return;
        }

        settings.EnsureValidWhenEnabled();
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    "First GSKU identity workflow recovery cycle failed with {ErrorCode}.",
                    exception.GetType().Name);
            }

            await Task.Delay(TimeSpan.FromSeconds(settings.PollIntervalSeconds), stoppingToken);
        }
    }

    protected virtual Task RunCycleAsync(CancellationToken cancellationToken) =>
        runner.RunCycleAsync(cancellationToken);
}
