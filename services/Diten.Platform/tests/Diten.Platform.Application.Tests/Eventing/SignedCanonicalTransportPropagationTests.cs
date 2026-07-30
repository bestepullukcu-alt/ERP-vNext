using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Infrastructure.Eventing;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Diten.Platform.Application.Tests.Eventing;

public sealed class SignedCanonicalTransportPropagationTests
{
    [Fact]
    public async Task MassTransitAdapter_PropagatesTrustedHeadersExactly()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness()
            .BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            var headers = new Dictionary<string, string>
            {
                [TrustedTransportMetadata.SignatureSchemeHeader] = "hmac-sha256-v1",
                [TrustedTransportMetadata.KeyIdHeader] = "ppm-key-2026",
                [TrustedTransportMetadata.SignatureHeader] = new string('b', 64)
            };
            var message = new EventTransportMessage(
                Guid.NewGuid(),
                "test.signed.v1",
                1,
                Guid.NewGuid(),
                null,
                Guid.NewGuid(),
                "Diten.Platform.Tests",
                DateTimeOffset.UtcNow,
                "{\"a\":1}",
                headers);

            await new MassTransitRabbitMqEventPublisher(harness.Bus).PublishAsync(message);

            var published = await harness.Published.SelectAsync<EventTransportMessage>().First();
            Assert.Equal("hmac-sha256-v1", published.Context.Headers.Get<string>(TrustedTransportMetadata.SignatureSchemeHeader));
            Assert.Equal("ppm-key-2026", published.Context.Headers.Get<string>(TrustedTransportMetadata.KeyIdHeader));
            Assert.Equal(new string('b', 64), published.Context.Headers.Get<string>(TrustedTransportMetadata.SignatureHeader));
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task InMemoryAdapter_PreservesTrustedMetadata()
    {
        var headers = new Dictionary<string, string>
        {
            [TrustedTransportMetadata.SignatureSchemeHeader] = "hmac-sha256-v1",
            [TrustedTransportMetadata.KeyIdHeader] = "key-1",
            [TrustedTransportMetadata.SignatureHeader] = new string('d', 64)
        };
        var message = new EventTransportMessage(
            Guid.NewGuid(),
            "test.signed.v1",
            1,
            Guid.NewGuid(),
            null,
            null,
            "Diten.Platform.Tests",
            DateTimeOffset.UtcNow,
            "{}",
            headers);
        var transport = new InMemoryEventBus();

        await transport.PublishAsync(message);

        Assert.Same(headers, Assert.Single(transport.Messages).TransportHeaders);
    }
}
