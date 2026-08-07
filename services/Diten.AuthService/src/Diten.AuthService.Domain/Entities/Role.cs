namespace Diten.AuthService.Domain.Entities;

public sealed class Role : EntityBase
{
    private Role() { }

    public Role(string name, string displayName, string? description, Guid tenantId)
    {
        Name = name;
        DisplayName = displayName;
        Description = description;
        TenantId = tenantId;
        IsSystem = false;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public string Name { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsSystem { get; private set; }
    public long ExplicitGrantValidationFence { get; private set; }

    public void MarkAsSystem() => IsSystem = true;
    
    public void Update(string displayName, string? description)
    {
        DisplayName = displayName;
        Description = description;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
