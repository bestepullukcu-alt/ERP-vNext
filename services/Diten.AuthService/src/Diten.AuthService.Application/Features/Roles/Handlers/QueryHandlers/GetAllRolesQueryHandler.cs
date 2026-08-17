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
    private readonly ITenantContext _tenantContext;

    public GetAllRolesQueryHandler(
        IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository,
        ITenantContext tenantContext)
    {
        _roleRepository = roleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<List<RoleDto>>> Handle(GetAllRolesQuery request, CancellationToken ct)
    {
        var roles = await _roleRepository.GetAllByTenantAsync(_tenantContext.TenantId, ct);
        var dtos = new List<RoleDto>();

        foreach (var role in roles)
        {
            var perms = await _rolePermissionRepository.GetPermissionsByRoleAsync(role.Id, _tenantContext.TenantId, ct);
            dtos.Add(new RoleDto(role.Id, role.Name, role.DisplayName, role.Description, role.IsSystem, perms.Count()));
        }

        return Response<List<RoleDto>>.Success(dtos);
    }
}
