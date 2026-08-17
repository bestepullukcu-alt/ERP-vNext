namespace Diten.AuthService.Domain.Entities;

public sealed class UserRole : EntityBase
{
    private UserRole() { }

    public UserRole(Guid userId, Guid roleId, Guid tenantId, string assignedBy)
    {
        UserId = userId;
        RoleId = roleId;
        TenantId = tenantId;
        AssignedAt = DateTime.UtcNow;
        AssignedBy = assignedBy;
        CreatedAt = DateTimeOffset.UtcNow;
        CreatedBy = assignedBy;
    }

    public Guid UserId { get; private set; }
    public Guid RoleId { get; private set; }
    public DateTime AssignedAt { get; private set; }
    public string AssignedBy { get; private set; } = string.Empty;
}
