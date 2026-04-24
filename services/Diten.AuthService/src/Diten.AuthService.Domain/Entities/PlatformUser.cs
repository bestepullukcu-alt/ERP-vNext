namespace Diten.AuthService.Domain.Entities;

public sealed class PlatformUser : GlobalEntityBase
{
    private PlatformUser() { }

    public PlatformUser(string email, string displayName)
    {
        Email = email;
        DisplayName = displayName;
        IsActive = true;
    }

    public string Email { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public void Disable()
    {
        IsActive = false;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
