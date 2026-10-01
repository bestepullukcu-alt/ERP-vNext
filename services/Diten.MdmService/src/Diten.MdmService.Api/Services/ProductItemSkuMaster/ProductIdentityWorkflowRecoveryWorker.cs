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
            return;
        }

        settings.EnsureValidWhenEnabled();
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
                logger.LogError(exception, "Product identity workflow recovery cycle failed.");
            }
            await Task.Delay(TimeSpan.FromSeconds(settings.PollIntervalSeconds), stoppingToken);
        }
    }
}
