using System.Text.Json;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Auth.Commands;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.Enums;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Auth.Handlers.CommandHandlers;

public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, Response<AuthResponse>>
{
    // BL-412 (CT benchmark D6, 21 CFR Part 11 attributability) — anonymous self-registration has no current user.
    // The default-role row is written by TENANT POLICY (it picks Viewer, the registrant picks nothing), so it carries
    // the same lowercase non-person actor every other system writer uses. Stamping the new user's own id there would
    // read as a self-granted role in an access / segregation-of-duties review. SystemGrantPathsActorGuardTests pins it.
    private const string SystemActor = "system";

    // The attributable act is the registration itself: one Auth audit event whose actor IS the new user.
    private const string SelfRegisteredEvent = "tenant_user_self_registered";

    private readonly IUserRepository _userRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IRoleProvisioningService _roleProvisioningService;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenHasher _refreshTokenHasher;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITenantLoginSettingsClient _tenantLoginSettingsClient;
    private readonly IPasswordPolicyService _passwordPolicyService;
    private readonly ITenantContext _tenantContext;
    private readonly IAuthAuditService _authAuditService;
    private readonly ILogger<RegisterCommandHandler> _logger;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        IRoleRepository roleRepository,
        IUserRoleRepository userRoleRepository,
        IRoleProvisioningService roleProvisioningService,
        ITokenService tokenService,
        IRefreshTokenHasher refreshTokenHasher,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        ITenantLoginSettingsClient tenantLoginSettingsClient,
        IPasswordPolicyService passwordPolicyService,
        ITenantContext tenantContext,
        IAuthAuditService authAuditService,
        ILogger<RegisterCommandHandler> logger)
    {
        _userRepository = userRepository;
        _roleRepository = roleRepository;
        _userRoleRepository = userRoleRepository;
        _roleProvisioningService = roleProvisioningService;
        _tokenService = tokenService;
        _refreshTokenHasher = refreshTokenHasher;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _tenantLoginSettingsClient = tenantLoginSettingsClient;
        _passwordPolicyService = passwordPolicyService;
        _tenantContext = tenantContext;
        _authAuditService = authAuditService;
        _logger = logger;
    }

    public async Task<Response<AuthResponse>> Handle(RegisterCommand request, CancellationToken ct)
    {
        var existing = await _userRepository.GetByEmailAndTenantAsync(request.Email, _tenantContext.TenantId, ct);
        if (existing != null)
            return Response<AuthResponse>.Fail("Email is already in use.", 409);

        await _passwordPolicyService.ValidateTenantPasswordAsync(_tenantContext.TenantId, null, request.Password, "register", ct);
        var settings = await _tenantLoginSettingsClient.GetAsync(_tenantContext.TenantId, ct);

        var role = await _roleRepository.GetByNameAndTenantAsync("Viewer", _tenantContext.TenantId, ct);
        if (role is null)
        {
            await _roleProvisioningService.EnsureDefaultRolesAsync(_tenantContext.TenantId, ct);
            role = await _roleRepository.GetByNameAndTenantAsync("Viewer", _tenantContext.TenantId, ct);
        }

        if (role is null)
        {
            _logger.LogWarning("Default role could not be resolved after ensure fallback. TenantId={TenantId}", _tenantContext.TenantId);
            return Response<AuthResponse>.Fail("Tenant default roles are not ready.", 422);
        }

        var hashedPassword = _passwordHasher.Hash(request.Password);
        var user = new User(request.Email, hashedPassword, request.FirstName, request.LastName, _tenantContext.TenantId);
        // WP-INFRA-AUTH-ACCOUNT-KIND-01 — an automatic creation path never classifies: self-registration is Unknown,
        // stated here so the intent is readable (and guarded), not merely inherited from the constructor.
        user.SetAccountKind(AccountKind.Unknown);
        var created = await _userRepository.CreateAsync(user, ct);
        await _userRoleRepository.AssignAsync(new UserRole(created.Id, role.Id, _tenantContext.TenantId, SystemActor), ct);

        // BL-412 (D6) — written through the existing Auth audit store (authAuditLogs), like the sibling login and
        // password handlers, right after the state change it records. The actor is the registrant; the metadata says
        // the default role came from tenant policy so the "system" on the role row and this event tell one story.
        // IDs and the role name only — no email or person name (the RbacAuditRecorder no-PII rule).
        await _authAuditService.WriteAsync(
            SelfRegisteredEvent,
            created.Id,
            _tenantContext.TenantId,
            JsonSerializer.Serialize(new
            {
                actorId = created.Id,
                defaultRoleId = role.Id,
                defaultRole = role.Name,
                roleAssignedBy = SystemActor,
                reason = $"self-registration; default role {role.Name} assigned by tenant policy"
            }),
            ct);

        var roles = new[] { role.Name };
        var accessToken = _tokenService.GenerateAccessToken(created, roles, Array.Empty<string>(), settings.SessionTimeoutMinutes);
        var refreshTokenStr = _tokenService.GenerateRefreshToken();
        var refreshTokenHash = _refreshTokenHasher.Hash(refreshTokenStr);

        var refreshToken = new RefreshToken(
            created.Id,
            refreshTokenHash,
            DateTime.UtcNow.AddDays(settings.RefreshTokenLifetimeDays),
            request.RequestIp,
            _tenantContext.TenantId,
            "tenant_user",
            request.UserAgent);
        await _refreshTokenRepository.CreateAsync(refreshToken, ct);

        return Response<AuthResponse>.Success(new AuthResponse(
            accessToken,
            refreshTokenStr,
            refreshToken.ExpiresAt,
            new UserDto(created.Id, created.Email, created.FirstName, created.LastName, created.IsActive, roles, created.TenantId)
        ), 201);
    }
}
