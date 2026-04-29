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
    private const string TenantActorType = "tenant_user";

    private readonly IUserRepository _userRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenHasher _refreshTokenHasher;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuthAuditService _authAuditService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IUserRoleRepository userRoleRepository,
        IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository,
        ITokenService tokenService,
        IRefreshTokenHasher refreshTokenHasher,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        IAuthAuditService authAuditService,
        ITenantContext tenantContext,
        ILogger<LoginCommandHandler> logger)
    {
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _roleRepository = roleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _tokenService = tokenService;
        _refreshTokenHasher = refreshTokenHasher;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _authAuditService = authAuditService;
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

        return await HandleLoginSuccess(user, request, ct);
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

    private async Task<AuthResponse> HandleLoginSuccess(User user, LoginCommand request, CancellationToken ct)
    {
        user.RecordLoginSuccess();
        await _userRepository.UpdateAsync(user, ct);

        var roleValues = await _userRoleRepository.GetRolesByUserAsync(user.Id, _tenantContext.TenantId, ct);
        var roles = roleValues?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];

        if (roles.Length == 0)
        {
            _logger.LogWarning(
                "Login produced empty role set. UserId={UserId} TenantId={TenantId} Email={Email}",
                user.Id,
                _tenantContext.TenantId,
                user.Email);

            await _authAuditService.WriteEmptyRoleLoginAsync(user.Id, _tenantContext.TenantId, user.Email, ct);
        }

        var roleIds = await ResolveRoleIdsAsync(roles, _tenantContext.TenantId, ct);
        var permissions = (await _rolePermissionRepository
                .GetPermissionsByRolesAsync(roleIds, _tenantContext.TenantId, ct))
            ?.Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];

        var accessToken = _tokenService.GenerateAccessToken(user, roles, permissions);
        var refreshTokenStr = _tokenService.GenerateRefreshToken();
        var refreshTokenHash = _refreshTokenHasher.Hash(refreshTokenStr);

        var refreshToken = new RefreshToken(
            user.Id,
            refreshTokenHash,
            DateTime.UtcNow.AddDays(7),
            request.RequestIp,
            _tenantContext.TenantId,
            TenantActorType,
            request.UserAgent);
        await _refreshTokenRepository.CreateAsync(refreshToken, ct);

        return new AuthResponse(
            accessToken,
            refreshTokenStr,
            refreshToken.ExpiresAt,
            new UserDto(user.Id, user.Email, user.FirstName, user.LastName, user.IsActive, roles, user.TenantId)
        );
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
