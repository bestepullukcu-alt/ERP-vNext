using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Auth.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Auth.Handlers.CommandHandlers;

public sealed class PlatformLoginCommandHandler : IRequestHandler<PlatformLoginCommand, Response<AuthResponse>>
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
    private readonly IPlatformAdministratorStatusClient _platformAdministratorStatusClient;
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
        IPlatformAdministratorStatusClient platformAdministratorStatusClient,
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
        _platformAdministratorStatusClient = platformAdministratorStatusClient;
        _logger = logger;
    }

    public async Task<Response<AuthResponse>> Handle(PlatformLoginCommand request, CancellationToken ct)
    {
        var loginIdentifier = request.Email.Trim().ToLowerInvariant();
        var user = loginIdentifier.Contains('@', StringComparison.Ordinal)
            ? await _userRepository.GetByEmailAndTenantAsync(loginIdentifier, PlatformTenantId, ct)
            : await _userRepository.GetByUserNameAndTenantAsync(loginIdentifier, PlatformTenantId, ct);

        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            return Response<AuthResponse>.Fail("Invalid credentials.", 401);
        }

        if (!user.IsActive)
        {
            return Response<AuthResponse>.Fail("User is inactive.", 401);
        }

        if (!await _platformAdministratorStatusClient.IsActiveAsync(user.Email, ct))
        {
            _logger.LogWarning("Platform login denied by platform administrator status. UserId={UserId} Email={Email}", user.Id, user.Email);
            return Response<AuthResponse>.Fail("Platform administrator account is inactive.", 401);
        }

        if (user.MustChangePassword &&
            user.TemporaryPasswordExpiresAt.HasValue &&
            user.TemporaryPasswordExpiresAt.Value <= DateTime.UtcNow)
        {
            return Response<AuthResponse>.Fail("Temporary password has expired. Please reset your password.", 401);
        }

        var roles = (await _userRoleRepository.GetRolesByUserAsync(user.Id, PlatformTenantId, ct))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var isPlatformAdmin = roles.Any(r =>
            string.Equals(r, "SuperAdmin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, "Admin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, "BillingAdmin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, "SupportAdmin", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(r, "ReadOnly", StringComparison.OrdinalIgnoreCase));

        if (!isPlatformAdmin)
        {
            _logger.LogWarning("Platform login denied. UserId={UserId} Email={Email}", user.Id, user.Email);
            return Response<AuthResponse>.Fail("Platform admin privileges are required.", 403);
        }

        var roleIds = await ResolveRoleIdsAsync(roles, PlatformTenantId, ct);
        var permissions = (await _rolePermissionRepository.GetPermissionsByRolesAsync(roleIds, PlatformTenantId, ct))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var actorType = string.IsNullOrWhiteSpace(user.PlatformActorType) ? PlatformActorType : user.PlatformActorType!;
        var accessToken = _tokenService.GeneratePlatformAccessToken(
            user.Id,
            user.Email,
            user.FirstName,
            user.LastName,
            PlatformTenantId,
            actorType,
            roles,
            permissions,
            15,
            user.MustChangePassword);

        var refreshTokenStr = _tokenService.GenerateRefreshToken();
        var refreshTokenHash = _refreshTokenHasher.Hash(refreshTokenStr);
        var refreshToken = new Diten.AuthService.Domain.Entities.RefreshToken(
            user.Id,
            refreshTokenHash,
            DateTime.UtcNow.AddDays(request.RememberMe ? 30 : 7),
            request.RequestIp,
            PlatformTenantId,
            actorType,
            request.UserAgent);

        await _refreshTokenRepository.CreateAsync(refreshToken, ct);
        user.RecordLoginSuccess();
        await _userRepository.UpdateForTenantAsync(user, PlatformTenantId, ct);
        await _platformAdministratorStatusClient.MarkLoginAcceptedAsync(user.Email, ct);

        return Response<AuthResponse>.Success(new AuthResponse(
            accessToken,
            refreshTokenStr,
            refreshToken.ExpiresAt,
            new UserDto(user.Id, user.Email, user.FirstName, user.LastName, user.IsActive, roles, null),
            RequiresPasswordChange: user.MustChangePassword));
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
