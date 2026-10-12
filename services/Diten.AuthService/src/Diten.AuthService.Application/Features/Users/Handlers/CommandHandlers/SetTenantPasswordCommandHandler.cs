using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Commands;
using Diten.AuthService.Application.Features.Users.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;

// Mirrors PlatformAuthController.ResetPassword, but TENANT-scoped and tenant-context-free:
// the (anonymous) caller has no tenant header/JWT, so the user is resolved by token hash.
public sealed class SetTenantPasswordCommandHandler : IRequestHandler<SetTenantPasswordCommand, Response<NoContent>>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordPolicyService _passwordPolicyService;
    private readonly IRefreshTokenHasher _refreshTokenHasher;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ILogger<SetTenantPasswordCommandHandler> _logger;

    public SetTenantPasswordCommandHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IPasswordPolicyService passwordPolicyService,
        IRefreshTokenHasher refreshTokenHasher,
        IRefreshTokenRepository refreshTokenRepository,
        ILogger<SetTenantPasswordCommandHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _passwordPolicyService = passwordPolicyService;
        _refreshTokenHasher = refreshTokenHasher;
        _refreshTokenRepository = refreshTokenRepository;
        _logger = logger;
    }

    public async Task<Response<NoContent>> Handle(SetTenantPasswordCommand request, CancellationToken ct)
    {
        // WP-USERS-ROLES-CLOSE-FIX1 item 3 — the link is read by the one reader the page's pre-check reads it with
        // (TenantPasswordLink): the same three answers, in the same order — the pre-check cannot say more than this door.
        var (linkState, linkUser, linkHash) = await TenantPasswordLink.ReadAsync(_userRepository, _refreshTokenHasher, request.Email, request.Token, ct);
        if (linkState == TenantPasswordLinkState.Invalid)
        {
            return TenantPasswordLink.InvalidAnswer();
        }

        // BL-529 FIX2 — an account an administrator deactivated is not switched back on by a link (an invitation's
        // included: the link activates the account, the deactivation would be undone from outside).
        // FIX8 item 1 (b) — marked, or switched off before BL-529 (inactive, no mark, not a pending invitation). The link is
        // kept: once an administrator activates the account, the same link sets the password.
        if (linkState == TenantPasswordLinkState.Deactivated)
        {
            return TenantPasswordLink.DeactivatedAnswer();
        }

        var user = linkUser!;
        var tokenHash = linkHash!;

        await _passwordPolicyService.ValidateTenantPasswordAsync(user.TenantId, user.Id, request.NewPassword, "tenant_set_password", ct);

        var state = _userRepository.CaptureState(user);
        var wasActive = user.IsActive;
        user.UpdatePassword(_passwordHasher.Hash(request.NewPassword));
        user.ClearPasswordChangeRequirement(); // also clears the one-time token (single use)
        // BL-529 — a password set through the link starts clean: the lockout the old password's failures (or an attacker
        // guessing at it) built up does not outlive it.
        user.ClearLockout();
        user.Activate();
        user.ConfirmEmail();

        // BL-529 FIX2 — written only while this link is still the account's (not replaced by a newer reset, not used by a
        // parallel redemption, not cleared by a deactivation). FIX4 — and while no administrator has deactivated it: a
        // deactivation of a pending invitation keeps the link, so the link alone does not say it. FIX9 (M4) — and while the
        // switch is the one read: an old instance in a rolling deploy deactivating it without the mark meanwhile is not undone
        // (a pending invitation reads false and still matches).
        if (!await _userRepository.TryWriteChangesAsync(user, state, user.TenantId, new UserWriteCondition(PasswordResetTokenHash: tokenHash, IsActive: wasActive, DeactivatedByAdministrator: false), ct))
        {
            return TenantPasswordLink.InvalidAnswer();
        }
        await _refreshTokenRepository.RevokeLiveSessionsAsync(user.Id, user.TenantId, SessionRevocationReasons.PasswordChanged, ct);

        _logger.LogInformation("Tenant user set-password redeemed. Id={Id}", user.Id);
        return Response<NoContent>.Success(204);
    }
}
