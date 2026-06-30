using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Roles.Commands;
using MediatR;

namespace Diten.AuthService.Application.Features.Roles.Handlers.CommandHandlers;

public sealed class UpdateRoleCommandHandler : IRequestHandler<UpdateRoleCommand, Response<RoleDto>>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IRoleAssignmentVersionService _versionService;
    private readonly ITenantContext _tenantContext;

    public UpdateRoleCommandHandler(
        IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository,
        IRoleAssignmentVersionService versionService,
        ITenantContext tenantContext)
    {
        _roleRepository = roleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _versionService = versionService;
        _tenantContext = tenantContext;
    }

    public async Task<Response<RoleDto>> Handle(UpdateRoleCommand request, CancellationToken ct)
    {
        var role = await _roleRepository.GetByIdAndTenantAsync(request.Id, _tenantContext.TenantId, ct);
        if (role == null) return Response<RoleDto>.Fail("Role not found.", 404);

        role.Update(request.DisplayName, request.Description);
        var updated = await _roleRepository.UpdateAsync(role, ct);

        // FU13 — bump the tenant role-assignment version so cached authorization snapshots refresh.
        await _versionService.IncrementAsync(_tenantContext.TenantId, ct);

        var permissions = await _rolePermissionRepository.GetPermissionsByRoleAsync(role.Id, _tenantContext.TenantId, ct);

        return Response<RoleDto>.Success(new RoleDto(updated.Id, updated.Name, updated.DisplayName, updated.Description, updated.IsSystem, permissions.Count()));
    }
}
