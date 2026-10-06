using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;

// Mirrors PlatformAuthController.ResetPassword, but TENANT-scoped and tenant-context-free:
// the (anonymous) caller has no tenant header/JWT, so the user is resolved by token hash.
public sealed class SetTenantPasswordCommandHandler : IRequestHandler<SetTenantPasswordCommand, Response<NoContent>>
{
    private const string InvalidTokenMessage = "Password setup link is invalid or expired.";

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
        var normalizedEmail = (request.Email ?? string.Empty).Trim().ToLowerInvariant();
        var tokenHash = _refreshTokenHasher.Hash(request.Token);
        var user = await _userRepository.GetByPasswordResetTokenHashAsync(tokenHash, ct);

        // Generic failure for every mismatch (don't reveal which check failed). The email is
        // re-verified against the token-owner so a leaked token alone can't target a different email.
        if (user is null ||
            string.IsNullOrWhiteSpace(user.PasswordResetTokenHash) ||
            user.PasswordResetTokenExpiresAt is null ||
            user.PasswordResetTokenExpiresAt <= DateTime.UtcNow ||
            // BL-529 FIX7 item 5 — constant-time, as the platform door (hygiene: the stored value is a keyed hash).
            !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(user.PasswordResetTokenHash),
                System.Text.Encoding.UTF8.GetBytes(tokenHash)) ||
            !string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            return Response<NoContent>.Fail(InvalidTokenMessage, 400);
        }

        // BL-529 FIX2 — an account an administrator deactivated is not switched back on by a link (an invitation's
        // included: the link activates the account, the deactivation would be undone from outside).
        if (user.DeactivatedByAdministrator)
        {
            return Response<NoContent>.Fail(
                "This account has been deactivated by an administrator.",
                [new ResponseError(AuthRefusalCodes.AccountDeactivated)],
                409);
        }

        await _passwordPolicyService.ValidateTenantPasswordAsync(user.TenantId, user.Id, request.NewPassword, "tenant_set_password", ct);

        var state = _userRepository.CaptureState(user);
        user.UpdatePassword(_passwordHasher.Hash(request.NewPassword));
        user.ClearPasswordChangeRequirement(); // also clears the one-time token (single use)
        // BL-529 — a password set through the link starts clean: the lockout the old password's failures (or an attacker
        // guessing at it) built up does not outlive it.
        user.ClearLockout();
        user.Activate();
        user.ConfirmEmail();

        // BL-529 FIX2 — written only while this link is still the account's (not replaced by a newer reset, not used by a
        // parallel redemption, not cleared by a deactivation). FIX4 — and while no administrator has deactivated it: a
        // deactivation of a pending invitation keeps the link, so the link alone does not say it.
        if (!await _userRepository.TryWriteChangesAsync(user, state, user.TenantId, new UserWriteCondition(PasswordResetTokenHash: tokenHash, DeactivatedByAdministrator: false), ct))
        {
            return Response<NoContent>.Fail(InvalidTokenMessage, 400);
        }
        await _refreshTokenRepository.RevokeLiveSessionsAsync(user.Id, user.TenantId, SessionRevocationReasons.PasswordChanged, ct);

        _logger.LogInformation("Tenant user set-password redeemed. Id={Id}", user.Id);
        return Response<NoContent>.Success(204);
    }
}
