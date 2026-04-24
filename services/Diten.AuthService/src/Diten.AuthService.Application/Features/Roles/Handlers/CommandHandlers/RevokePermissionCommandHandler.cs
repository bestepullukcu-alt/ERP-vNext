using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Roles.Commands;
using MediatR;

namespace Diten.AuthService.Application.Features.Roles.Handlers.CommandHandlers;

public sealed class RevokePermissionCommandHandler : IRequestHandler<RevokePermissionCommand, Unit>
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

    public async Task<Unit> Handle(RevokePermissionCommand request, CancellationToken ct)
    {
        var role = await _roleRepository.GetByIdAndTenantAsync(request.RoleId, _tenantContext.TenantId, ct);
        if (role != null && role.IsSystem) 
            throw new InvalidOperationException("Sistem rollerinden yetki kaldırılamaz.");

        await _rolePermissionRepository.RevokeAsync(request.RoleId, request.PermissionId, _tenantContext.TenantId, ct);
        return Unit.Value;
    }
}
