using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Diten.BuildingBlocks.Eventing;
using Diten.ManufacturingService.Application.Features.Boms;
using Diten.ManufacturingService.Domain.Entities;
using Diten.ManufacturingService.Domain.Repositories;
using Diten.ManufacturingService.Persistence.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using Xunit;

namespace Diten.ManufacturingService.Tests.Api;

/// <summary>
/// BOM-version lifecycle event (<c>bom.openapi.yaml</c> v1.2.0) over the real Program and a real Mongo replica set:
/// what a release writes to the outbox, that the outbox row exists only if the change committed, and how the shared
/// publisher moves it. A row in the outbox is NOT a delivery — broker delivery is measured separately.
/// </summary>
[Collection(BomOutboxCollection.Name)]
public sealed class BomVersionEventTests : IClassFixture<BomApiFixture>
{
    private readonly BomApiFactory _factory;

    public BomVersionEventTests(BomApiFixture fixture)
    {
        Skip.If(BomApiFactory.Connection is null, $"{BomApiFactory.EnvironmentVariable} is not set — no isolated Mongo replica set.");
        _factory = fixture.Factory!;
    }

    private IMongoCollection<BomOutboxEvent> Outbox => _factory.Db().GetCollection<BomOutboxEvent>("mfg_bom_outbox");

    private Task<List<BomOutboxEvent>> EventsFor(Guid tenant) => Outbox.Find(e => e.TenantId == tenant).SortBy(e => e.EventName).ToListAsync();

    [SkippableFact]
    public async Task Release_announces_the_new_effective_version_and_the_one_it_supersedes()
    {
        var caller = new BomCaller(_factory);
        var item = Guid.NewGuid();
        var v1 = await caller.CreateDraft(item, (Guid.NewGuid(), "2.000", "EA", 10));
        Assert.Empty(await EventsFor(caller.Tenant)); // a draft is not announced

        var correlation1 = Guid.NewGuid();
        var (status1, released1, _) = await caller.Send(HttpMethod.Post, $"/api/bom/version/{v1["bomVersionId"]}/release",
            new { changeControlRef = "CC-1", rowVersion = (int)v1["rowVersion"]! }, correlation1.ToString());
        Assert.Equal(200, status1);

        var afterFirst = await EventsFor(caller.Tenant);
        var effective1 = Assert.Single(afterFirst);
        Assert.Equal(BomVersionEvents.EffectiveEventName, effective1.EventName);
        Assert.Equal(BomOutboxStatus.Pending, effective1.Status);
        Assert.Equal(correlation1, effective1.CorrelationId);
        var payload1 = JsonNode.Parse(effective1.PayloadJson)!;
        Assert.Equal("BomVersionEffective", (string)payload1["eventType"]!);
        Assert.Equal(effective1.Id, Guid.Parse((string)payload1["eventId"]!));
        Assert.Equal(caller.Tenant, Guid.Parse((string)payload1["tenantId"]!));
        Assert.Equal(caller.LegalEntity, Guid.Parse((string)payload1["legalEntityId"]!));
        Assert.Equal(item, Guid.Parse((string)payload1["itemId"]!));
        Assert.Equal((string)v1["bomVersionId"]!, (string)payload1["bomVersionId"]!);
        Assert.Equal(1, (int)payload1["version"]!);
        Assert.Equal("Effective", (string)payload1["status"]!);
        Assert.Equal(DateTimeOffset.Parse((string)released1!["effectiveFrom"]!), DateTimeOffset.Parse((string)payload1["effectiveFrom"]!));

        var v2 = await caller.CreateDraft(item, (Guid.NewGuid(), "3.000", "EA", 10));
        var correlation2 = Guid.NewGuid();
        var (status2, _, _) = await caller.Send(HttpMethod.Post, $"/api/bom/version/{v2["bomVersionId"]}/release",
            new { changeControlRef = "CC-2", rowVersion = (int)v2["rowVersion"]! }, correlation2.ToString());
        Assert.Equal(200, status2);

        var second = (await EventsFor(caller.Tenant)).Where(e => e.CorrelationId == correlation2).ToList();
        Assert.Equal(2, second.Count);
        var superseded = JsonNode.Parse(second.Single(e => e.EventName == BomVersionEvents.SupersededEventName).PayloadJson)!;
        var effective2 = JsonNode.Parse(second.Single(e => e.EventName == BomVersionEvents.EffectiveEventName).PayloadJson)!;
        Assert.Equal((string)v1["bomVersionId"]!, (string)superseded["bomVersionId"]!);
        Assert.Equal("Superseded", (string)superseded["status"]!);
        Assert.Equal("BomVersionSuperseded", (string)superseded["eventType"]!);
        Assert.Equal(1, (int)superseded["version"]!);
        Assert.Equal((string)payload1["effectiveFrom"]!, (string)superseded["effectiveFrom"]!); // when v1 became effective
        Assert.Equal((string)v2["bomVersionId"]!, (string)effective2["bomVersionId"]!);
        Assert.Equal(2, (int)effective2["version"]!);
    }

    [SkippableFact]
    public async Task The_payload_carries_exactly_the_contract_fields_and_no_bom_lines()
    {
        var caller = new BomCaller(_factory);
        var draft = await caller.CreateDraft(Guid.NewGuid(), (Guid.NewGuid(), "2.000", "EA", 10));
        await caller.Release(draft);
        var payload = JsonNode.Parse(Assert.Single(await EventsFor(caller.Tenant)).PayloadJson)!.AsObject();

        Assert.Equal(ContractRequiredFields().Order(), payload.Select(p => p.Key).Order());
        foreach (var lineField in new[] { "components", "routing", "quantity", "componentItemId", "steps" })
        {
            Assert.DoesNotContain(lineField, payload.Select(p => p.Key));
        }
    }

    [SkippableFact]
    public async Task No_event_is_written_when_the_change_is_not()
    {
        var caller = new BomCaller(_factory);
        var draft = await caller.CreateDraft(Guid.NewGuid(), (Guid.NewGuid(), "2.000", "EA", 10));

        // A stale row version: the release is refused before anything is written.
        var (status, _, _) = await caller.Send(HttpMethod.Post, $"/api/bom/version/{draft["bomVersionId"]}/release",
            new { changeControlRef = "CC-1", rowVersion = (int)draft["rowVersion"]! + 5 });
        Assert.Equal(409, status);
        Assert.Empty(await EventsFor(caller.Tenant));

        // Update and delete of a draft are not lifecycle events.
        var (updated, body, _) = await caller.Send(HttpMethod.Put, $"/api/bom/version/{draft["bomVersionId"]}",
            new { description = "edited", components = new[] { new { componentItemId = Guid.NewGuid(), quantity = "1", uomId = "KG", position = 10 } }, rowVersion = (int)draft["rowVersion"]! });
        Assert.True(updated == 200, $"update answered {updated}: {body}");
        var (deleted, _, _) = await caller.Send(HttpMethod.Delete, $"/api/bom/version/{draft["bomVersionId"]}?rowVersion={(int)body!["rowVersion"]!}");
        Assert.Equal(204, deleted);
        Assert.Empty(await EventsFor(caller.Tenant));
    }

    [SkippableFact]
    public async Task If_the_outbox_cannot_be_written_the_bom_and_its_history_are_not_written_either()
    {
        var tenant = Guid.NewGuid();
        var le = Guid.NewGuid();
        var bom = new BomVersion
        {
            TenantId = tenant, LegalEntityId = le, ItemId = Guid.NewGuid(), RevisionNo = 1, Version = 1,
            Status = BomStatus.Effective, EffectiveFrom = DateTimeOffset.UtcNow,
            Components = [new BomComponentLine { ComponentItemId = Guid.NewGuid(), Quantity = "1", UomId = "EA", Position = 10 }]
        };
        var history = new BomHistoryEntry
        {
            TenantId = tenant, LegalEntityId = le, BomVersionId = bom.Id, ItemId = bom.ItemId, Operation = BomHistoryOperation.Released,
            OccurredAtUtc = DateTimeOffset.UtcNow
        };
        var first = BomVersionEvents.Effective(bom, Guid.NewGuid().ToString(), DateTimeOffset.UtcNow);
        var clash = BomVersionEvents.Effective(bom, Guid.NewGuid().ToString(), DateTimeOffset.UtcNow);
        clash.Id = first.Id; // the second outbox insert fails inside the transaction

        var result = await new BomHistoryJournal(_factory.Db()).CommitAsync(
            new BomChangeSet(tenant, le, bom, [], [history], [first, clash]), CancellationToken.None);

        Assert.Equal(BomCommitResult.Conflict, result);
        Assert.Equal(0, await _factory.Db().GetCollection<BomVersion>("mfg_boms").CountDocumentsAsync(x => x.TenantId == tenant));
        Assert.Equal(0, await _factory.Db().GetCollection<BomHistoryEntry>("mfg_bom_history").CountDocumentsAsync(x => x.TenantId == tenant));
        Assert.Empty(await EventsFor(tenant));
    }

    [SkippableFact]
    public async Task The_shared_publisher_delivers_through_the_transport_and_marks_the_row_published()
    {
        var caller = new BomCaller(_factory);
        var draft = await caller.CreateDraft(Guid.NewGuid(), (Guid.NewGuid(), "2.000", "EA", 10));
        await caller.Release(draft);
        var row = Assert.Single(await EventsFor(caller.Tenant));

        var store = _factory.Services.GetRequiredService<IEventOutboxStore>();
        var transport = new RecordingTransport(failFor: null);
        await new EventOutboxPublisherProcessor(store, transport, Options()).PublishPendingAsync();

        var sent = Assert.Single(transport.Sent, m => m.EventId == row.Id);
        Assert.Equal(BomVersionEvents.EffectiveEventName, sent.EventName);
        Assert.Equal(1, sent.EventVersion);
        Assert.Equal(caller.Tenant, sent.TenantId);
        Assert.Equal(row.CorrelationId, sent.CorrelationId);
        Assert.Equal(BomVersionEvents.Producer, sent.Producer);
        Assert.Equal(row.PayloadJson, System.Text.Encoding.UTF8.GetString(sent.CanonicalPayloadUtf8.Span));
        var stored = await Outbox.Find(e => e.Id == row.Id).SingleAsync();
        Assert.Equal(BomOutboxStatus.Published, stored.Status);
        Assert.NotNull(stored.PublishedAtUtc);
    }

    [SkippableFact]
    public async Task A_transport_failure_keeps_the_event_for_a_retry_and_dead_letters_it_after_the_last_attempt()
    {
        var caller = new BomCaller(_factory);
        var draft = await caller.CreateDraft(Guid.NewGuid(), (Guid.NewGuid(), "2.000", "EA", 10));
        await caller.Release(draft);
        var row = Assert.Single(await EventsFor(caller.Tenant));
        var store = _factory.Services.GetRequiredService<IEventOutboxStore>();
        var transport = new RecordingTransport(failFor: row.Id);

        var retryLater = Options(maxAttempts: 2, retryDelay: TimeSpan.FromMinutes(1));
        await new EventOutboxPublisherProcessor(store, transport, retryLater).PublishPendingAsync();
        var afterFirst = await Outbox.Find(e => e.Id == row.Id).SingleAsync();
        Assert.Equal(BomOutboxStatus.Pending, afterFirst.Status);
        Assert.Equal(1, afterFirst.AttemptCount);
        Assert.Equal(nameof(TimeoutException), afterFirst.LastError);
        Assert.True(afterFirst.NextAttemptAtUtc > DateTimeOffset.UtcNow, "the retry waits for its backoff");

        // Not eligible before its backoff; once the time has come (simulated), the last attempt dead-letters it.
        await new EventOutboxPublisherProcessor(store, transport, retryLater).PublishPendingAsync();
        Assert.Equal(1, (await Outbox.Find(e => e.Id == row.Id).SingleAsync()).AttemptCount);
        await Outbox.UpdateOneAsync(e => e.Id == row.Id, Builders<BomOutboxEvent>.Update.Set(e => e.NextAttemptAtUtc, DateTimeOffset.UtcNow.AddSeconds(-1)));
        await new EventOutboxPublisherProcessor(store, transport, retryLater).PublishPendingAsync();
        Assert.Equal(BomOutboxStatus.DeadLettered, (await Outbox.Find(e => e.Id == row.Id).SingleAsync()).Status);
    }

    [SkippableFact]
    public void Without_a_configured_transport_nothing_claims_to_deliver()
    {
        using var scope = _factory.Services.CreateScope();
        Assert.Null(scope.ServiceProvider.GetService<IEventTransportPublisher>());
        Assert.Throws<NotSupportedException>(() =>
            scope.ServiceProvider.GetRequiredService<IEventOutboxStore>().EnqueueAsync(null!).GetAwaiter().GetResult());
    }

    [SkippableFact]
    public void An_enabled_transport_without_a_broker_address_fails_at_startup()
    {
        using var factory = new TransportMisconfiguredFactory();
        var error = Assert.ThrowsAny<Exception>(() => factory.Services);
        Assert.Contains("EventTransport:Enabled is true", error.ToString());
    }

    private static EventOutboxPublisherOptions Options(int maxAttempts = 10, TimeSpan? retryDelay = null) =>
        new(BatchSize: 1000, MaxAttempts: maxAttempts, InitialRetryDelay: retryDelay ?? TimeSpan.Zero, MaximumRetryDelay: retryDelay ?? TimeSpan.Zero,
            PublishingStaleAfter: TimeSpan.FromMinutes(5));

    /// <summary>The <c>required</c> list of <c>BomVersionEvent</c>, read from the contract file itself.</summary>
    private static string[] ContractRequiredFields()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md")))
        {
            dir = dir.Parent;
        }

        var yaml = File.ReadAllText(Path.Combine(dir!.FullName, "docs", "analysis", "contracts", "bom.openapi.yaml"));
        var block = Regex.Match(yaml, @"\n    BomVersionEvent:\n(?<body>(?:      .*\n)+)").Groups["body"].Value;
        var required = Regex.Match(block, @"required: \[(?<list>[^\]]+)\]").Groups["list"].Value;
        Assert.False(string.IsNullOrWhiteSpace(required), "BomVersionEvent.required not found in bom.openapi.yaml");
        return required.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    }

    private sealed class RecordingTransport(Guid? failFor) : IEventTransportPublisher
    {
        public List<EventTransportMessage> Sent { get; } = [];

        public Task PublishAsync(EventTransportMessage message, CancellationToken cancellationToken = default)
        {
            if (message.EventId == failFor)
            {
                throw new TimeoutException("broker unreachable");
            }

            Sent.Add(message);
            return Task.CompletedTask;
        }
    }
}

/// <summary>EventTransport switched on with no broker address: the host must refuse to start.</summary>
public sealed class TransportMisconfiguredFactory : BomApiFactory
{
    protected override Dictionary<string, string?> Settings
    {
        get
        {
            var settings = base.Settings;
            settings["EventTransport:Enabled"] = "true";
            settings["EventTransport:RabbitMqHost"] = "";
            return settings;
        }
    }
}
