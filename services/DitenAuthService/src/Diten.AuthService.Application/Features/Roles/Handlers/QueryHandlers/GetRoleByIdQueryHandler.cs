using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Roles.Queries;
using MediatR;

namespace Diten.AuthService.Application.Features.Roles.Handlers.QueryHandlers;

public sealed class GetRoleByIdQueryHandler : IRequestHandler<GetRoleByIdQuery, RoleDto>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly ITenantContext _tenantContext;

    public GetRoleByIdQueryHandler(
        IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository,
        ITenantContext tenantContext)
    {
        _roleRepository = roleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _tenantContext = tenantContext;
    }

    public async Task<RoleDto> Handle(GetRoleByIdQuery request, CancellationToken ct)
    {
        var role = await _roleRepository.GetByIdAndTenantAsync(request.Id, _tenantContext.TenantId, ct);
        if (role == null) throw new KeyNotFoundException("Rol bulunamadı.");

        var perms = await _rolePermissionRepository.GetPermissionsByRoleAsync(role.Id, _tenantContext.TenantId, ct);

        return new RoleDto(role.Id, role.Name, role.DisplayName, role.Description, role.IsSystem, perms.Count());
    }
}
