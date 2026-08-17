using Diten.BuildingBlocks.Eventing;
using Diten.Platform.Application.Features.Notifications;
using Diten.Platform.Application.Features.Notifications.Eventing;
using Diten.Platform.Contracts.Events;

namespace Diten.Platform.Application.Features.Tenants.Notifications;

public sealed class TenantCreatedV1NotificationMapper : INotificationEventMapper<TenantCreatedV1>
{
    public const string TemplateKey = "tenant.invite.email";
    public const string MissingRecipientResolutionContractReason =
        "QueueEmailNotificationRequest requires email recipients; TenantCreatedV1 carries InitialAdminUserId only.";

    public QueueEmailNotificationRequest? Map(EventEnvelope<TenantCreatedV1> envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        return null;
    }

    public QueueEmailNotificationRequest? Map(
        EventEnvelope<TenantCreatedV1> envelope,
        IReadOnlyList<EmailRecipientDto> recipients)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        ArgumentNullException.ThrowIfNull(recipients);

        if (recipients.Count == 0)
        {
            return null;
        }

        return new QueueEmailNotificationRequest(
            TemplateKey,
            string.IsNullOrWhiteSpace(envelope.Payload.Locale) ? "en-US" : envelope.Payload.Locale,
            new Dictionary<string, object?>
            {
                ["TenantId"] = envelope.Payload.TenantId,
                ["TenantDisplayName"] = envelope.Payload.TenantDisplayName,
                ["PlanId"] = envelope.Payload.PlanId,
                ["InitialAdminUserId"] = envelope.Payload.InitialAdminUserId,
                ["CreatedAtUtc"] = envelope.Payload.CreatedAtUtc
            },
            recipients,
            CausationId: envelope.CausationId);
    }
}
