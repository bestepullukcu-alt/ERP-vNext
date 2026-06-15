using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Services;

public sealed class RoleProvisioningService : IRoleProvisioningService
{
    private const string SystemActor = "system";

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
        var catalog = (await _permissionRepository.GetAllAsync(ct)).ToList();

        foreach (var template in DefaultRoles)
        {
            var role = await _roleRepository.UpsertSystemRoleAsync(
                template.Name,
                template.DisplayName,
                template.Description,
                tenantId,
                ct);

            await EnsureBaselineGrantsAsync(role, catalog, tenantId, ct);
        }
    }

    // Attaches the baseline permissions for the role using the shared template (same selection the
    // DataSeeder uses, so the default tenant and every provisioned tenant stay in lock-step).
    // Idempotent: existing grants are skipped, so re-running on retry produces no duplicates.
    private async Task EnsureBaselineGrantsAsync(Role role, IReadOnlyList<Permission> catalog, Guid tenantId, CancellationToken ct)
    {
        var baseline = DefaultRolePermissionTemplate.SelectFor(role.Name, catalog);
        if (baseline.Count == 0)
        {
            return;
        }

        var existingKeys = (await _rolePermissionRepository.GetPermissionsByRoleAsync(role.Id, tenantId, ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var permission in baseline)
        {
            if (existingKeys.Contains(permission.Key))
            {
                continue;
            }

            await _rolePermissionRepository.AssignAsync(
                RolePermission.SystemGrant(role.Id, permission.Id, tenantId, SystemActor),
                ct);
        }
    }
}
