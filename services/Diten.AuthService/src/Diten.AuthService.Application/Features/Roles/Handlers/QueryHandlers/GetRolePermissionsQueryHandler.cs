using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Roles.Queries;
using MediatR;

namespace Diten.AuthService.Application.Features.Roles.Handlers.QueryHandlers;

public sealed class GetRolePermissionsQueryHandler : IRequestHandler<GetRolePermissionsQuery, Response<RolePermissionsDto>>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly ITenantContext _tenantContext;

    public GetRolePermissionsQueryHandler(
        IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository,
        IPermissionRepository permissionRepository,
        ITenantContext tenantContext)
    {
        _roleRepository = roleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _permissionRepository = permissionRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<RolePermissionsDto>> Handle(GetRolePermissionsQuery request, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId;

        // Tenant-scoped lookup: a role from another tenant resolves to null → 404 (no cross-tenant leak).
        var role = await _roleRepository.GetByIdAndTenantAsync(request.RoleId, tenantId, ct);
        if (role is null)
        {
            return Response<RolePermissionsDto>.Fail("Role not found.", 404);
        }

        var grants = await _rolePermissionRepository.GetByRoleAsync(role.Id, tenantId, ct);
        var permissionsById = (await _permissionRepository.GetAllAsync(ct)).ToDictionary(p => p.Id);

        var entries = grants
            .Where(g => permissionsById.ContainsKey(g.PermissionId)) // drop dangling grants defensively
            .Select(g => new RolePermissionEntryDto(
                g.PermissionId,
                permissionsById[g.PermissionId].Key,
                g.GrantSource.ToString(),
                g.SourceModuleCode))
            .OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Response<RolePermissionsDto>.Success(new RolePermissionsDto(role.Id, entries));
    }
}
