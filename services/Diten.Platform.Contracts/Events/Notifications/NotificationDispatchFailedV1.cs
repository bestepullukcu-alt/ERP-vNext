using Diten.BuildingBlocks.Eventing;

namespace Diten.Platform.Contracts.Events.Notifications;

public sealed record NotificationDispatchFailedV1(
    Guid DispatchId,
    Guid TenantId,
    string TemplateKey,
    string Locale,
    string ProviderCode,
    string ErrorCode,
    int RetryCount,
    DateTimeOffset? NextRetryAtUtc,
    DateTimeOffset FailedAtUtc,
    string? CorrelationId) : IInternalEvent
{
    public const string Name = "notifications.dispatch.failed.v1";
    public const int Version = 1;

    public string EventName => Name;

    public int EventVersion => Version;
}
