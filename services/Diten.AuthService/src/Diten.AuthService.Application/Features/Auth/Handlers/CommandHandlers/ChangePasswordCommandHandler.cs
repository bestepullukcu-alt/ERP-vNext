using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Auth.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Auth.Handlers.CommandHandlers;

public sealed class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Response<NoContent>>
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IPasswordPolicyService _passwordPolicyService;
    private readonly IAuthAuditService _authAuditService;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<ChangePasswordCommandHandler> _logger;

    public ChangePasswordCommandHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        IPasswordPolicyService passwordPolicyService,
        IAuthAuditService authAuditService,
        ITenantContext tenantContext,
        ILogger<ChangePasswordCommandHandler> logger)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher = passwordHasher;
        _passwordPolicyService = passwordPolicyService;
        _authAuditService = authAuditService;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<Response<NoContent>> Handle(ChangePasswordCommand request, CancellationToken ct)
    {
        if (!Guid.TryParse(request.UserId, out var userId))
            return Response<NoContent>.Fail("Invalid user ID.", 400);

        var user = await _userRepository.GetByIdAndTenantAsync(userId, _tenantContext.TenantId, ct);
        if (user == null) return Response<NoContent>.Fail("User not found.", 404);

        if (!_passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
            return Response<NoContent>.Fail("Current password is incorrect.", 401);

        await _passwordPolicyService.ValidateTenantPasswordAsync(_tenantContext.TenantId, user.Id, request.NewPassword, "change_password", ct);
        var newHashedPassword = _passwordHasher.Hash(request.NewPassword);
        var verifiedHash = user.PasswordHash;
        user.UpdatePassword(newHashedPassword);
        user.ClearPasswordChangeRequirement();

        // BL-529 FIX2 — written only while the password is still the one just verified: an administrator's reset that
        // landed meanwhile (the caller may hold an access token from before it) is never overwritten.
        if (!await _userRepository.TryUpdateForTenantIfPasswordHashAsync(user, _tenantContext.TenantId, verifiedHash, ct))
            return PasswordChangedMeanwhile<NoContent>();
        await _refreshTokenRepository.RevokeAllByUserAsync(userId, _tenantContext.TenantId, ct);
        await _authAuditService.WriteAsync("tenant_password_changed", user.Id, _tenantContext.TenantId, "{}", ct);

        _logger.LogInformation("User changed password. UserId={UserId}", userId);
        return Response<NoContent>.Success(204);
    }

    internal static Response<T> PasswordChangedMeanwhile<T>()
        => Response<T>.Fail("The password changed while this request ran (for example an administrator reset it). Sign in again.", 409);
}
