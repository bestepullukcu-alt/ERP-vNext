using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Auth.Commands;
using Diten.AuthService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Diten.AuthService.Application.Features.Auth.Handlers.CommandHandlers;

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IUserRepository userRepository,
        IUserRoleRepository userRoleRepository,
        IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository,
        ITokenService tokenService,
        IRefreshTokenRepository refreshTokenRepository,
        ITenantContext tenantContext,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _roleRepository = roleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _tokenService = tokenService;
        _refreshTokenRepository = refreshTokenRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<AuthResponse> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        var existingToken = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken, ct);
        if (existingToken == null) throw new UnauthorizedAccessException("Geçersiz yenileme belirteci.");

        if (existingToken.RevokedAt != null)
        {
            await _refreshTokenRepository.RevokeAllByUserAsync(existingToken.UserId, _tenantContext.TenantId, ct);
            throw new UnauthorizedAccessException("Güvenlik ihlali tespit edildi. Lütfen tekrar oturum açın.");
        }

        if (existingToken.IsExpired)
            throw new UnauthorizedAccessException("Yenileme belirtecinin süresi dolmuş.");

        var principal = _tokenService.GetPrincipalFromExpiredToken(request.AccessToken);
        var actorType = principal.FindFirst("actor_type")?.Value;
        var subject = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                      ?? principal.FindFirst("sub")?.Value;
        var tokenTenantId = principal.FindFirst("tenant_id")?.Value;

        if (!Guid.TryParse(subject, out var accessTokenUserId) || accessTokenUserId != existingToken.UserId)
        {
            throw new UnauthorizedAccessException("Access token user mismatch.");
        }

        if (!Guid.TryParse(tokenTenantId, out var accessTokenTenantId) || accessTokenTenantId != existingToken.TenantId)
        {
            throw new UnauthorizedAccessException("Access token tenant mismatch.");
        }

        if (!string.Equals(existingToken.ActorType, actorType, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("Access token actor mismatch.");
        }

        var user = await _userRepository.GetByIdAndTenantAsync(existingToken.UserId, _tenantContext.TenantId, ct);
        if (user == null || !user.IsActive)
            throw new UnauthorizedAccessException("Kullanıcı bulunamadı veya pasif durumda.");

        return await GenerateNewTokens(user, existingToken, actorType, ct);
    }

    private async Task<AuthResponse> GenerateNewTokens(User user, RefreshToken oldToken, string? actorType, CancellationToken ct)
    {
        var roles = await _userRoleRepository.GetRolesByUserAsync(user.Id, _tenantContext.TenantId, ct);
        var roleIds = await ResolveRoleIdsAsync(roles, _tenantContext.TenantId, ct);
        var permissions = await _rolePermissionRepository.GetPermissionsByRolesAsync(roleIds, _tenantContext.TenantId, ct);

        var isPlatformActor = IsPlatformActor(actorType);

        var accessToken = isPlatformActor
            ? _tokenService.GeneratePlatformAccessToken(
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.TenantId,
                actorType ?? "platform_admin",
                roles,
                permissions)
            : _tokenService.GenerateAccessToken(user, roles, permissions);
        var newRefreshTokenStr = _tokenService.GenerateRefreshToken();

        oldToken.Revoke(newRefreshTokenStr);
        await _refreshTokenRepository.UpdateAsync(oldToken, ct);

        var newRefreshToken = new RefreshToken(
            user.Id,
            newRefreshTokenStr,
            DateTime.UtcNow.AddDays(7),
            "0.0.0.0",
            _tenantContext.TenantId,
            actorType ?? "tenant_user");
        await _refreshTokenRepository.CreateAsync(newRefreshToken, ct);

        return new AuthResponse(
            accessToken,
            newRefreshTokenStr,
            newRefreshToken.ExpiresAt,
            new UserDto(
                user.Id,
                user.Email,
                user.FirstName,
                user.LastName,
                user.IsActive,
                roles,
                isPlatformActor ? null : user.TenantId)
        );
    }

    private static bool IsPlatformActor(string? actorType)
    {
        return string.Equals(actorType, "platform_admin", StringComparison.OrdinalIgnoreCase)
               || string.Equals(actorType, "partner_admin", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<List<Guid>> ResolveRoleIdsAsync(IEnumerable<string> roleNames, Guid tenantId, CancellationToken ct)
    {
        var roleIds = new List<Guid>();

        foreach (var roleName in roleNames.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var role = await _roleRepository.GetByNameAndTenantAsync(roleName, tenantId, ct);
            if (role is not null)
            {
                roleIds.Add(role.Id);
            }
        }

        return roleIds;
    }
}
