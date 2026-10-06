namespace Diten.Platform.Domain.Entities.Notifications;

/// <summary>BL-374 — a calendar/file attachment persisted on the dispatch row itself, so a background retry
/// (<c>EmailDispatchJob</c>) can carry the same attachment the first send did.</summary>
public sealed class NotificationDispatchAttachment
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public byte[] Content { get; set; } = [];
}
