namespace Diten.AuthService.Domain.Entities;

public sealed class RolePermission : EntityBase
{
    private RolePermission() { }

    public RolePermission(Guid roleId, Guid permissionId, Guid tenantId, string assignedBy)
    {
        RoleId = roleId;
        PermissionId = permissionId;
        TenantId = tenantId;
        AssignedAt = DateTime.UtcNow;
        AssignedBy = assignedBy;
        CreatedAt = DateTimeOffset.UtcNow;
        CreatedBy = assignedBy;
    }

    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    public DateTime AssignedAt { get; private set; }
    public string AssignedBy { get; private set; } = string.Empty;
}
