using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Eventing;
using Diten.Platform.Contracts.Events;

namespace Diten.Platform.Application.Features.Tenants.Notifications;

public sealed class TenantReactivatedV1NotificationMapper : INotificationEventMapper<TenantReactivatedV1>
{
    public const string TemplateKey = "tenant.reactivated.email";
    public const string DefaultLocale = "en-US";
    public const string MissingRecipientResolutionContractReason =
        "QueueEmailNotificationRequest requires email recipients; TenantReactivatedV1 does not carry a notification recipient.";

    public QueueEmailNotificationRequest? Map(EventEnvelope<TenantReactivatedV1> envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return null;
    }

    public QueueEmailNotificationRequest? Map(
        EventEnvelope<TenantReactivatedV1> envelope,
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
                ["ReactivatedAtUtc"] = envelope.Payload.ReactivatedAtUtc
            },
            recipients,
            CausationId: envelope.CausationId);
    }
}
