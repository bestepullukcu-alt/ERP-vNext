namespace Diten.AuthService.Domain.Entities;

public sealed class Permission : GlobalEntityBase
{
    private Permission() { }

    public Permission(string module, string resource, string action, string displayName, string? description)
    {
        Module = module;
        Resource = resource;
        Action = action;
        Key = $"{module}.{resource}.{action}".ToLowerInvariant();
        DisplayName = displayName;
        Description = description;
        IsSystem = true;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public string Module { get; private set; } = string.Empty;
    public string Resource { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string Key { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsSystem { get; private set; }

    public void MarkAsUserDefined() => IsSystem = false;
    
    public void Update(string displayName, string? description)
    {
        DisplayName = displayName;
        Description = description;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
