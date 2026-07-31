using System.Text.Json;
using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Application.Services.Eventing;
using Diten.Platform.Contracts.Events;
using Diten.Platform.Domain.Enums;
using MassTransit;
using EventTransportMessage = Diten.BuildingBlocks.Eventing.EventTransportMessage;
using LegacyEventTransportMessage = Diten.Platform.Application.Contracts.Eventing.EventTransportMessage;

namespace Diten.Platform.Infrastructure.Eventing;

public sealed class TenantLifecycleAuditConsumer :
    IConsumer<EventTransportMessage>,
    IConsumer<LegacyEventTransportMessage>
{
    public const string ConsumerName = nameof(TenantLifecycleAuditConsumer);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ConsumedEventStore _consumedEventStore;
    private readonly IAuditService _auditService;

    public TenantLifecycleAuditConsumer(
        ConsumedEventStore consumedEventStore,
        IAuditService auditService)
    {
        _consumedEventStore = consumedEventStore;
        _auditService = auditService;
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
            TenantCreatedV1.Name => ConsumeTenantEventAsync(
                message,
                Deserialize<TenantCreatedV1>(message),
                BuildCreatedAudit,
                cancellationToken),
            TenantActivatedV1.Name => ConsumeTenantEventAsync(
                message,
                Deserialize<TenantActivatedV1>(message),
                envelope => BuildAuditRequest(envelope, AuditOperation.Activate, envelope.Payload.ActivatedBy),
                cancellationToken),
            TenantSuspendedV1.Name => ConsumeTenantEventAsync(
                message,
                Deserialize<TenantSuspendedV1>(message),
                envelope => BuildAuditRequest(
                    envelope,
                    AuditOperation.Suspend,
                    envelope.Payload.SuspendedBy,
                    new Dictionary<string, object?> { ["Reason"] = RedactedValue() }),
                cancellationToken),
            TenantReactivatedV1.Name => ConsumeTenantEventAsync(
                message,
                Deserialize<TenantReactivatedV1>(message),
                envelope => BuildAuditRequest(envelope, AuditOperation.Reactivate, envelope.Payload.ReactivatedBy),
                cancellationToken),
            TenantCancelledV1.Name => ConsumeTenantEventAsync(
                message,
                Deserialize<TenantCancelledV1>(message),
                envelope => BuildAuditRequest(
                    envelope,
                    AuditOperation.Delete,
                    envelope.Payload.CancelledBy,
                    new Dictionary<string, object?>
                    {
                        ["EffectiveAtUtc"] = envelope.Payload.EffectiveAtUtc,
                        ["Reason"] = envelope.Payload.Reason is null ? null : RedactedValue()
                    }),
                cancellationToken),
            TenantProvisioningCompletedV1.Name => ConsumeTenantEventAsync(
                message,
                Deserialize<TenantProvisioningCompletedV1>(message),
                envelope => BuildAuditRequest(
                    envelope,
                    AuditOperation.Execute,
                    actorId: null,
                    new Dictionary<string, object?>
                    {
                        ["CompletedAtUtc"] = envelope.Payload.CompletedAtUtc,
                        ["StepCount"] = envelope.Payload.Steps.Count,
                        ["Steps"] = envelope.Payload.Steps.ToArray()
                    }),
                cancellationToken),
            TenantProvisioningFailedV1.Name => ConsumeTenantEventAsync(
                message,
                Deserialize<TenantProvisioningFailedV1>(message),
                envelope => BuildAuditRequest(
                    envelope,
                    AuditOperation.Execute,
                    actorId: null,
                    new Dictionary<string, object?>
                    {
                        ["FailedAtUtc"] = envelope.Payload.FailedAtUtc,
                        ["FailedStep"] = envelope.Payload.FailedStep,
                        ["Error"] = envelope.Payload.Error,
                        ["AttemptCount"] = envelope.Payload.AttemptCount
                    },
                    AuditOutcome.Failed),
                cancellationToken),
            _ => Task.FromResult<ConsumedEventExecutionResult?>(null)
        };
    }

    private async Task<ConsumedEventExecutionResult?> ConsumeTenantEventAsync<TEvent>(
        EventTransportMessage message,
        TEvent payload,
        Func<EventEnvelope<TEvent>, AuditAppendRequest> requestFactory,
        CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        var envelope = new EventEnvelope<TEvent>(CreateMetadata(message), payload);
        var result = await _consumedEventStore.ExecuteOnceAsync(
            envelope,
            ConsumerName,
            async ct => await _auditService.AppendAsync(requestFactory(envelope), ct),
            cancellationToken);

        return result;
    }

    private static AuditAppendRequest BuildCreatedAudit(EventEnvelope<TenantCreatedV1> envelope)
    {
        return BuildAuditRequest(
            envelope,
            AuditOperation.Create,
            envelope.Payload.CreatedBy,
            new Dictionary<string, object?>
            {
                ["CreatedAtUtc"] = envelope.Payload.CreatedAtUtc,
                ["PlanId"] = envelope.Payload.PlanId,
                ["InitialAdminUserId"] = envelope.Payload.InitialAdminUserId,
                ["TenantDisplayName"] = RedactedValue(),
                ["Locale"] = envelope.Payload.Locale
            });
    }

    private static AuditAppendRequest BuildAuditRequest<TEvent>(
        EventEnvelope<TEvent> envelope,
        AuditOperation operation,
        Guid? actorId,
        IReadOnlyDictionary<string, object?>? eventMetadata = null,
        AuditOutcome outcome = AuditOutcome.Succeeded)
        where TEvent : IIntegrationEvent
    {
        var tenantId = envelope.TenantId ?? ResolveTenantId(envelope.Payload);
        var metadata = new Dictionary<string, object?>
        {
            ["EventId"] = envelope.EventId,
            ["EventName"] = envelope.EventName,
            ["EventVersion"] = envelope.EventVersion,
            ["TenantId"] = tenantId,
            ["CorrelationId"] = envelope.CorrelationId,
            ["CausationId"] = envelope.CausationId,
            ["OccurredAtUtc"] = envelope.OccurredAtUtc,
            ["Producer"] = envelope.Producer
        };

        if (actorId.HasValue)
        {
            metadata["ActorId"] = actorId;
        }

        if (eventMetadata is not null)
        {
            foreach (var item in eventMetadata)
            {
                metadata[item.Key] = item.Value;
            }
        }

        return new AuditAppendRequest
        {
            CorrelationId = envelope.CorrelationId,
            RequestType = envelope.EventName,
            ActorType = actorId.HasValue ? AuditActorType.PlatformAdministrator : AuditActorType.System,
            ActorId = actorId,
            TargetTenantId = tenantId,
            Category = AuditCategory.TenantAdministration,
            EntityType = "Tenant",
            EntityId = tenantId,
            Operation = operation,
            Outcome = outcome,
            Metadata = metadata,
            OccurredAtUtc = envelope.OccurredAtUtc,
            SourceService = "Diten.Platform",
            SourceModule = "MOD-0009",
            IsPlatformGlobal = true
        };
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

    private static TEvent Deserialize<TEvent>(EventTransportMessage message)
        where TEvent : IIntegrationEvent
    {
        return JsonSerializer.Deserialize<TEvent>(message.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException($"Unable to deserialize {message.EventName} payload.");
    }

    private static Guid ResolveTenantId<TEvent>(TEvent payload)
    {
        var property = typeof(TEvent).GetProperty("TenantId");
        if (property?.GetValue(payload) is Guid tenantId && tenantId != Guid.Empty)
        {
            return tenantId;
        }

        throw new InvalidOperationException($"{typeof(TEvent).Name} payload does not expose a valid TenantId.");
    }

    private static string RedactedValue() => "[REDACTED]";
}
