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
            !string.Equals(user.PasswordResetTokenHash, tokenHash, StringComparison.Ordinal) ||
            !string.Equals(user.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            return Response<NoContent>.Fail(InvalidTokenMessage, 400);
        }

        await _passwordPolicyService.ValidateTenantPasswordAsync(user.TenantId, user.Id, request.NewPassword, "tenant_set_password", ct);

        user.UpdatePassword(_passwordHasher.Hash(request.NewPassword));
        user.ClearPasswordChangeRequirement(); // also clears the one-time token (single use)
        user.Activate();
        user.ConfirmEmail();

        await _userRepository.UpdateForTenantAsync(user, user.TenantId, ct);
        await _refreshTokenRepository.RevokeAllByUserAsync(user.Id, user.TenantId, ct);

        _logger.LogInformation("Tenant user set-password redeemed. Id={Id}", user.Id);
        return Response<NoContent>.Success(204);
    }
}
