using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;

public sealed class RevokeRoleCommandHandler : IRequestHandler<RevokeRoleCommand, Unit>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<RevokeRoleCommandHandler> _logger;

    public RevokeRoleCommandHandler(
        IRoleRepository roleRepository,
        IUserRoleRepository userRoleRepository,
        ITenantContext tenantContext,
        ILogger<RevokeRoleCommandHandler> logger)
    {
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<Unit> Handle(RevokeRoleCommand request, CancellationToken ct)
    {
        var role = await _roleRepository.GetByIdAndTenantAsync(request.RoleId, _tenantContext.TenantId, ct);
        if (role != null && role.IsSystem)
            throw new InvalidOperationException("Sistem rollerinden kullanıcı kaldırılamaz.");

        await _userRoleRepository.RevokeAsync(request.UserId, request.RoleId, _tenantContext.TenantId, ct);
        return Unit.Value;
    }
}
