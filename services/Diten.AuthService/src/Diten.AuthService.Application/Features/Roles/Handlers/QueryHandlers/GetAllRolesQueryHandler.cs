using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Roles.Queries;
using MediatR;

namespace Diten.AuthService.Application.Features.Roles.Handlers.QueryHandlers;

public sealed class GetAllRolesQueryHandler : IRequestHandler<GetAllRolesQuery, Response<List<RoleDto>>>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IUserRepository _userRepository;
    private readonly ITenantContext _tenantContext;

    public GetAllRolesQueryHandler(
        IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository,
        IUserRoleRepository userRoleRepository,
        IUserRepository userRepository,
        ITenantContext tenantContext)
    {
        _roleRepository = roleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _userRoleRepository = userRoleRepository;
        _userRepository = userRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<List<RoleDto>>> Handle(GetAllRolesQuery request, CancellationToken ct)
    {
        var roles = await _roleRepository.GetAllByTenantAsync(_tenantContext.TenantId, ct);
        var dtos = new List<RoleDto>();

        // N+1 over roles is acceptable here — tenant role counts are small (typically < 30).
        foreach (var role in roles)
        {
            // Permission keys are "module.resource.action"; group by the leading module segment.
            var permKeys = (await _rolePermissionRepository.GetPermissionsByRoleAsync(role.Id, _tenantContext.TenantId, ct)).ToList();
            var modulePermissions = permKeys
                .GroupBy(k => k.Split('.', 2)[0])
                .ToDictionary(g => g.Key, g => g.Count());

            // BL-521 FIX7 item 3 - the holder count a tenant actor sees leaves the platform accounts out (they are absent from
            // every user list it gets; a count that included them would say a role has a holder the list does not show).
            var holders = await _userRoleRepository.GetUserIdsByRoleAsync(role.Id, _tenantContext.TenantId, ct);
            var userIds = await _userRepository.VisibleUserIdsAsync(_tenantContext.TenantId, holders, ct);

            dtos.Add(new RoleDto(
                role.Id, role.Name, role.DisplayName, role.Description, role.IsSystem,
                permKeys.Count, userIds.Count, modulePermissions));
        }

        return Response<List<RoleDto>>.Success(dtos);
    }
}
