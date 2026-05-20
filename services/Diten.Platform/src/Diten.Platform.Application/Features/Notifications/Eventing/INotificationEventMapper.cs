using Diten.BuildingBlocks.Eventing;

namespace Diten.Platform.Application.Features.Notifications.Eventing;

/// <summary>
/// Inbound seam for mapping a notification-worthy internal event to a queue-email request.
/// Concrete mappings are owned by the module that publishes the source event - NOT by MOD-0027.
/// Consumers must route through MOD-0035 public event abstractions and must not invoke
/// transport-specific APIs (RabbitMQ/MassTransit) from notification handlers.
/// </summary>
/// <remarks>
/// <para>
/// <b>Ownership contract:</b>
/// </para>
/// <list type="bullet">
/// <item>
///   <description>
///     MOD-0027 owns this interface and the <see cref="QueueEmailNotificationRequest"/> target contract only.
///     MOD-0027 does not ship concrete mappings.
///   </description>
/// </item>
/// <item>
///   <description>
///     The module that publishes a source event (e.g. tenant onboarding, password reset, subscription expiry)
///     owns the concrete <c>INotificationEventMapper&lt;TEvent&gt;</c> implementation, declares the source event
///     contract in its own pack, and registers the mapper in its own DI module.
///   </description>
/// </item>
/// <item>
///   <description>
///     A mapper implementation should not load tenant context out-of-band; the source event envelope carries
///     <c>TenantId</c> via <see cref="EventEnvelope{TPayload}.TenantId"/>. Mappers should return <c>null</c>
///     when the event should not produce an email (e.g. preference-suppressed, missing template variables).
///   </description>
/// </item>
/// <item>
///   <description>
///     Concrete mappings must not be added to MOD-0027 without a real, approved source event from an owning
///     module pack; speculative or placeholder mappings are forbidden by orchestrator rule 5 (zero hallucination).
///   </description>
/// </item>
/// </list>
/// </remarks>
public interface INotificationEventMapper<TEvent>
    where TEvent : IIntegrationEvent
{
    /// <summary>
    /// Maps an internal event envelope to a queue-email request, or returns null
    /// to indicate the event should not produce an email notification.
    /// </summary>
    QueueEmailNotificationRequest? Map(EventEnvelope<TEvent> envelope);
}
