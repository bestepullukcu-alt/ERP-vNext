using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Application.Features.Auth.Commands;
using Diten.AuthService.Application.Features.Auth.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Auth.Handlers.CommandHandlers;

public sealed class PlatformLoginCommandHandler : IRequestHandler<PlatformLoginCommand, Response<AuthResponse>>
{
    private static readonly Guid PlatformTenantId = PlatformSystemTenant.Id;

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

        // WP-USERS-ROLES-CLOSE-FIX1 FIX2 item 2 — no account still costs one verification (SignInTiming): the answer's time
        // does not tell an administrator's address from an unknown one.
        if (user is null)
        {
            await SignInTiming.VerifyAgainstNoAccountAsync(_passwordHasher, request.Password, ct);
            return Response<AuthResponse>.Fail("Invalid credentials.", [new ResponseError(AuthRefusalCodes.InvalidCredentials)], 401);
        }
        if (!await _passwordHasher.VerifyAsync(request.Password, user.PasswordHash, ct))
        {
            return Response<AuthResponse>.Fail("Invalid credentials.", [new ResponseError(AuthRefusalCodes.InvalidCredentials)], 401);
        }

        if (!user.IsActive)
        {
            return Response<AuthResponse>.Fail("User is inactive.", [new ResponseError(AuthRefusalCodes.AccountDeactivated)], 401);
        }

        // BL-521 FIX2 item 1 — the actor type of the token comes from the administrator's Platform RECORD, never from
        // Auth's own copy on the user (User.PlatformActorType): that copy is written best-effort AFTER the record is saved,
        // so a sync that fails leaves it saying platform_admin and every login would mint platform_admin for a partner.
        // Item 3 — no answer from Platform is 503 (retry), not "inactive".
        var record = await _platformAdministratorStatusClient.GetActiveActorTypeAsync(user.Email, ct);
        if (record.Verdict == PlatformAdministratorRecordVerdict.Unavailable)
        {
            _logger.LogWarning("Platform login refused: platform administrator status unavailable. UserId={UserId}", user.Id);
            return Response<AuthResponse>.Fail(PlatformAdministratorRecordAnswer.UnavailableMessage, [new ResponseError(AuthRefusalCodes.LoginUnavailable)], 503);
        }

        if (record.Verdict != PlatformAdministratorRecordVerdict.Confirmed)
        {
            _logger.LogWarning("Platform login denied by platform administrator status. UserId={UserId} Email={Email}", user.Id, user.Email);
            return Response<AuthResponse>.Fail("Platform administrator account is inactive.", [new ResponseError(AuthRefusalCodes.AccountDeactivated)], 401);
        }

        // BL-521 FIX7 item 1 - the record is the authority and it says this is a platform administrator: Auth's own copy of the
        // actor type (what the tenant doors and the user repository tell platform accounts apart by) is made to say the same
        // before anything is minted - a targeted write, and the in-memory user is healed too because the sign-in's own replace
        // of the document (below) would otherwise write the old value back.
        await PlatformAccountDiscriminator.EnsureAsync(_userRepository, user, record.ActorType!, _logger, ct);

        if (user.MustChangePassword &&
            user.TemporaryPasswordExpiresAt.HasValue &&
            user.TemporaryPasswordExpiresAt.Value <= DateTime.UtcNow)
        {
            return Response<AuthResponse>.Fail("Temporary password has expired. Please reset your password.", [new ResponseError(AuthRefusalCodes.TemporaryPasswordExpired)], 401);
        }

        // BL-521 FIX4 item 2 — the roles this token is minted with follow the RECORD: Auth's copy is converged to the roles
        // Platform's answer names before it is read below (a sync that was lost — a refused write, a failed convergence —
        // no longer outlives the next sign-in). An older Platform that sends no roles converges nothing.
        // FIX5 item 3 — AT MINT NO PROVISIONING DEFAULT: an empty or unrecognised list means the record grants no platform
        // role, so none is granted (and the account's platform roles are revoked) and the 403 below answers, as it always
        // did for an account without one. FIX4 turned that into a ReadOnly sign-in.
        if (record.Roles is not null)
        {
            await PlatformAdministratorRoleConvergence.ConvergeAtMintAsync(_userRoleRepository, _roleRepository, user.Id, PlatformTenantId, record.Roles, ct);
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
            return Response<AuthResponse>.Fail("Platform admin privileges are required.", [new ResponseError(AuthRefusalCodes.PlatformPrivilegesRequired)], 403);
        }

        var roleIds = await ResolveRoleIdsAsync(roles, PlatformTenantId, ct);
        var permissions = (await _rolePermissionRepository.GetPermissionsByRolesAsync(roleIds, PlatformTenantId, ct))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var actorType = record.ActorType!;
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

        // BL-529 FIX3 — the session is bound to the password it was opened with (SessionPasswordBinding).
        SessionPasswordBinding.Bind(refreshToken, _refreshTokenHasher, user);
        await _refreshTokenRepository.CreateAsync(refreshToken, ct);

        // BL-529 — a reset between this sign-in's read and now ended every session; the token just written must not
        // outlive it (the answer is the wrong-password one: the password checked is no longer the account's).
        if (!await IssuedSessionGuard.StillValidAsync(_userRepository, _refreshTokenRepository, user.Id, PlatformTenantId, user.PasswordHash, refreshTokenStr, ct))
        {
            return Response<AuthResponse>.Fail("Invalid credentials.", [new ResponseError(AuthRefusalCodes.InvalidCredentials)], 401);
        }

        // BL-529 — only the sign-in's own fields: a whole-account write here could undo a reset that landed meanwhile.
        user.RecordLoginSuccess();
        await _userRepository.RecordLoginOutcomeAsync(user, PlatformTenantId, ct);
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
