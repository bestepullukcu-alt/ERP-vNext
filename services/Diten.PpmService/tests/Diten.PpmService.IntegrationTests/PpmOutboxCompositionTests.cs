using Diten.BuildingBlocks.Eventing;
using Diten.PpmService.Application;
using Diten.PpmService.Infrastructure;
using Diten.PpmService.Infrastructure.Audit;
using Diten.PpmService.Persistence;
using Diten.PpmService.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using Xunit;

namespace Diten.PpmService.IntegrationTests;

public sealed class PpmOutboxCompositionTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Outbox_composition_uses_one_store_per_scope(
        bool producerEnabled,
        bool workerEnabled)
    {
        using var provider = BuildProvider(producerEnabled, workerEnabled);

        using var firstScope = provider.CreateScope();
        var writer = firstScope.ServiceProvider.GetRequiredService<IEventOutboxWriter>();
        var store = firstScope.ServiceProvider.GetRequiredService<IEventOutboxStore>();

        Assert.IsType<PpmEventOutboxStore>(writer);
        Assert.Same(writer, store);
        Assert.Same(writer, firstScope.ServiceProvider.GetRequiredService<IEventOutboxWriter>());
        Assert.NotNull(firstScope.ServiceProvider.GetRequiredService<IEventBus>());
        Assert.NotNull(firstScope.ServiceProvider.GetRequiredService<PpmAuditIntentDispatcher>());
        Assert.NotNull(firstScope.ServiceProvider.GetRequiredService<EventOutboxPublisherProcessor>());

        using var secondScope = provider.CreateScope();
        Assert.NotSame(
            writer,
            secondScope.ServiceProvider.GetRequiredService<IEventOutboxStore>());
    }

    private static ServiceProvider BuildProvider(bool producerEnabled, bool workerEnabled)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:ConnectionString"] = "mongodb://127.0.0.1:37018/?directConnection=true",
                ["Mongo:DatabaseName"] = "ppm-composition-only",
                ["PpmAuditProducer:Enabled"] = producerEnabled.ToString(),
                ["PpmAuditProducer:WorkerEnabled"] = workerEnabled.ToString(),
                ["PpmAuditProducer:KeyId"] = "composition-key-v1",
                ["PpmAuditProducer:SecretBase64"] =
                    Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
                ["PpmAuditProducer:RabbitMqHost"] = "127.0.0.1",
                ["PpmAuditProducer:RabbitMqVirtualHost"] = "composition-only",
                ["PpmAuditProducer:RabbitMqUsername"] = "composition-only",
                ["PpmAuditProducer:RabbitMqPassword"] = "composition-only"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);
        services.AddPpmPersistence(configuration);
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }
}
