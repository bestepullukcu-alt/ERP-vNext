using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Users.Commands;
using Diten.AuthService.Application.Features.Users.Services;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;

// Admin reset (mirror of ResendUserInvitation, guard INVERTED): only for users who already completed
// setup. Issues a fresh 7-day set-password token, forces a password change, and re-sends the link.
// Pending invitations (MustChangePassword == true) are rejected — use Resend for those.
// BL-529 — and the reset INVALIDATES: the old password stops working at once and every open session ends
// (AdminPasswordReset — the one rule every administrator reset path follows).
public sealed class AdminResetPasswordCommandHandler : IRequestHandler<AdminResetPasswordCommand, Response<InviteLinkResult>>
{
    private static readonly TimeSpan InvitationTokenLifetime = TimeSpan.FromDays(7);

    /// <summary>
    /// BL-529 — YOU CANNOT RESET YOURSELF: the reset kills the caller's own password and sessions, and with the e-mail
    /// down there is no way back. Same shape as <see cref="SetUserActiveStatusCommandHandler.SelfDeactivateCode"/>:
    /// refused with a code the screen translates, before anything is written.
    /// </summary>
    public const string SelfResetCode = UserErrorCodes.ResetSelf;

    private readonly IUserRepository _userRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ITokenService _tokenService;
    private readonly IRefreshTokenHasher _refreshTokenHasher;
    private readonly ITenantUserInvitationEmailService _invitationEmailService;
    private readonly IHostEnvironment _environment;
    private readonly IUserAuditRecorder _audit;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly ILogger<AdminResetPasswordCommandHandler> _logger;

    public AdminResetPasswordCommandHandler(
        IUserRepository userRepository,
        ITenantContext tenantContext,
        ITokenService tokenService,
        IRefreshTokenHasher refreshTokenHasher,
        ITenantUserInvitationEmailService invitationEmailService,
        IHostEnvironment environment,
        IUserAuditRecorder audit,
        ILogger<AdminResetPasswordCommandHandler> logger,
        IPasswordHasher passwordHasher,
        IRefreshTokenRepository refreshTokens,
        ICurrentUserAccessor currentUser)
    {
        _passwordHasher = passwordHasher;
        _refreshTokens = refreshTokens;
        _currentUser = currentUser;
        _userRepository = userRepository;
        _tenantContext = tenantContext;
        _tokenService = tokenService;
        _refreshTokenHasher = refreshTokenHasher;
        _invitationEmailService = invitationEmailService;
        _environment = environment;
        _audit = audit;
        _logger = logger;
    }

    public async Task<Response<InviteLinkResult>> Handle(AdminResetPasswordCommand request, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId;
        var user = await _userRepository.GetByIdAndTenantAsync(request.UserId, tenantId, ct);
        if (user is null) return UserErrorCodes.NotFoundRefusal<InviteLinkResult>();

        if (_currentUser.UserId == request.UserId)
        {
            return Response<InviteLinkResult>.Fail(
                "You cannot reset the password of the account you are signed in with — use Change password.",
                [new ResponseError(SelfResetCode)],
                409);
        }

        // Inverted guard: a still-pending invite should be re-sent, not "reset".
        if (user.MustChangePassword)
        {
            return Response<InviteLinkResult>.Fail("User invitation is still pending — use Resend instead.", [new ResponseError(UserErrorCodes.PasswordSetupPending)], 409);
        }

        string? setupUrl = null;
        var setupToken = _tokenService.GenerateRefreshToken();

        // BL-529 — the old password and every open session end here, the link is written with them (one conditional write).
        var outcome = await AdminPasswordReset.ResetAsync(
            user,
            tenantId,
            AdminResetVia.UsersScreen,
            _ => AdminPasswordReset.UnusableHash(_passwordHasher, _tokenService),
            u =>
            {
                u.SetPasswordResetToken(_refreshTokenHasher.Hash(setupToken), DateTime.UtcNow.Add(InvitationTokenLifetime));
                u.RequirePasswordChange(null); // force change until the link is redeemed
            },
            c => _userRepository.GetByIdAndTenantAsync(request.UserId, tenantId, c),
            async (u, c) =>
            {
                setupUrl = _invitationEmailService.BuildTenantSetPasswordUrl(u.Email, setupToken);
                try
                {
                    // BL-454 — a reset of an account in use: its own words, stated here rather than guessed from the record.
                    await _invitationEmailService.SendTenantUserPasswordResetAsync(u.Email, setupToken, c);
                    return true;
                }
                catch when (_environment.IsDevelopment())
                {
                    // Dev without SMTP: swallow and rely on the logged link below.
                    return false;
                }
            },
            _userRepository,
            _refreshTokens,
            _audit,
            ct);

        if (outcome.Conflict)
        {
            return Response<InviteLinkResult>.Fail(
                "The account changed while it was being reset; nothing stale was written. Reset it again.",
                [new ResponseError(UserErrorCodes.ResetConflict)],
                409);
        }

        if (!outcome.Succeeded) return UserErrorCodes.NotFoundRefusal<InviteLinkResult>();

        _logger.LogInformation("Admin password reset issued. Id={Id} SessionsRevoked={SessionsRevoked}", user.Id, outcome.SessionsRevoked);
        if (_environment.IsDevelopment())
        {
            _logger.LogInformation("[DEV] Tenant set-password link for {Email}: {SetupUrl}", user.Email, setupUrl);
        }

        // Dev-only: surface the link so the admin can copy it; null in prod (never leaks).
        return Response<InviteLinkResult>.Success(new InviteLinkResult(_environment.IsDevelopment() ? setupUrl : null), 200);
    }
}
