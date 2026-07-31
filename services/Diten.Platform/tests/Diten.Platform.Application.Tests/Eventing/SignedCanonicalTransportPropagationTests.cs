using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Infrastructure.Eventing;
using MassTransit;
using MassTransit.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using EventTransportMessage = Diten.BuildingBlocks.Eventing.EventTransportMessage;
using LegacyEventTransportMessage = Diten.Platform.Application.Contracts.Eventing.EventTransportMessage;

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
                new TrustedTransportMetadata(headers));

            await new MassTransitRabbitMqEventPublisher(harness.Bus).PublishAsync(message);

            var published = await harness.Published.SelectAsync<EventTransportMessage>().First();
            Assert.Equal("hmac-sha256-v1", published.Context.Headers.Get<string>(TrustedTransportMetadata.SignatureSchemeHeader));
            Assert.Equal("ppm-key-2026", published.Context.Headers.Get<string>(TrustedTransportMetadata.KeyIdHeader));
            Assert.Equal(new string('b', 64), published.Context.Headers.Get<string>(TrustedTransportMetadata.SignatureHeader));
            Assert.Contains(
                "urn:message:Diten.BuildingBlocks.Eventing:EventTransportMessage",
                published.Context.SupportedMessageTypes);
            Assert.DoesNotContain(
                "urn:message:Diten.Platform.Application.Contracts.Eventing:EventTransportMessage",
                published.Context.SupportedMessageTypes);
            Assert.False(await harness.Published.Any<LegacyEventTransportMessage>());
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task MassTransit_ComputesPermanentAndLegacyContractUrnsExactly()
    {
        await using var provider = new ServiceCollection()
            .AddMassTransitTestHarness()
            .BuildServiceProvider(true);
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();
        try
        {
            await harness.Bus.Publish(new EventTransportMessage(
                Guid.NewGuid(),
                "test.permanent.v1",
                1,
                Guid.NewGuid(),
                null,
                null,
                "Diten.Platform.Tests",
                DateTimeOffset.UtcNow,
                "{}"));
            await harness.Bus.Publish(new LegacyEventTransportMessage(
                Guid.NewGuid(),
                "test.legacy.v1",
                1,
                Guid.NewGuid(),
                null,
                null,
                "Diten.Platform.Tests",
                DateTimeOffset.UtcNow,
                "{}"));

            var permanent = await harness.Published.SelectAsync<EventTransportMessage>().First();
            var legacy = await harness.Published.SelectAsync<LegacyEventTransportMessage>().First();

            Assert.Contains(
                "urn:message:Diten.BuildingBlocks.Eventing:EventTransportMessage",
                permanent.Context.SupportedMessageTypes);
            Assert.Contains(
                "urn:message:Diten.Platform.Application.Contracts.Eventing:EventTransportMessage",
                legacy.Context.SupportedMessageTypes);
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
            new TrustedTransportMetadata(headers));
        var transport = new InMemoryEventBus();

        await transport.PublishAsync(message);

        Assert.Equal(headers, Assert.Single(transport.Messages).TransportMetadata.Headers);
    }
}
