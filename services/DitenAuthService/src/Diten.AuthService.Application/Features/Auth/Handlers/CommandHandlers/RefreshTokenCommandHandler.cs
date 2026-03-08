using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Auth.Commands;
using Diten.AuthService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Auth.Handlers.CommandHandlers;

public sealed class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<RefreshTokenCommandHandler> _logger;

    public RefreshTokenCommandHandler(
        IUserRepository userRepository,
        IUserRoleRepository userRoleRepository,
        IRolePermissionRepository rolePermissionRepository,
        ITokenService tokenService,
        IRefreshTokenRepository refreshTokenRepository,
        ITenantContext tenantContext,
        ILogger<RefreshTokenCommandHandler> logger)
    {
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
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

        var user = await _userRepository.GetByIdAndTenantAsync(existingToken.UserId, _tenantContext.TenantId, ct);
        if (user == null || !user.IsActive)
            throw new UnauthorizedAccessException("Kullanıcı bulunamadı veya pasif durumda.");

        return await GenerateNewTokens(user, existingToken, ct);
    }

    private async Task<AuthResponse> GenerateNewTokens(User user, RefreshToken oldToken, CancellationToken ct)
    {
        var roles = await _userRoleRepository.GetRolesByUserAsync(user.Id, _tenantContext.TenantId, ct);
        // Simplified permissions
        var permissions = await _rolePermissionRepository.GetPermissionsByRolesAsync(new List<Guid>(), _tenantContext.TenantId, ct);

        var accessToken = _tokenService.GenerateAccessToken(user, roles, permissions);
        var newRefreshTokenStr = _tokenService.GenerateRefreshToken();

        oldToken.Revoke(newRefreshTokenStr);
        await _refreshTokenRepository.UpdateAsync(oldToken, ct);

        var newRefreshToken = new RefreshToken(user.Id, newRefreshTokenStr, DateTime.UtcNow.AddDays(7), "0.0.0.0", _tenantContext.TenantId);
        await _refreshTokenRepository.CreateAsync(newRefreshToken, ct);

        return new AuthResponse(
            accessToken,
            newRefreshTokenStr,
            newRefreshToken.ExpiresAt,
            new UserDto(user.Id, user.Email, user.FirstName, user.LastName, user.IsActive, roles)
        );
    }
}
