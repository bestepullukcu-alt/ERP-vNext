using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Eventing;
using Diten.Platform.Contracts.Events;

namespace Diten.Platform.Application.Features.Tenants.Notifications;

public sealed class TenantSuspendedV1NotificationMapper : INotificationEventMapper<TenantSuspendedV1>
{
    public const string TemplateKey = "tenant.suspended.email";
    public const string DefaultLocale = "en-US";
    public const string MissingRecipientResolutionContractReason =
        "QueueEmailNotificationRequest requires email recipients; TenantSuspendedV1 does not carry a notification recipient.";

    public QueueEmailNotificationRequest? Map(EventEnvelope<TenantSuspendedV1> envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return null;
    }

    public QueueEmailNotificationRequest? Map(
        EventEnvelope<TenantSuspendedV1> envelope,
        IReadOnlyList<EmailRecipientDto> recipients,
        string? locale)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(recipients);

        if (recipients.Count == 0)
        {
            return null;
        }

        return new QueueEmailNotificationRequest(
            TemplateKey,
            string.IsNullOrWhiteSpace(locale) ? DefaultLocale : locale,
            new Dictionary<string, object?>
            {
                ["TenantId"] = envelope.Payload.TenantId,
                ["Reason"] = envelope.Payload.Reason,
                ["SuspendedAtUtc"] = envelope.Payload.SuspendedAtUtc
            },
            recipients,
            CausationId: envelope.CausationId);
    }
}
