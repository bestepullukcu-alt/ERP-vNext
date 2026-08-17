using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Infrastructure.Services.Audit;

public sealed class AuditOutboxWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AuditOutboxWorkerOptions _options;
    private readonly ILogger<AuditOutboxWorker> _logger;

    public AuditOutboxWorker(
        IServiceScopeFactory scopeFactory,
        AuditOutboxWorkerOptions options,
        ILogger<AuditOutboxWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Audit outbox worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<AuditOutboxProcessor>();
                await processor.ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Audit outbox worker loop failed.");
            }

            await Task.Delay(_options.PollInterval, stoppingToken);
        }

        _logger.LogInformation("Audit outbox worker stopped.");
    }
}
