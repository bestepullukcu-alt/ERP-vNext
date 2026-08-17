namespace Diten.Platform.Domain.Entities.Notifications;

public sealed class EmailRecipient
{
    public string Email { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
}
