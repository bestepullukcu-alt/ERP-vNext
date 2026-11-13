using Diten.BuildingBlocks.Eventing;
using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diten.PlanningService.Infrastructure.Features.DemandPlanning;

public static class DemandRevisionEventTransportRegistration
{
    public static IServiceCollection AddDemandRevisionEventTransport(
        this IServiceCollection services, IConfiguration configuration,
        IHostEnvironment environment)
    {
        var section = configuration.GetSection("EventTransport");
        if (!section.GetValue("Enabled", false)) return services;
        if (!environment.IsDevelopment() && !environment.IsEnvironment("IntegrationTest"))
            throw new InvalidOperationException(
                "Draft Demand v2 event delivery is limited to Development and IntegrationTest.");

        var host = section["RabbitMqHost"];
        var user = section["RabbitMqUsername"];
        var password = section["RabbitMqPassword"];
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(user) ||
            string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException(
                "EventTransport:Enabled requires RabbitMqHost, RabbitMqUsername and RabbitMqPassword.");

        var port = (ushort)section.GetValue("RabbitMqPort", 5672);
        var virtualHost = section["RabbitMqVirtualHost"] ?? "/";
        services.AddMassTransit(bus => bus.UsingRabbitMq((context, rabbit) =>
        {
            rabbit.Host(host, port, virtualHost, options =>
            {
                options.Username(user);
                options.Password(password);
            });
            rabbit.ConfigureEndpoints(context);
        }));
        services.AddScoped<IEventTransportPublisher, DemandRevisionRabbitMqPublisher>();
        services.AddHostedService<DemandRevisionOutboxWorker>();
        return services;
    }
}

public sealed class DemandRevisionRabbitMqPublisher(IPublishEndpoint publisher)
    : IEventTransportPublisher
{
    public Task PublishAsync(EventTransportMessage message,
        CancellationToken cancellationToken = default) =>
        publisher.Publish(message, cancellationToken);
}

public sealed class DemandRevisionOutboxWorker(
    IServiceScopeFactory scopes, ILogger<DemandRevisionOutboxWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var processor = new EventOutboxPublisherProcessor(
                    scope.ServiceProvider.GetRequiredService<IEventOutboxStore>(),
                    scope.ServiceProvider.GetRequiredService<IEventTransportPublisher>(),
                    new EventOutboxPublisherOptions(20, 10,
                        TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(5),
                        TimeSpan.FromMinutes(5)));
                foreach (var result in await processor.PublishPendingAsync(stoppingToken))
                    if (result.Outcome != EventOutboxPublishOutcome.Published)
                        logger.LogWarning("Demand revision event {EventId} delivery: {Outcome}, attempt {Attempt}, {Reason}.",
                            result.EventId, result.Outcome, result.AttemptCount, result.ReasonCode);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                logger.LogError("Demand revision outbox processing failed: {ExceptionType}",
                    error.GetType().Name);
            }
            await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
