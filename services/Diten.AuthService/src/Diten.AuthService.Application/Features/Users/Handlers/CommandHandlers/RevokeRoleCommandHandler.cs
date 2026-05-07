using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;

public sealed class RevokeRoleCommandHandler : IRequestHandler<RevokeRoleCommand, Response<NoContent>>
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

    public async Task<Response<NoContent>> Handle(RevokeRoleCommand request, CancellationToken ct)
    {
        var role = await _roleRepository.GetByIdAndTenantAsync(request.RoleId, _tenantContext.TenantId, ct);
        if (role != null && role.IsSystem)
            return Response<NoContent>.Fail("Cannot remove users from system roles.", 403);

        await _userRoleRepository.RevokeAsync(request.UserId, request.RoleId, _tenantContext.TenantId, ct);
        return Response<NoContent>.Success(204);
    }
}
