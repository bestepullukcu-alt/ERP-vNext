using Diten.Platform.Infrastructure.Eventing;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class EntitlementCacheInvalidationConsumerRegistrationTests
{
    [Fact]
    public void AddPlatformEventConsumers_RegistersEntitlementCacheInvalidationConsumer()
    {
        var services = new ServiceCollection();

        services.AddMassTransit(registration =>
        {
            Diten.Platform.Infrastructure.DependencyInjection.AddPlatformEventConsumers(registration);
        });

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(EntitlementCacheInvalidationConsumer)
            && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Theory]
    [InlineData("InMemory", false)]
    [InlineData("", false)]
    [InlineData("RabbitMQ", true)]
    [InlineData("rabbitmq", true)]
    public void RabbitMqEventingOptions_UseRabbitMq_OnlyEnablesRabbitMqTransport(string transport, bool expected)
    {
        var options = new RabbitMqEventingOptions { Transport = transport };

        Assert.Equal(expected, options.UseRabbitMq);
    }
}
