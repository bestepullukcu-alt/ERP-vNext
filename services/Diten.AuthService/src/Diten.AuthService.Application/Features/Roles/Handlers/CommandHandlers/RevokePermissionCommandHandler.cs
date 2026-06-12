using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Roles.Commands;
using MediatR;

namespace Diten.AuthService.Application.Features.Roles.Handlers.CommandHandlers;

public sealed class RevokePermissionCommandHandler : IRequestHandler<RevokePermissionCommand, Response<NoContent>>
{
    private readonly IRoleRepository _roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITenantContext _tenantContext;

    public RevokePermissionCommandHandler(
        IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository,
        IUserRoleRepository userRoleRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ITenantContext tenantContext)
    {
        _roleRepository = roleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _userRoleRepository = userRoleRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<NoContent>> Handle(RevokePermissionCommand request, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId;

        var role = await _roleRepository.GetByIdAndTenantAsync(request.RoleId, tenantId, ct);
        if (role != null && role.IsSystem)
            return Response<NoContent>.Fail("Permissions cannot be removed from system roles.", 403);

        await _rolePermissionRepository.RevokeAsync(request.RoleId, request.PermissionId, tenantId, ct);

        // AG-STEP-010 / MOD-0018-FU13 Group C (OD-FU13-01, B-Option 1): removing a permission from a role narrows the
        // effective permissions of EVERY user holding that role, so close the refresh path for each holder — a still-
        // valid refresh token must not re-mint an access token carrying the removed grant. Tenant-scoped holder lookup,
        // distinct, sequential fail-fast: a failure here is NOT swallowed and the permission removal above stands (no
        // rollback); residual exposure is bounded by the access-token TTL (≤15 min). No deny-list / event / retry/outbox.
        var holders = await _userRoleRepository.GetUserIdsByRoleAsync(request.RoleId, tenantId, ct);
        foreach (var userId in holders.Distinct())
        {
            await _refreshTokenRepository.RevokeAllByUserAsync(userId, tenantId, ct);
        }

        return Response<NoContent>.Success(204);
    }
}
