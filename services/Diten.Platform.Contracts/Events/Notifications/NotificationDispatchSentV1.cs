using Diten.BuildingBlocks.Eventing;

namespace Diten.Platform.Contracts.Events.Notifications;

public sealed record NotificationDispatchSentV1(
    Guid DispatchId,
    Guid TenantId,
    string TemplateKey,
    string Locale,
    string ProviderCode,
    string? ProviderMessageId,
    int RetryCount,
    DateTimeOffset SentAtUtc,
    string? CorrelationId) : IInternalEvent
{
    public const string Name = "notifications.dispatch.sent.v1";
    public const int Version = 1;

    public string EventName => Name;

    public int EventVersion => Version;
}
