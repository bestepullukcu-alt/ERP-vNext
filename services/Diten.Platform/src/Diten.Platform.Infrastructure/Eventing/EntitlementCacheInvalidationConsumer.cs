using System.Text.Json;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Services;
using Diten.Platform.Application.Services.Eventing;
using Diten.Platform.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Infrastructure.Eventing;

public sealed class EntitlementCacheInvalidationConsumer : IConsumer<EventTransportMessage>
{
    public const string ConsumerName = nameof(EntitlementCacheInvalidationConsumer);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConsumedEventStore _consumedEventStore;
    private readonly EntitlementCacheService _cacheService;
    private readonly ILogger<EntitlementCacheInvalidationConsumer> _logger;

    public EntitlementCacheInvalidationConsumer(
        ConsumedEventStore consumedEventStore,
        EntitlementCacheService cacheService,
        ILogger<EntitlementCacheInvalidationConsumer> logger)
    {
        _consumedEventStore = consumedEventStore;
        _cacheService = cacheService;
        _logger = logger;
    }

    public Task Consume(ConsumeContext<EventTransportMessage> context)
    {
        return ConsumeAsync(context.Message, context.CancellationToken);
    }

    public Task<ConsumedEventExecutionResult?> ConsumeAsync(
        EventTransportMessage message,
        CancellationToken cancellationToken = default)
    {
        return message.EventName switch
        {
            TenantEntitlementAddedV1.Name => ConsumeTenantInvalidationAsync(message, DeserializeOrNull<TenantEntitlementAddedV1>(message), cancellationToken),
            TenantEntitlementEnabledV1.Name => ConsumeTenantInvalidationAsync(message, DeserializeOrNull<TenantEntitlementEnabledV1>(message), cancellationToken),
            TenantEntitlementDisabledV1.Name => ConsumeTenantInvalidationAsync(message, DeserializeOrNull<TenantEntitlementDisabledV1>(message), cancellationToken),
            TenantEntitlementExpiryUpdatedV1.Name => ConsumeTenantInvalidationAsync(message, DeserializeOrNull<TenantEntitlementExpiryUpdatedV1>(message), cancellationToken),
            TenantEntitlementOverrideRemovedV1.Name => ConsumeTenantInvalidationAsync(message, DeserializeOrNull<TenantEntitlementOverrideRemovedV1>(message), cancellationToken),
            TenantSubscriptionChangedV1.Name => ConsumeTenantInvalidationAsync(message, DeserializeOrNull<TenantSubscriptionChangedV1>(message), cancellationToken),
            _ => Task.FromResult<ConsumedEventExecutionResult?>(null)
        };
    }

    private async Task<ConsumedEventExecutionResult?> ConsumeTenantInvalidationAsync<TEvent>(
        EventTransportMessage message,
        TEvent? payload,
        CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        if (payload is null)
        {
            return null;
        }

        var envelope = new EventEnvelope<TEvent>(CreateMetadata(message), payload);
        return await _consumedEventStore.ExecuteOnceAsync(
            envelope,
            ConsumerName,
            _ =>
            {
                var tenantId = ResolveTenantId(envelope);
                _cacheService.EvictTenant(tenantId);
                _logger.LogInformation(
                    "entitlement.cache.invalidated EventId={EventId} EventName={EventName} TenantId={TenantId} CorrelationId={CorrelationId}",
                    envelope.EventId,
                    envelope.EventName,
                    tenantId,
                    envelope.CorrelationId);
                return Task.CompletedTask;
            },
            cancellationToken);
    }

    private TEvent? DeserializeOrNull<TEvent>(EventTransportMessage message)
        where TEvent : IIntegrationEvent
    {
        try
        {
            var payload = JsonSerializer.Deserialize<TEvent>(message.PayloadJson, JsonOptions);
            if (payload is null)
            {
                _logger.LogWarning(
                    "entitlement.cache.invalidation_payload_empty EventId={EventId} EventName={EventName} CorrelationId={CorrelationId}",
                    message.EventId,
                    message.EventName,
                    message.CorrelationId);
            }

            return payload;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        {
            _logger.LogWarning(
                ex,
                "entitlement.cache.invalidation_payload_invalid EventId={EventId} EventName={EventName} CorrelationId={CorrelationId}",
                message.EventId,
                message.EventName,
                message.CorrelationId);
            return default;
        }
    }

    private static EventMetadata CreateMetadata(EventTransportMessage message)
    {
        return new EventMetadata(
            message.EventId,
            message.EventName,
            message.EventVersion,
            message.CorrelationId,
            message.CausationId,
            message.TenantId,
            message.Producer,
            message.OccurredAtUtc);
    }

    private static Guid ResolveTenantId<TEvent>(EventEnvelope<TEvent> envelope)
        where TEvent : IIntegrationEvent
    {
        if (envelope.TenantId.HasValue && envelope.TenantId.Value != Guid.Empty)
        {
            return envelope.TenantId.Value;
        }

        var property = typeof(TEvent).GetProperty("TenantId");
        if (property?.GetValue(envelope.Payload) is Guid tenantId && tenantId != Guid.Empty)
        {
            return tenantId;
        }

        throw new InvalidOperationException($"{typeof(TEvent).Name} payload does not expose a valid TenantId.");
    }
}
