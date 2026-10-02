using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Features.Users.Commands;
using Diten.AuthService.Application.Features.Users.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;

// Admin enable/disable, tenant-scoped. Disabling revokes refresh tokens so live sessions terminate.
public sealed class SetUserActiveStatusCommandHandler : IRequestHandler<SetUserActiveStatusCommand, Response<NoContent>>
{
    /// <summary>
    /// YOU CANNOT SWITCH YOURSELF OFF — owner finding, control round 2026-09-24. Deactivation revokes every refresh
    /// token of the account, so an administrator who deactivated their own account was signed out on the spot with no
    /// way back in from that seat. Same shape as <see cref="DeleteUserCommandHandler.SelfDeleteCode"/>: refused with a
    /// code the screen can translate, before anything is written. SAP (SU01) and Oracle Fusion refuse the same act.
    /// </summary>
    public const string SelfDeactivateCode = UserErrorCodes.DeactivateSelf;

    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IUserAuditRecorder _audit;
    private readonly IUserQuotaClient _quota;
    private readonly ILogger<SetUserActiveStatusCommandHandler> _logger;

    public SetUserActiveStatusCommandHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ITenantContext tenantContext,
        ICurrentUserAccessor currentUser,
        IUserAuditRecorder audit,
        IUserQuotaClient quota,
        ILogger<SetUserActiveStatusCommandHandler> logger)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _tenantContext = tenantContext;
        _currentUser = currentUser;
        _audit = audit;
        _quota = quota;
        _logger = logger;
    }

    public async Task<Response<NoContent>> Handle(SetUserActiveStatusCommand request, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAndTenantAsync(request.Id, _tenantContext.TenantId, ct);
        if (user is null) return UserErrorCodes.NotFoundRefusal<NoContent>();

        if (!request.IsActive && _currentUser.UserId == request.Id)
        {
            return Response<NoContent>.Fail(
                "You cannot deactivate the account you are signed in with.",
                [new ResponseError(SelfDeactivateCode)],
                409);
        }

        // WP-AUTH-INVITED-LIFECYCLE-01 — an invited account activates when its owner redeems the set-password link.
        // Activated by an administrator it would read "Active" and still be unable to sign in (placeholder hash).
        if (UserLifecycle.RefusesActivation(user, request.IsActive))
        {
            return UserLifecycle.InvitationPendingRefusal<NoContent>();
        }

        var wasActive = user.IsActive;

        // BL-459 F1 — an Inactive account holds no seat (users.max counts Active + Invited), so switching one back on
        // takes a seat and asks first; otherwise deactivate → add someone → reactivate would walk past the plan's limit.
        if (request.IsActive && !wasActive)
        {
            var seat = await _quota.TryConsumeUserSeatAsync(_tenantContext.TenantId, $"user-activate:{user.Id:D}", ct);
            if (seat.Outcome == UserQuotaOutcome.LimitExceeded)
            {
                return UserLifecycle.QuotaExceededRefusal<NoContent>(seat.Limit, seat.Current);
            }
        }

        if (request.IsActive)
        {
            user.Activate();
        }
        else
        {
            user.Deactivate();
        }

        await _userRepository.UpdateAsync(user, ct);

        // Disabling must terminate active sessions immediately.
        if (!request.IsActive)
        {
            await _refreshTokenRepository.RevokeAllByUserAsync(user.Id, _tenantContext.TenantId, ct);
        }

        _logger.LogInformation("User active status changed. Id={Id} IsActive={IsActive}", user.Id, request.IsActive);

        // BL-456 — only a real transition is an event; re-sending the current state writes no audit row.
        if (wasActive != request.IsActive)
        {
            await _audit.RecordAsync(
                request.IsActive ? UserAuditEvents.Activated : UserAuditEvents.Deactivated,
                _tenantContext.TenantId, user.Id,
                new Dictionary<string, object?> { ["previousIsActive"] = wasActive, ["isActive"] = request.IsActive }, ct);
        }
        return Response<NoContent>.Success(204);
    }
}
