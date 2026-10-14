using System.Text;
using System.Text.Json.Nodes;
using Diten.ManufacturingService.Application.Features.Boms;
using Diten.ManufacturingService.Domain.Entities;
using MongoDB.Driver;
using RabbitMQ.Client;
using Xunit;

namespace Diten.ManufacturingService.Tests.Api;

/// <summary>
/// Broker delivery, measured end to end: a release over HTTP → outbox row (same transaction) → the PRODUCTION
/// <c>BomOutboxPublisherWorker</c> + <c>MassTransitBomEventTransportPublisher</c> → a real RabbitMQ, where a probe queue
/// bound to the message exchange receives it. Needs <c>MANUFACTURING_TEST_RABBITMQ</c> (host) plus
/// <c>MANUFACTURING_TEST_RABBITMQ_USER</c> / <c>_PASSWORD</c>; skipped — with the reason printed — otherwise.
/// Same collection as the other outbox tests: this host's worker drains the whole outbox.
/// </summary>
[Collection(BomOutboxCollection.Name)]
public sealed class BomEventBrokerDeliveryTests : IClassFixture<BrokerFixture>
{
    // MassTransit publishes a message type to a fanout exchange named "{namespace}:{type}".
    private const string Exchange = "Diten.BuildingBlocks.Eventing:EventTransportMessage";

    private readonly BrokerFixture _fixture;

    public BomEventBrokerDeliveryTests(BrokerFixture fixture)
    {
        Skip.If(BomApiFactory.Connection is null, $"{BomApiFactory.EnvironmentVariable} is not set — no isolated Mongo replica set.");
        Skip.If(BrokerFactory.Host is null, $"{BrokerFactory.HostVariable} is not set — no RabbitMQ to deliver to.");
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task A_release_reaches_the_broker_and_the_outbox_row_is_marked_published()
    {
        var factory = _fixture.Factory!;
        var connectionFactory = new ConnectionFactory
        {
            HostName = BrokerFactory.Host!, UserName = BrokerFactory.User!, Password = BrokerFactory.Password!
        };
        await using var connection = await connectionFactory.CreateConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Fanout, durable: true, autoDelete: false);
        var probe = (await channel.QueueDeclareAsync($"bom-event-probe-{Guid.NewGuid():N}", durable: false, exclusive: true, autoDelete: true)).QueueName;
        await channel.QueueBindAsync(probe, Exchange, routingKey: string.Empty);

        var caller = new BomCaller(factory);
        var draft = await caller.CreateDraft(Guid.NewGuid(), (Guid.NewGuid(), "2.000", "EA", 10));
        await caller.Release(draft);
        var outbox = factory.Db().GetCollection<BomOutboxEvent>("mfg_bom_outbox");
        var row = await outbox.Find(e => e.TenantId == caller.Tenant).SingleAsync();

        JsonNode? delivered = null;
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (delivered is null && DateTime.UtcNow < deadline)
        {
            var got = await channel.BasicGetAsync(probe, autoAck: true);
            if (got is null)
            {
                await Task.Delay(200);
                continue;
            }

            var envelope = JsonNode.Parse(Encoding.UTF8.GetString(got.Body.Span))!;
            if ((string?)envelope["message"]?["eventId"] == row.Id.ToString())
            {
                delivered = envelope["message"];
            }
        }

        Assert.True(delivered is not null, $"event {row.Id} did not reach RabbitMQ within 30 s");
        Assert.Equal(BomVersionEvents.EffectiveEventName, (string)delivered!["eventName"]!);
        Assert.Equal(caller.Tenant.ToString(), (string)delivered["tenantId"]!);
        Assert.Equal(row.CorrelationId.ToString(), (string)delivered["correlationId"]!);
        var payload = Encoding.UTF8.GetString(Convert.FromBase64String((string)delivered["canonicalPayloadUtf8"]!));
        Assert.Equal(row.PayloadJson, payload);

        var published = await outbox.Find(e => e.Id == row.Id).SingleAsync();
        for (var i = 0; i < 50 && published.Status != BomOutboxStatus.Published; i++)
        {
            await Task.Delay(100);
            published = await outbox.Find(e => e.Id == row.Id).SingleAsync();
        }

        Assert.Equal(BomOutboxStatus.Published, published.Status);
    }
}

[CollectionDefinition(Name)]
public sealed class BomOutboxCollection
{
    public const string Name = "bom-outbox";
}

public sealed class BrokerFixture : IDisposable
{
    public BrokerFactory? Factory { get; } =
        BomApiFactory.Connection is null || BrokerFactory.Host is null ? null : new BrokerFactory();

    public void Dispose() => Factory?.Dispose();
}

/// <summary>The real Program with <c>EventTransport:Enabled=true</c> against the test broker.</summary>
public sealed class BrokerFactory : BomApiFactory
{
    public const string HostVariable = "MANUFACTURING_TEST_RABBITMQ";
    public static string? Host => Environment.GetEnvironmentVariable(HostVariable);
    public static string? User => Environment.GetEnvironmentVariable(HostVariable + "_USER");
    public static string? Password => Environment.GetEnvironmentVariable(HostVariable + "_PASSWORD");

    protected override Dictionary<string, string?> Settings
    {
        get
        {
            var settings = base.Settings;
            settings["EventTransport:Enabled"] = "true";
            settings["EventTransport:RabbitMqHost"] = Host;
            settings["EventTransport:RabbitMqUsername"] = User;
            settings["EventTransport:RabbitMqPassword"] = Password;
            return settings;
        }
    }
}
