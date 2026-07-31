using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Diten.PpmService.Infrastructure.Audit;

public sealed class PpmAuditProducerWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<PpmAuditProducerOptions> options) : BackgroundService
{
    private readonly PpmAuditProducerOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled || !_options.WorkerEnabled)
        {
            return;
        }

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_options.PollIntervalSeconds));
        do
        {
            using var scope = scopeFactory.CreateScope();
            await scope.ServiceProvider
                .GetRequiredService<PpmAuditIntentDispatcher>()
                .DispatchPendingAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
