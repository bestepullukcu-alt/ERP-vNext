using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Roles.Commands;
using Diten.AuthService.Domain.Entities;
using MediatR;

namespace Diten.AuthService.Application.Features.Roles.Handlers.CommandHandlers;

public sealed class AssignPermissionCommandHandler : IRequestHandler<AssignPermissionCommand, Response<NoContent>>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly ITenantContext _tenantContext;

    public AssignPermissionCommandHandler(
        IRoleRepository roleRepository,
        IPermissionRepository permissionRepository,
        IRolePermissionRepository rolePermissionRepository,
        ITenantContext tenantContext)
    {
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<NoContent>> Handle(AssignPermissionCommand request, CancellationToken ct)
    {
        var role = await _roleRepository.GetByIdAndTenantAsync(request.RoleId, _tenantContext.TenantId, ct);
        if (role == null) return Response<NoContent>.Fail("Role not found.", 404);

        // Permissions are global, so we use ID directly
        // Note: Repository might need module.resource.action check later if needed
        
        await _rolePermissionRepository.AssignAsync(new RolePermission(request.RoleId, request.PermissionId, _tenantContext.TenantId, "System"), ct);
        return Response<NoContent>.Success(204);
    }
}
