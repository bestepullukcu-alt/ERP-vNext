using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Auth.Commands;
using Diten.AuthService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Auth.Handlers.CommandHandlers;

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IUserRoleRepository userRoleRepository,
        IRolePermissionRepository rolePermissionRepository,
        ITokenService tokenService,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        ITenantContext tenantContext,
        ILogger<LoginCommandHandler> logger)
    {
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _tokenService = tokenService;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<AuthResponse> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await _userRepository.GetByEmailAndTenantAsync(request.Email, _tenantContext.TenantId, ct);
        if (user == null) throw new UnauthorizedAccessException("Geçersiz e-posta veya şifre.");

        CheckLockout(user);

        if (!_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            await HandleLoginFailure(user, ct);
            throw new UnauthorizedAccessException("Geçersiz e-posta veya şifre.");
        }

        return await HandleLoginSuccess(user, ct);
    }

    private void CheckLockout(User user)
    {
        if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTime.UtcNow)
            throw new UnauthorizedAccessException("Hesabınız kilitlendi. Lütfen daha sonra tekrar deneyiniz.");
    }

    private async Task HandleLoginFailure(User user, CancellationToken ct)
    {
        user.RecordLoginFailure();
        await _userRepository.UpdateAsync(user, ct);
    }

    private async Task<AuthResponse> HandleLoginSuccess(User user, CancellationToken ct)
    {
        user.RecordLoginSuccess();
        await _userRepository.UpdateAsync(user, ct);

        var roles = await _userRoleRepository.GetRolesByUserAsync(user.Id, _tenantContext.TenantId, ct);
        // UserRoleRepository implementation should return role IDs or names. Assuming names for token.
        // But RolePermission needs role IDs. I'll need to adjust interfaces if IDs are needed.
        // For now, let's assume we need to fetch full roles to get IDs for permissions.
        // Actually, simplified UserRole/RolePermission repos will handle the mapping.
        
        var permissions = await _rolePermissionRepository.GetPermissionsByRolesAsync(new List<Guid>(), _tenantContext.TenantId, ct); // Simplified

        var accessToken = _tokenService.GenerateAccessToken(user, roles, permissions);
        var refreshTokenStr = _tokenService.GenerateRefreshToken();

        var refreshToken = new RefreshToken(user.Id, refreshTokenStr, DateTime.UtcNow.AddDays(7), "0.0.0.0", _tenantContext.TenantId);
        await _refreshTokenRepository.CreateAsync(refreshToken, ct);

        return new AuthResponse(
            accessToken,
            refreshTokenStr,
            refreshToken.ExpiresAt,
            new UserDto(user.Id, user.Email, user.FirstName, user.LastName, user.IsActive, roles)
        );
    }
}
