using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Common.Services;

public sealed class FullCatalogPermissionGrantService : IFullCatalogPermissionGrantService
{
    // Mirrors DataSeeder.DefaultTenantId / PlatformLoginCommandHandler.PlatformTenantId — the SuperAdmin
    // full-catalog role lives only in the default tenant (DefaultRolePermissionTemplate: default-tenant only).
    private static readonly Guid DefaultTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    // The full-catalog role(s). Today only SuperAdmin holds the entire catalog; kept as a list for future roles.
    private static readonly string[] FullCatalogRoleNames = { DefaultRolePermissionTemplate.SuperAdminRole };

    private const string GrantActor = "system";

    private readonly IRoleRepository _roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly ILogger<FullCatalogPermissionGrantService> _logger;

    public FullCatalogPermissionGrantService(
        IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository,
        IPermissionRepository permissionRepository,
        ILogger<FullCatalogPermissionGrantService> logger)
    {
        _roleRepository = roleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _permissionRepository = permissionRepository;
        _logger = logger;
    }

    public async Task GrantToFullCatalogRolesAsync(Guid permissionId, CancellationToken ct)
    {
        Permission? permission;
        try
        {
            permission = await _permissionRepository.GetByIdAsync(permissionId, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not resolve permission {PermissionId}; full-catalog auto-grant was skipped.",
                permissionId);
            return;
        }

        if (permission is null)
        {
            _logger.LogWarning(
                "Permission {PermissionId} was not found; full-catalog auto-grant was skipped.",
                permissionId);
            return;
        }

        // FU26 — recovery is provisioning-only and can exist solely on ProductIdentityRecoveryOperator through the
        // authoritative entitlement profile. This exact-key guard prevents the legacy full-catalog SuperAdmin bridge
        // from creating a System grant while preserving every other registered permission path.
        if (DefaultRolePermissionTemplate.IsProvisioningOnlyPermission(permission))
        {
            _logger.LogInformation(
                "Provisioning-only permission {PermissionKey} was excluded from full-catalog auto-grant.",
                permission.Key);
            return;
        }

        foreach (var roleName in FullCatalogRoleNames)
        {
            try
            {
                var role = await _roleRepository.GetByNameAndTenantAsync(roleName, DefaultTenantId, ct);
                if (role is null)
                {
                    _logger.LogWarning(
                        "Full-catalog role '{Role}' not found in default tenant; skipping auto-grant for permission {PermissionId}.",
                        roleName,
                        permissionId);
                    continue;
                }

                var existing = await _rolePermissionRepository.GetByRoleAsync(role.Id, DefaultTenantId, ct);
                if (existing.Any(rp => rp.PermissionId == permissionId && !rp.IsDeleted))
                {
                    continue; // idempotent: already granted
                }

                await _rolePermissionRepository.AssignAsync(
                    RolePermission.SystemGrant(role.Id, permissionId, DefaultTenantId, GrantActor),
                    ct);

                _logger.LogInformation(
                    "Auto-granted new permission {PermissionId} to full-catalog role '{Role}'.",
                    permissionId,
                    roleName);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Best-effort: never let a grant failure break the permission sync that triggered it.
                _logger.LogWarning(
                    ex,
                    "Failed to auto-grant permission {PermissionId} to full-catalog role '{Role}'.",
                    permissionId,
                    roleName);
            }
        }
    }
}
