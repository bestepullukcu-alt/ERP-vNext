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
