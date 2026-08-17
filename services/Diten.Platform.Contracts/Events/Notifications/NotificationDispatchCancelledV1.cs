using Diten.BuildingBlocks.Eventing;

namespace Diten.Platform.Contracts.Events.Notifications;

public sealed record NotificationDispatchCancelledV1(
    Guid DispatchId,
    Guid TenantId,
    string TemplateKey,
    string Locale,
    string ProviderCode,
    DateTimeOffset CancelledAtUtc,
    string? CorrelationId) : IInternalEvent
{
    public const string Name = "notifications.dispatch.cancelled.v1";
    public const int Version = 1;

    public string EventName => Name;

    public int EventVersion => Version;
}
