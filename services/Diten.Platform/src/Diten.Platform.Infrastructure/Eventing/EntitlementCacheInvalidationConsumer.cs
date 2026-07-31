using System.Text.Json;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Services;
using Diten.Platform.Application.Services.Eventing;
using Diten.Platform.Contracts.Events;
using MassTransit;
using Microsoft.Extensions.Logging;
using EventTransportMessage = Diten.BuildingBlocks.Eventing.EventTransportMessage;
using LegacyEventTransportMessage = Diten.Platform.Application.Contracts.Eventing.EventTransportMessage;

namespace Diten.Platform.Infrastructure.Eventing;

public sealed class EntitlementCacheInvalidationConsumer :
    IConsumer<EventTransportMessage>,
    IConsumer<LegacyEventTransportMessage>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly EntitlementCacheService _cacheService;
    private readonly ILogger<EntitlementCacheInvalidationConsumer> _logger;

    public EntitlementCacheInvalidationConsumer(
        EntitlementCacheService cacheService,
        ILogger<EntitlementCacheInvalidationConsumer> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    public Task Consume(ConsumeContext<EventTransportMessage> context)
    {
        return ConsumeAsync(context.Message, context.CancellationToken);
    }

    public Task Consume(ConsumeContext<LegacyEventTransportMessage> context) =>
        ConsumeAsync(LegacyEventTransportMessageMapper.Map(context.Message), context.CancellationToken);

    public Task<ConsumedEventExecutionResult?> ConsumeAsync(
        EventTransportMessage message,
        CancellationToken cancellationToken = default)
    {
        return message.EventName switch
        {
            TenantEntitlementAddedV1.Name => ConsumeTenantInvalidationAsync(message, DeserializeRequired<TenantEntitlementAddedV1>(message)),
            TenantEntitlementEnabledV1.Name => ConsumeTenantInvalidationAsync(message, DeserializeRequired<TenantEntitlementEnabledV1>(message)),
            TenantEntitlementDisabledV1.Name => ConsumeTenantInvalidationAsync(message, DeserializeRequired<TenantEntitlementDisabledV1>(message)),
            TenantEntitlementExpiryUpdatedV1.Name => ConsumeTenantInvalidationAsync(message, DeserializeRequired<TenantEntitlementExpiryUpdatedV1>(message)),
            TenantEntitlementOverrideRemovedV1.Name => ConsumeTenantInvalidationAsync(message, DeserializeRequired<TenantEntitlementOverrideRemovedV1>(message)),
            TenantSubscriptionChangedV1.Name => ConsumeTenantInvalidationAsync(message, DeserializeRequired<TenantSubscriptionChangedV1>(message)),
            _ => Task.FromResult<ConsumedEventExecutionResult?>(null)
        };
    }

    private Task<ConsumedEventExecutionResult?> ConsumeTenantInvalidationAsync<TEvent>(
        EventTransportMessage message,
        TEvent payload)
        where TEvent : IIntegrationEvent
    {
        var envelope = new EventEnvelope<TEvent>(CreateMetadata(message), payload);
        var tenantId = ResolveTenantId(envelope);
        _cacheService.EvictTenant(tenantId);
        _logger.LogInformation(
            "entitlement.cache.invalidated EventId={EventId} EventName={EventName} TenantId={TenantId} CorrelationId={CorrelationId}",
            envelope.EventId,
            envelope.EventName,
            tenantId,
            envelope.CorrelationId);
        return Task.FromResult<ConsumedEventExecutionResult?>(ConsumedEventExecutionResult.Consumed);
    }

    private TEvent DeserializeRequired<TEvent>(EventTransportMessage message)
        where TEvent : IIntegrationEvent
    {
        try
        {
            var payload = JsonSerializer.Deserialize<TEvent>(message.PayloadJson, JsonOptions);
            if (payload is null)
            {
                throw new JsonException($"{message.EventName} payload is empty.");
            }

            return payload;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or InvalidOperationException)
        {
            _logger.LogError(
                ex,
                "entitlement.cache.invalidation_payload_invalid EventId={EventId} EventName={EventName} CorrelationId={CorrelationId}",
                message.EventId,
                message.EventName,
                message.CorrelationId);
            throw;
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
