using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Users.Commands;
using Diten.AuthService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;

public sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, Response<UserDto>>
{
    // Invitation set-password links are valid for 7 days.
    private static readonly TimeSpan InvitationTokenLifetime = TimeSpan.FromDays(7);

    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordPolicyService _passwordPolicyService;
    private readonly ITenantContext _tenantContext;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenHasher _refreshTokenHasher;
    private readonly IHostEnvironment _environment;
    private readonly ITenantUserInvitationEmailService _invitationEmailService;
    private readonly ILogger<CreateUserCommandHandler> _logger;

    public CreateUserCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IPasswordPolicyService passwordPolicyService,
        ITenantContext tenantContext,
        ITokenService tokenService,
        IRefreshTokenHasher refreshTokenHasher,
        IHostEnvironment environment,
        ITenantUserInvitationEmailService invitationEmailService,
        ILogger<CreateUserCommandHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _passwordPolicyService = passwordPolicyService;
        _tenantContext = tenantContext;
        _tokenService = tokenService;
        _refreshTokenHasher = refreshTokenHasher;
        _environment = environment;
        _invitationEmailService = invitationEmailService;
        _logger = logger;
    }

    public async Task<Response<UserDto>> Handle(CreateUserCommand request, CancellationToken ct)
    {
        var existing = await _userRepository.GetByEmailAndTenantAsync(request.Email, _tenantContext.TenantId, ct);
        if (existing != null) return Response<UserDto>.Fail("Email is already in use.", 409);

        return string.IsNullOrWhiteSpace(request.Password)
            ? await CreateByInvitationAsync(request, ct)
            : await CreateSelfServiceAsync(request, ct);
    }

    // ── Self-service: admin supplies the password; user is active immediately (unchanged behavior). ──
    private async Task<Response<UserDto>> CreateSelfServiceAsync(CreateUserCommand request, CancellationToken ct)
    {
        await _passwordPolicyService.ValidateTenantPasswordAsync(_tenantContext.TenantId, null, request.Password!, "create_user", ct);
        var hashedPassword = _passwordHasher.Hash(request.Password!);
        var user = new User(request.Email, hashedPassword, request.FirstName, request.LastName, _tenantContext.TenantId);

        var created = await _userRepository.CreateAsync(user, ct);

        _logger.LogInformation("User created (self-service). Id={Id}", created.Id);

        return Response<UserDto>.Success(
            new UserDto(created.Id, created.Email, created.FirstName, created.LastName, created.IsActive, new List<string>(), created.TenantId),
            201);
    }

    // ── Invitation: no password. The user sets their own via the emailed set-password link. ──
    private async Task<Response<UserDto>> CreateByInvitationAsync(CreateUserCommand request, CancellationToken ct)
    {
        // Placeholder hash — the column is non-null. It is never usable for login: the account is
        // inactive + must-change-password until the set-password link is redeemed.
        var placeholderHash = _passwordHasher.Hash(_tokenService.GenerateRefreshToken());
        var user = new User(request.Email, placeholderHash, request.FirstName, request.LastName, _tenantContext.TenantId);

        var setupToken = _tokenService.GenerateRefreshToken();
        user.SetPasswordResetToken(_refreshTokenHasher.Hash(setupToken), DateTime.UtcNow.Add(InvitationTokenLifetime));
        user.Deactivate();
        user.RequirePasswordChange(null);

        var created = await _userRepository.CreateAsync(user, ct);

        var setupDelivery = await SendInvitationAsync(created.Email, setupToken, ct);

        _logger.LogInformation(
            "User invited (set-password). Id={Id} EmailSent={EmailSent}", created.Id, setupDelivery.EmailSent);
        if (setupDelivery.SetupUrl is not null)
        {
            // Development convenience only — prod never logs the link/token.
            _logger.LogInformation("[DEV] Tenant set-password link for {Email}: {SetupUrl}", created.Email, setupDelivery.SetupUrl);
        }

        return Response<UserDto>.Success(
            new UserDto(created.Id, created.Email, created.FirstName, created.LastName, created.IsActive, new List<string>(), created.TenantId,
                created.LastLoginAt, created.FailedLoginAttempts, created.MustChangePassword, "TenantPolicy", setupDelivery.SetupUrl),
            201);
    }

    // Mirrors PlatformAuthController.SendSetupLinkAsync: in Development, SMTP failures are swallowed and
    // the link is surfaced/logged; in Production the link is null and a send failure propagates.
    private async Task<(string? SetupUrl, bool EmailSent)> SendInvitationAsync(string email, string setupToken, CancellationToken ct)
    {
        var setupUrl = _invitationEmailService.BuildTenantSetPasswordUrl(email, setupToken);
        try
        {
            await _invitationEmailService.SendTenantUserInvitationAsync(email, setupToken, ct);
            return (_environment.IsDevelopment() ? setupUrl : null, true);
        }
        catch when (_environment.IsDevelopment())
        {
            return (setupUrl, false);
        }
    }
}
