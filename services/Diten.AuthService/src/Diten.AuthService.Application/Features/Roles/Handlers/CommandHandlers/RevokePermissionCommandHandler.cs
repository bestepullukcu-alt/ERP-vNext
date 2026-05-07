using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Roles.Commands;
using MediatR;

namespace Diten.AuthService.Application.Features.Roles.Handlers.CommandHandlers;

public sealed class RevokePermissionCommandHandler : IRequestHandler<RevokePermissionCommand, Response<NoContent>>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly ITenantContext _tenantContext;

    public RevokePermissionCommandHandler(
        IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository,
        ITenantContext tenantContext)
    {
        _roleRepository = roleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<NoContent>> Handle(RevokePermissionCommand request, CancellationToken ct)
    {
        var role = await _roleRepository.GetByIdAndTenantAsync(request.RoleId, _tenantContext.TenantId, ct);
        if (role != null && role.IsSystem) 
            return Response<NoContent>.Fail("Permissions cannot be removed from system roles.", 403);

        await _rolePermissionRepository.RevokeAsync(request.RoleId, request.PermissionId, _tenantContext.TenantId, ct);
        return Response<NoContent>.Success(204);
    }
}
