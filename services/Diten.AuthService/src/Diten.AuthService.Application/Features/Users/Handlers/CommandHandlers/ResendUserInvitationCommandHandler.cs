using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Users.Commands;
using Diten.AuthService.Application.Features.Users.Services;
using Diten.AuthService.Domain.Entities;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;

// Authed, tenant-scoped: re-issue a fresh 7-day set-password token for a still-pending invited user
// and re-send the invitation email. Tenant comes from context (NOT the request).
public sealed class ResendUserInvitationCommandHandler : IRequestHandler<ResendUserInvitationCommand, Response<InviteLinkResult>>
{
    private static readonly TimeSpan InvitationTokenLifetime = TimeSpan.FromDays(7);

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
    private readonly ILogger<ResendUserInvitationCommandHandler> _logger;

    public ResendUserInvitationCommandHandler(
        IUserRepository userRepository,
        ITenantContext tenantContext,
        ITokenService tokenService,
        IRefreshTokenHasher refreshTokenHasher,
        ITenantUserInvitationEmailService invitationEmailService,
        IHostEnvironment environment,
        IUserAuditRecorder audit,
        ILogger<ResendUserInvitationCommandHandler> logger,
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

    public async Task<Response<InviteLinkResult>> Handle(ResendUserInvitationCommand request, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAndTenantAsync(request.UserId, _tenantContext.TenantId, ct);
        if (user is null) return UserErrorCodes.NotFoundRefusal<InviteLinkResult>();

        // Resend only applies to a pending invitation. Once the user has set their password
        // (active + no change requirement) there is nothing to resend.
        if (!user.MustChangePassword)
        {
            return Response<InviteLinkResult>.Fail("User has already completed setup.", [new ResponseError(UserErrorCodes.SetupAlreadyCompleted)], 409);
        }

        var setupToken = _tokenService.GenerateRefreshToken();

        // BL-529 — "must change password" is not only a pending invitation: an account reset before BL-529 (old password
        // still valid), a tenant administrator with a temporary password. Resending to such an account IS a reset: it
        // follows the same rule (old password and sessions end, AdminPasswordReset), or the old password would still
        // sign in and choose the new one at the forced change.
        if (!user.IsInvitationPending())
        {
            if (_currentUser.UserId == user.Id)
            {
                return Response<InviteLinkResult>.Fail(
                    "You cannot reset the password of the account you are signed in with — use Change password.",
                    [new ResponseError(UserErrorCodes.ResetSelf)],
                    409);
            }

            return await ResetInsteadAsync(user, setupToken, ct);
        }

        user.SetPasswordResetToken(_refreshTokenHasher.Hash(setupToken), DateTime.UtcNow.Add(InvitationTokenLifetime));
        await _userRepository.UpdateForTenantAsync(user, _tenantContext.TenantId, ct);

        var setupUrl = _invitationEmailService.BuildTenantSetPasswordUrl(user.Email, setupToken);
        var emailSent = false;
        try
        {
            await _invitationEmailService.SendTenantUserInvitationAsync(user.Email, setupToken, ct);
            emailSent = true;
        }
        catch when (_environment.IsDevelopment())
        {
            // Dev without SMTP: swallow and rely on the logged link below.
        }
        finally
        {
            // BL-456 — the new token is already saved; the audit row must exist even when the e-mail throws (production re-throws).
            await _audit.RecordAsync(UserAuditEvents.InvitationResent, _tenantContext.TenantId, user.Id,
                new Dictionary<string, object?> { ["emailSent"] = emailSent }, ct);
        }

        _logger.LogInformation("Tenant user invitation re-sent. Id={Id} EmailSent={EmailSent}", user.Id, emailSent);
        if (_environment.IsDevelopment())
        {
            _logger.LogInformation("[DEV] Tenant set-password link for {Email}: {SetupUrl}", user.Email, setupUrl);
        }

        // Dev-only: surface the link so the admin can copy it; null in prod (never leaks).
        return Response<InviteLinkResult>.Success(new InviteLinkResult(_environment.IsDevelopment() ? setupUrl : null), 200);
    }

    private async Task<Response<InviteLinkResult>> ResetInsteadAsync(User user, string setupToken, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId;
        var setupUrl = _invitationEmailService.BuildTenantSetPasswordUrl(user.Email, setupToken);
        var outcome = await AdminPasswordReset.ResetAsync(
            user,
            tenantId,
            AdminResetVia.ResendInvitation,
            _ => AdminPasswordReset.UnusableHash(_passwordHasher, _tokenService),
            u =>
            {
                u.SetPasswordResetToken(_refreshTokenHasher.Hash(setupToken), DateTime.UtcNow.Add(InvitationTokenLifetime));
                u.RequirePasswordChange(null);
            },
            c => _userRepository.GetByIdAndTenantAsync(user.Id, tenantId, c),
            async (u, c) =>
            {
                try
                {
                    await _invitationEmailService.SendTenantUserInvitationAsync(u.Email, setupToken, c);
                    return true;
                }
                catch when (_environment.IsDevelopment())
                {
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

        _logger.LogInformation("Resend reset a non-pending account. Id={Id} SessionsRevoked={SessionsRevoked}", user.Id, outcome.SessionsRevoked);
        return Response<InviteLinkResult>.Success(new InviteLinkResult(_environment.IsDevelopment() ? setupUrl : null), 200);
    }
}
