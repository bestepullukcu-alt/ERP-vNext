using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Common.Services;

/// <summary>
/// The tenant's default roles (Admin, Viewer). WP-0018-ROLE1 (owner decision 2026-10-10): the template is applied to a role ONCE,
/// by the call that creates it (<see cref="DefaultRoleProvisioningRule"/>). Every caller — <c>tenant-activated</c>,
/// <c>tenant-admin-invited</c>, self-registration — may run at any time on any tenant: a role the tenant already has is left
/// exactly as its administrators set it (no key added, none removed); a role it lacks is created from the template and audited.
/// </summary>
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
    private readonly IRbacAuditRecorder _audit;

    public RoleProvisioningService(
        IRoleRepository roleRepository,
        IPermissionRepository permissionRepository,
        IRolePermissionRepository rolePermissionRepository,
        IRbacAuditRecorder audit)
    {
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _audit = audit;
    }

    public async Task EnsureDefaultRolesAsync(Guid tenantId, string trigger, CancellationToken ct = default)
    {
        var catalog = (await _permissionRepository.GetAllAsync(ct)).ToList();

        foreach (var template in DefaultRoles)
        {
            var (role, created) = await _roleRepository.CreateSystemRoleIfMissingAsync(
                template.Name,
                template.DisplayName,
                template.Description,
                tenantId,
                ct);

            var granted = await GrantAsync(role, DefaultRoleProvisioningRule.GrantsFor(role.Name, catalog, created), tenantId, ct);
            if (created)
            {
                await _audit.RecordAsync(DefaultRoleProvisioningRule.AuditEvent, tenantId,
                    new { roleId = role.Id, roleName = role.Name, permissionCount = granted, trigger }, ct);
            }
        }
    }

    // Grants the permissions the rule decided (System source, system actor). A key the role already holds is skipped, so a
    // retried creation never duplicates a grant.
    private async Task<int> GrantAsync(Role role, IReadOnlyList<Permission> grants, Guid tenantId, CancellationToken ct)
    {
        if (grants.Count == 0)
        {
            return 0;
        }

        var existingKeys = (await _rolePermissionRepository.GetPermissionsByRoleAsync(role.Id, tenantId, ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var granted = 0;
        foreach (var permission in grants)
        {
            if (existingKeys.Contains(permission.Key))
            {
                continue;
            }

            await _rolePermissionRepository.AssignAsync(
                RolePermission.SystemGrant(role.Id, permission.Id, tenantId, SystemActor),
                ct);
            granted++;
        }

        return granted;
    }
}
