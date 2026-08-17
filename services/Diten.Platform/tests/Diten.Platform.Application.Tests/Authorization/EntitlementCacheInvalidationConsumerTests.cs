using System.Collections.Concurrent;
using System.Text.Json;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Services;
using Diten.Platform.Application.Services.Eventing;
using Diten.Platform.Common.Authorization;
using Diten.Platform.Contracts.Events;
using Diten.Platform.Infrastructure.Eventing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Diten.Platform.Application.Tests.Authorization;

public sealed class EntitlementCacheInvalidationConsumerTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherTenantId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid ActorId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
    private static readonly DateTimeOffset OccurredAtUtc = DateTimeOffset.Parse("2026-05-20T10:00:00Z");

    public static TheoryData<EventTransportMessage> EntitlementInvalidationMessages =>
        new()
        {
            CreateMessage(new TenantEntitlementAddedV1(Guid.NewGuid(), OccurredAtUtc, TenantId, Guid.NewGuid(), ActorId, "HR")),
            CreateMessage(new TenantEntitlementEnabledV1(Guid.NewGuid(), OccurredAtUtc, TenantId, Guid.NewGuid(), ActorId, "HR")),
            CreateMessage(new TenantEntitlementDisabledV1(Guid.NewGuid(), OccurredAtUtc, TenantId, Guid.NewGuid(), ActorId, "HR")),
            CreateMessage(new TenantEntitlementExpiryUpdatedV1(Guid.NewGuid(), OccurredAtUtc, TenantId, Guid.NewGuid(), ActorId, "HR")),
            CreateMessage(new TenantEntitlementOverrideRemovedV1(Guid.NewGuid(), OccurredAtUtc, TenantId, Guid.NewGuid(), ActorId, "HR"))
        };

    [Theory]
    [MemberData(nameof(EntitlementInvalidationMessages))]
    public async Task EntitlementCacheInvalidationConsumer_EvictsTenantCacheForEntitlementEvents(EventTransportMessage message)
    {
        var cacheService = CreateCacheService();
        var consumer = CreateConsumer(cacheService);
        var calls = new CacheFactoryCalls();

        await SeedTenantAndOtherTenantCacheAsync(cacheService, calls);

        var result = await consumer.ConsumeAsync(message);

        await ReadTenantAndOtherTenantCacheAsync(cacheService, calls);

        Assert.Equal(ConsumedEventExecutionResult.Consumed, result);
        Assert.Equal(2, calls.TenantModule);
        Assert.Equal(2, calls.TenantFeature);
        Assert.Equal(1, calls.OtherTenantModule);
        Assert.Equal(1, calls.OtherTenantFeature);
    }

    [Fact]
    public async Task EntitlementCacheInvalidationConsumer_EvictsTenantCacheForSubscriptionChanged()
    {
        var cacheService = CreateCacheService();
        var consumer = CreateConsumer(cacheService);
        var calls = new CacheFactoryCalls();
        var message = CreateMessage(new TenantSubscriptionChangedV1(
            Guid.NewGuid(),
            OccurredAtUtc,
            TenantId,
            Guid.NewGuid(),
            ActorId,
            previousPlanId: Guid.NewGuid(),
            newPlanId: Guid.NewGuid(),
            previousStatus: "Active",
            newStatus: "Suspended"));

        await SeedTenantAndOtherTenantCacheAsync(cacheService, calls);

        var result = await consumer.ConsumeAsync(message);

        await ReadTenantAndOtherTenantCacheAsync(cacheService, calls);

        Assert.Equal(ConsumedEventExecutionResult.Consumed, result);
        Assert.Equal(2, calls.TenantModule);
        Assert.Equal(2, calls.TenantFeature);
        Assert.Equal(1, calls.OtherTenantModule);
        Assert.Equal(1, calls.OtherTenantFeature);
    }

    [Fact]
    public async Task EntitlementCacheInvalidationConsumer_SkipsDuplicateEventWithoutEvictingAgain()
    {
        var cacheService = CreateCacheService();
        var consumer = CreateConsumer(cacheService);
        var calls = new CacheFactoryCalls();
        var message = CreateMessage(new TenantEntitlementEnabledV1(Guid.NewGuid(), OccurredAtUtc, TenantId, Guid.NewGuid(), ActorId, "HR"));

        await SeedTenantAndOtherTenantCacheAsync(cacheService, calls);
        var first = await consumer.ConsumeAsync(message);
        await ReadTenantAndOtherTenantCacheAsync(cacheService, calls);
        var duplicate = await consumer.ConsumeAsync(message);
        await ReadTenantAndOtherTenantCacheAsync(cacheService, calls);

        Assert.Equal(ConsumedEventExecutionResult.Consumed, first);
        Assert.Equal(ConsumedEventExecutionResult.Duplicate, duplicate);
        Assert.Equal(2, calls.TenantModule);
        Assert.Equal(2, calls.TenantFeature);
    }

    [Fact]
    public async Task EntitlementCacheInvalidationConsumer_IgnoresUnknownEvent()
    {
        var cacheService = CreateCacheService();
        var consumer = CreateConsumer(cacheService);
        var message = new EventTransportMessage(
            Guid.NewGuid(),
            "unknown.event.v1",
            1,
            Guid.NewGuid(),
            null,
            TenantId,
            "Diten.Platform",
            OccurredAtUtc,
            "{}");

        var result = await consumer.ConsumeAsync(message);

        Assert.Null(result);
    }

    [Fact]
    public async Task EntitlementCacheInvalidationConsumer_LogsAndIgnoresInvalidPayload()
    {
        var cacheService = CreateCacheService();
        var consumer = CreateConsumer(cacheService);
        var message = new EventTransportMessage(
            Guid.NewGuid(),
            TenantEntitlementAddedV1.Name,
            TenantEntitlementAddedV1.Version,
            Guid.NewGuid(),
            null,
            TenantId,
            "Diten.Platform",
            OccurredAtUtc,
            "{}");

        var result = await consumer.ConsumeAsync(message);

        Assert.Null(result);
    }

    private static EntitlementCacheInvalidationConsumer CreateConsumer(EntitlementCacheService cacheService)
    {
        return new EntitlementCacheInvalidationConsumer(
            new ConsumedEventStore(
                new InMemoryConsumedEventRepository(),
                NullLogger<ConsumedEventStore>.Instance),
            cacheService,
            NullLogger<EntitlementCacheInvalidationConsumer>.Instance);
    }

    private static EntitlementCacheService CreateCacheService()
    {
        return new EntitlementCacheService(
            new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new EntitlementCacheOptions { CacheTtlSeconds = 300 }));
    }

    private static async Task SeedTenantAndOtherTenantCacheAsync(EntitlementCacheService cacheService, CacheFactoryCalls calls)
    {
        await cacheService.GetOrCreateModuleAsync(TenantId, "HR", () => CreateAllowedModuleAsync("HR", () => calls.TenantModule++));
        await cacheService.GetOrCreateFeatureAsync(TenantId, "PAYROLL", () => CreateAllowedFeatureAsync("PAYROLL", () => calls.TenantFeature++));
        await cacheService.GetOrCreateModuleAsync(OtherTenantId, "HR", () => CreateAllowedModuleAsync("HR", () => calls.OtherTenantModule++));
        await cacheService.GetOrCreateFeatureAsync(OtherTenantId, "PAYROLL", () => CreateAllowedFeatureAsync("PAYROLL", () => calls.OtherTenantFeature++));
    }

    private static async Task ReadTenantAndOtherTenantCacheAsync(EntitlementCacheService cacheService, CacheFactoryCalls calls)
    {
        await cacheService.GetOrCreateModuleAsync(TenantId, "HR", () => CreateAllowedModuleAsync("HR", () => calls.TenantModule++));
        await cacheService.GetOrCreateFeatureAsync(TenantId, "PAYROLL", () => CreateAllowedFeatureAsync("PAYROLL", () => calls.TenantFeature++));
        await cacheService.GetOrCreateModuleAsync(OtherTenantId, "HR", () => CreateAllowedModuleAsync("HR", () => calls.OtherTenantModule++));
        await cacheService.GetOrCreateFeatureAsync(OtherTenantId, "PAYROLL", () => CreateAllowedFeatureAsync("PAYROLL", () => calls.OtherTenantFeature++));
    }

    private static Task<EntitlementCheckResult> CreateAllowedModuleAsync(string moduleCode, Action beforeReturn)
    {
        beforeReturn();
        return Task.FromResult(EntitlementCheckResult.Allowed(EntitlementKind.Module, moduleCode));
    }

    private static Task<EntitlementCheckResult> CreateAllowedFeatureAsync(string featureCode, Action beforeReturn)
    {
        beforeReturn();
        return Task.FromResult(EntitlementCheckResult.Allowed(EntitlementKind.Feature, featureCode));
    }

    private static EventTransportMessage CreateMessage<TEvent>(TEvent @event)
        where TEvent : IIntegrationEvent
    {
        var eventId = (Guid)typeof(TEvent).GetProperty("EventId")!.GetValue(@event)!;
        var tenantId = (Guid)typeof(TEvent).GetProperty("TenantId")!.GetValue(@event)!;
        var correlationId = (Guid)typeof(TEvent).GetProperty("CorrelationId")!.GetValue(@event)!;
        var occurredAtUtc = (DateTimeOffset)typeof(TEvent).GetProperty("OccurredAtUtc")!.GetValue(@event)!;

        return new EventTransportMessage(
            eventId,
            @event.EventName,
            @event.EventVersion,
            correlationId,
            null,
            tenantId,
            "Diten.Platform",
            occurredAtUtc,
            JsonSerializer.Serialize(@event, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    private sealed class CacheFactoryCalls
    {
        public int TenantModule { get; set; }

        public int TenantFeature { get; set; }

        public int OtherTenantModule { get; set; }

        public int OtherTenantFeature { get; set; }
    }

    private sealed class InMemoryConsumedEventRepository : IConsumedEventRepository
    {
        private readonly ConcurrentDictionary<(Guid EventId, string ConsumerName), ConsumedEvent> _items = [];

        public Task<ConsumedEventStartResult> TryStartAsync(ConsumedEvent consumedEvent, CancellationToken cancellationToken = default)
        {
            var key = (consumedEvent.EventId, consumedEvent.ConsumerName);
            var existing = _items.GetOrAdd(key, consumedEvent);
            if (!ReferenceEquals(existing, consumedEvent))
            {
                var status = existing.Status == ConsumedEventStatus.Consumed
                    ? ConsumedEventStartStatus.ConsumedDuplicate
                    : ConsumedEventStartStatus.InFlightDuplicate;
                return Task.FromResult(new ConsumedEventStartResult(status, existing));
            }

            return Task.FromResult(new ConsumedEventStartResult(ConsumedEventStartStatus.Started, consumedEvent));
        }

        public Task MarkConsumedAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default)
        {
            if (_items.TryGetValue((eventId, consumerName), out var item))
            {
                item.MarkConsumed();
            }

            return Task.CompletedTask;
        }

        public Task MarkSkippedDuplicateAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default)
        {
            if (_items.TryGetValue((eventId, consumerName), out var item))
            {
                item.MarkSkippedDuplicate();
            }

            return Task.CompletedTask;
        }

        public Task MarkFailedAsync(Guid eventId, string consumerName, string error, CancellationToken cancellationToken = default)
        {
            if (_items.TryGetValue((eventId, consumerName), out var item))
            {
                item.MarkFailed(error);
            }

            return Task.CompletedTask;
        }

        public Task<ConsumedEvent?> GetAsync(Guid eventId, string consumerName, CancellationToken cancellationToken = default)
        {
            _items.TryGetValue((eventId, consumerName), out var item);
            return Task.FromResult(item);
        }
    }
}
