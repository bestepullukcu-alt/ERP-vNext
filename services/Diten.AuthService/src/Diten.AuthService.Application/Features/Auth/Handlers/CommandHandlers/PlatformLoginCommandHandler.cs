using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Auth.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Auth.Handlers.CommandHandlers;

public sealed class PlatformLoginCommandHandler : IRequestHandler<PlatformLoginCommand, AuthResponse>
{
    private static readonly Guid PlatformTenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string PlatformActorType = "platform_admin";

    private readonly IUserRepository _userRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IRolePermissionRepository _rolePermissionRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenHasher _refreshTokenHasher;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITokenService _tokenService;
    private readonly ILogger<PlatformLoginCommandHandler> _logger;

    public PlatformLoginCommandHandler(
        IUserRepository userRepository,
        IUserRoleRepository userRoleRepository,
        IRoleRepository roleRepository,
        IRolePermissionRepository rolePermissionRepository,
        IPasswordHasher passwordHasher,
        IRefreshTokenHasher refreshTokenHasher,
        IRefreshTokenRepository refreshTokenRepository,
        ITokenService tokenService,
        ILogger<PlatformLoginCommandHandler> logger)
    {
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _roleRepository = roleRepository;
        _rolePermissionRepository = rolePermissionRepository;
        _passwordHasher = passwordHasher;
        _refreshTokenHasher = refreshTokenHasher;
        _refreshTokenRepository = refreshTokenRepository;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<AuthResponse> Handle(PlatformLoginCommand request, CancellationToken ct)
    {
        var user = await _userRepository.GetByEmailAndTenantAsync(request.Email, PlatformTenantId, ct);
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new UnauthorizedAccessException("Invalid credentials.");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("User is inactive.");
        }

        var roles = (await _userRoleRepository.GetRolesByUserAsync(user.Id, PlatformTenantId, ct))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var isPlatformAdmin = roles.Any(r =>
            string.Equals(r, "SuperAdmin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase));

        if (!isPlatformAdmin)
        {
            _logger.LogWarning("Platform login denied. UserId={UserId} Email={Email}", user.Id, user.Email);
            throw new UnauthorizedAccessException("Platform admin privileges are required.");
        }

        var roleIds = await ResolveRoleIdsAsync(roles, PlatformTenantId, ct);
        var permissions = (await _rolePermissionRepository.GetPermissionsByRolesAsync(roleIds, PlatformTenantId, ct))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var accessToken = _tokenService.GeneratePlatformAccessToken(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            PlatformTenantId,
            PlatformActorType,
            roles,
            permissions);

        var refreshTokenStr = _tokenService.GenerateRefreshToken();
        var refreshTokenHash = _refreshTokenHasher.Hash(refreshTokenStr);
        var refreshToken = new Diten.AuthService.Domain.Entities.RefreshToken(
            user.Id,
            refreshTokenHash,
            DateTime.UtcNow.AddDays(7),
            request.RequestIp,
            PlatformTenantId,
            PlatformActorType,
            request.UserAgent);

        await _refreshTokenRepository.CreateAsync(refreshToken, ct);

        return new AuthResponse(
            accessToken,
            refreshTokenStr,
            refreshToken.ExpiresAt,
            new UserDto(user.Id, user.Email, user.FirstName, user.LastName, user.IsActive, roles, null));
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
