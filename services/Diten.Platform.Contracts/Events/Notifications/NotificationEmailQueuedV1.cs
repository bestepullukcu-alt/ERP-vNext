using Diten.BuildingBlocks.Eventing;

namespace Diten.Platform.Contracts.Events.Notifications;

public sealed record NotificationEmailQueuedV1(
    Guid DispatchId,
    Guid TenantId,
    string TemplateKey,
    Guid? TemplateId,
    string Locale,
    string ProviderCode,
    int RecipientCount,
    DateTimeOffset QueuedAtUtc,
    string? CorrelationId) : IInternalEvent
{
    public const string Name = "notifications.email.queued.v1";
    public const int Version = 1;

    public string EventName => Name;

    public int EventVersion => Version;
}
