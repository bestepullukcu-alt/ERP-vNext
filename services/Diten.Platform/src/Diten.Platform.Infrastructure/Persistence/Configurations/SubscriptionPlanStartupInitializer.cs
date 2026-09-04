using Diten.Platform.Application.Features.SubscriptionPlans.Commands;
using Diten.Platform.Infrastructure.Persistence.Settings;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Diten.Platform.Infrastructure.Persistence.Configurations;

/// <summary>
/// Runs subscription-plan startup maintenance only after the application service provider exists,
/// so authoritative seed mutations can use the normal transaction coordinator and outboxes.
/// </summary>
public sealed class SubscriptionPlanStartupInitializer : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MongoDbSettings _settings;
    private readonly ILogger<SubscriptionPlanStartupInitializer> _logger;

    public SubscriptionPlanStartupInitializer(
        IServiceScopeFactory scopeFactory,
        MongoDbSettings settings,
        ILogger<SubscriptionPlanStartupInitializer> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            /*
             * ⚠ IMongoDatabase is SCOPED and this class is an IHostedService, which the container
             * builds as a SINGLETON. Taking it as a constructor parameter fails DI validation at
             * startup — the app does not boot at all, and no unit test sees it because unit tests
             * never build the container. It is resolved from the scope below instead, next to the
             * mediator that already was.
             */
            await using var scope = _scopeFactory.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();

            // Legacy quota BSON datatype repair is maintenance, not a catalog/applicability mutation.
            await SubscriptionPlanSeed.RepairQuotaDataTypesAsync(database, cancellationToken);

            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            var response = await mediator.Send(new SeedDefaultSubscriptionPlansCommand(), cancellationToken);
            if (!response.IsSuccessful)
            {
                throw new InvalidOperationException(
                    $"Transactional subscription-plan startup seed failed: {string.Join("; ", response.Errors)}");
            }
        }
        catch (Exception ex) when (_settings.AllowStartupWithoutDatabase && ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "Transactional subscription-plan startup initialization failed. Startup continues because MongoDbSettings:AllowStartupWithoutDatabase=true; readiness will report MongoDB status.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
