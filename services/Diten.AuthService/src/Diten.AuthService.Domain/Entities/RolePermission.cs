namespace Diten.AuthService.Domain.Entities;

public sealed class RolePermission : EntityBase
{
    private RolePermission() { }

    /// <summary>
    /// Backward-compatible constructor for operator-driven assignment (the roles API). Tags the
    /// grant as <see cref="GrantSource.Manual"/>; existing call sites keep their behaviour.
    /// </summary>
    public RolePermission(Guid roleId, Guid permissionId, Guid tenantId, string assignedBy)
        : this(roleId, permissionId, tenantId, assignedBy, GrantSource.Manual, sourceModuleCode: null)
    {
    }

    public RolePermission(Guid roleId, Guid permissionId, Guid tenantId, string assignedBy, GrantSource grantSource, string? sourceModuleCode)
    {
        RoleId = roleId;
        PermissionId = permissionId;
        TenantId = tenantId;
        AssignedAt = DateTime.UtcNow;
        AssignedBy = assignedBy;
        GrantSource = grantSource;
        SourceModuleCode = sourceModuleCode;
        CreatedAt = DateTimeOffset.UtcNow;
        CreatedBy = assignedBy;
    }

    public Guid RoleId { get; private set; }
    public Guid PermissionId { get; private set; }
    public DateTime AssignedAt { get; private set; }
    public string AssignedBy { get; private set; } = string.Empty;

    /// <summary>Origin of this grant. Legacy documents without the field deserialize to <see cref="GrantSource.System"/>.</summary>
    public GrantSource GrantSource { get; private set; }

    /// <summary>For <see cref="GrantSource.Module"/> grants, the entitlement module that produced it; otherwise null.</summary>
    public string? SourceModuleCode { get; private set; }

    /// <summary>Baseline grant from provisioning/seed.</summary>
    public static RolePermission SystemGrant(Guid roleId, Guid permissionId, Guid tenantId, string assignedBy)
        => new(roleId, permissionId, tenantId, assignedBy, GrantSource.System, sourceModuleCode: null);

    /// <summary>Grant produced by a tenant module entitlement, tagged with its source module code.</summary>
    public static RolePermission ModuleGrant(Guid roleId, Guid permissionId, Guid tenantId, string assignedBy, string sourceModuleCode)
        => new(roleId, permissionId, tenantId, assignedBy, GrantSource.Module, sourceModuleCode);

    /// <summary>Grant assigned manually through the roles API.</summary>
    public static RolePermission ManualGrant(Guid roleId, Guid permissionId, Guid tenantId, string assignedBy)
        => new(roleId, permissionId, tenantId, assignedBy, GrantSource.Manual, sourceModuleCode: null);
}
