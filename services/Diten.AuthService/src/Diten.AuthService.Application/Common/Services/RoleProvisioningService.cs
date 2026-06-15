using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Services;

public sealed class RoleProvisioningService : IRoleProvisioningService
{
    private static readonly string[] AdminPermissionKeys =
    [
        "platform.businessreferencedata.read",
        "platform.businessreferencedata.create",
        "platform.businessreferencedata.update",
        "platform.businessreferencedata.version.create",
        "platform.businessreferencedata.version.update",
        "platform.businessreferencedata.version.validate",
        "platform.businessreferencedata.version.submit",
        "platform.businessreferencedata.version.approve",
        "platform.businessreferencedata.version.publish",
        "platform.businessreferencedata.version.publishoverride",
        "platform.businessreferencedata.import.preview",
        "platform.businessreferencedata.import.commit",
        "platform.businessreferencedata.usage.register",
        "platform.businessreferencedata.consumer.read"
    ];

    private static readonly string[] ViewerPermissionKeys =
    [
        "platform.businessreferencedata.read",
        "platform.businessreferencedata.consumer.read"
    ];

    private static readonly (string Name, string DisplayName, string Description)[] DefaultRoles =
    [
        ("Admin", "Yönetici", "Tenant yönetimi için varsayılan yönetici rolü"),
        ("Viewer", "İzleyici", "Tenant için minimum okuma rolü")
    ];

    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;

    public RoleProvisioningService(
        IRoleRepository roleRepository,
        IPermissionRepository permissionRepository,
        IRolePermissionRepository rolePermissionRepository)
    {
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
        _rolePermissionRepository = rolePermissionRepository;
    }

    public async Task EnsureDefaultRolesAsync(Guid tenantId, CancellationToken ct = default)
    {
        foreach (var template in DefaultRoles)
        {
            var role = await _roleRepository.UpsertSystemRoleAsync(
                template.Name,
                template.DisplayName,
                template.Description,
                tenantId,
                ct);

            var permissionKeys = string.Equals(template.Name, "Admin", StringComparison.OrdinalIgnoreCase)
                ? AdminPermissionKeys
                : ViewerPermissionKeys;

            await EnsureRolePermissionsAsync(role.Id, tenantId, permissionKeys, ct);
        }
    }

    private async Task EnsureRolePermissionsAsync(Guid roleId, Guid tenantId, IEnumerable<string> permissionKeys, CancellationToken ct)
    {
        var currentPermissions = (await _rolePermissionRepository.GetPermissionsByRoleAsync(roleId, tenantId, ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var permissionKey in permissionKeys)
        {
            if (currentPermissions.Contains(permissionKey))
            {
                continue;
            }

            var permission = await _permissionRepository.GetByKeyAsync(permissionKey, ct);
            if (permission is null)
            {
                continue;
            }

            await _rolePermissionRepository.AssignAsync(new RolePermission(roleId, permission.Id, tenantId, "system"), ct);
            currentPermissions.Add(permissionKey);
        }
    }
}
