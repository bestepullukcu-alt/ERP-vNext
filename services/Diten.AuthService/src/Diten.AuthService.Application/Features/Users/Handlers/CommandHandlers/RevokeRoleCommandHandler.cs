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
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IRoleAssignmentVersionService _versionService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<RevokeRoleCommandHandler> _logger;

    public RevokeRoleCommandHandler(
        IRoleRepository roleRepository,
        IUserRoleRepository userRoleRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IRoleAssignmentVersionService versionService,
        ITenantContext tenantContext,
        ILogger<RevokeRoleCommandHandler> logger)
    {
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _versionService = versionService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<Response<NoContent>> Handle(RevokeRoleCommand request, CancellationToken ct)
    {
        var role = await _roleRepository.GetByIdAndTenantAsync(request.RoleId, _tenantContext.TenantId, ct);
        if (role != null && role.IsSystem)
            return Response<NoContent>.Fail("Cannot remove users from system roles.", 403);

        await _userRoleRepository.RevokeAsync(request.UserId, request.RoleId, _tenantContext.TenantId, ct);

        // FU13 — bump the tenant role-assignment version so cached authorization snapshots are invalidated at once.
        await _versionService.IncrementAsync(_tenantContext.TenantId, ct);

        // AG-STEP-010 / MOD-0018-FU13 Group B (OD-FU13-01): removing a role narrows the user's effective permissions,
        // so close the refresh path immediately — a still-valid refresh token must not re-mint an access token that
        // carries the now-removed grant. If this revoke throws, the role removal above stands (no rollback) and the
        // command fails; residual exposure is bounded by the access-token TTL (≤15 min). No deny-list / event is added.
        await _refreshTokenRepository.RevokeAllByUserAsync(request.UserId, _tenantContext.TenantId, ct);

        return Response<NoContent>.Success(204);
    }
}
