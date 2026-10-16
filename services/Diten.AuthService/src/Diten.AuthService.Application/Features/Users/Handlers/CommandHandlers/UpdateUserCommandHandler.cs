using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.DTOs;
using Diten.AuthService.Application.Features.Users.Commands;
using Diten.AuthService.Application.Features.Users.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Features.Users.Handlers.CommandHandlers;

public sealed class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, Response<UserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly ITenantContext _tenantContext;
    private readonly AccountKindWriter _kindWriter;
    private readonly IUserAuditRecorder _audit;
    private readonly IUserQuotaClient _quota;
    private readonly ICurrentUserAccessor _currentUser;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ILogger<UpdateUserCommandHandler> _logger;

    public UpdateUserCommandHandler(
        IUserRepository userRepository,
        IUserRoleRepository userRoleRepository,
        ITenantContext tenantContext,
        AccountKindWriter kindWriter,
        IUserAuditRecorder audit,
        IUserQuotaClient quota,
        ICurrentUserAccessor currentUser,
        IRefreshTokenRepository refreshTokens,
        ILogger<UpdateUserCommandHandler> logger)
    {
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _tenantContext = tenantContext;
        _kindWriter = kindWriter;
        _audit = audit;
        _quota = quota;
        _currentUser = currentUser;
        _refreshTokens = refreshTokens;
        _logger = logger;
    }

    public async Task<Response<UserDto>> Handle(UpdateUserCommand request, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAndTenantAsync(request.Id, _tenantContext.TenantId, ct);
        if (user == null) return UserErrorCodes.NotFoundRefusal<UserDto>();

        // An update that does not say isActive leaves the account as it is — never "missing means switch off".
        var isActive = request.IsActive ?? user.IsActive;

        // WP-AUTH-USER-KIND-UPDATE-01 — the kind rides on the edit form's "Update" under create's rule: classifying is
        // a SEPARATE right from editing. A supplied kind that CHANGES the account, from a caller without
        // auth.users.account-kind.manage, refuses the whole update (403 PERM_DENIED, nothing written) — never silently
        // dropped, which would hide a permission gap behind a success. Re-sending the current kind is no change and
        // needs no right: the form always posts the field.
        var hasKind = !string.IsNullOrWhiteSpace(request.AccountKind);
        var newKind = user.AccountKind;
        if (hasKind && !AccountKindWriter.TryParse(request.AccountKind, out newKind))
        {
            return Response<UserDto>.Fail("AccountKind must be one of: Unknown, Human, Service.", [new ResponseError(UserErrorCodes.AccountKindInvalid)], 400);
        }

        if (hasKind && newKind != user.AccountKind && !request.CallerCanManageAccountKind)
        {
            return Response<UserDto>.Fail(
                "Setting the account kind requires the auth.users.account-kind.manage permission.",
                [new ResponseError(CreateUserCommandHandler.PermissionDeniedCode)],
                403);
        }

        // Finding 33 (owner, 2026-09-24), the SECOND door: the edit form's Active switch. The kebab's disable refuses to
        // switch off the signed-in account (SetUserActiveStatusCommandHandler); the form must answer exactly the same,
        // with the same code, before anything is written — otherwise the rule is a screen, not a rule.
        if (!isActive && user.IsActive && _currentUser.UserId == user.Id)
        {
            return Response<UserDto>.Fail(
                "You cannot deactivate the account you are signed in with.",
                [new ResponseError(SetUserActiveStatusCommandHandler.SelfDeactivateCode)],
                409);
        }

        // WP-AUTH-INVITED-LIFECYCLE-01 — the edit form is the second administrator door to activation; it answers
        // exactly like the kebab's enable (UserLifecycle), before anything is written.
        if (UserLifecycle.RefusesActivation(user, isActive))
        {
            return UserLifecycle.InvitationPendingRefusal<UserDto>();
        }

        // BL-459 F1 — the edit form is the second door to switching an Inactive account back on; same seat question as
        // the kebab's enable, before anything is written.
        if (isActive && !user.IsActive)
        {
            var seat = await _quota.TryConsumeUserSeatAsync(_tenantContext.TenantId, $"user-activate:{user.Id:D}", ct);
            if (seat.Outcome == UserQuotaOutcome.LimitExceeded)
            {
                return UserLifecycle.QuotaExceededRefusal<UserDto>(seat.Limit, seat.Current);
            }
        }

        // BL-456 — what the form changed, measured before the entity is touched. The kind has its own event below.
        var changedFields = new List<string>();
        if (!string.Equals(user.FirstName, request.FirstName, StringComparison.Ordinal)) changedFields.Add("firstName");
        if (!string.Equals(user.LastName, request.LastName, StringComparison.Ordinal)) changedFields.Add("lastName");
        var wasActive = user.IsActive;
        if (wasActive != isActive) changedFields.Add("isActive");

        var state = _userRepository.CaptureState(user);
        user.UpdateProfile(request.FirstName, request.LastName);
        // BL-529 FIX3 — the administrator's mark moves only on a REAL switch. A pending invitation is inactive by nature:
        // saving its name must not mark it deactivated (that locked it for good — FIX2's regression).
        if (wasActive && !isActive) user.DeactivateByAdministrator();
        else if (!wasActive && isActive) user.ActivateByAdministrator();
        var kindChange = hasKind ? _kindWriter.Apply(user, newKind) : null;

        // BL-529 FIX3 — only the fields the form changed (profile, switch, kind), never a whole-document write from the copy
        // read above (which put back a password an administrator's reset replaced meanwhile).
        await _userRepository.TryWriteChangesAsync(user, state, _tenantContext.TenantId, UserWriteCondition.None, ct);
        if (wasActive && !isActive)
        {
            // Deactivation through the form ends the sessions too — the kebab's disable already did; a deactivated user
            // who could keep working until the token expired was a hole the form left open.
            await _refreshTokens.RevokeAllByUserAsync(user.Id, _tenantContext.TenantId, ct);
        }
        if (kindChange is not null)
        {
            await _kindWriter.RecordAsync(user, kindChange, _tenantContext.TenantId, request.CorrelationId, ct);
            _logger.LogInformation(
                "Account kind changed via update. UserId={UserId} TenantId={TenantId} {PreviousKind}->{NewKind} CorrelationId={CorrelationId}",
                user.Id, _tenantContext.TenantId, kindChange.Previous, kindChange.Next, request.CorrelationId);
        }

        // BL-456 — field NAMES, not values (the recorder's no-PII rule); the active flag is not personal data and
        // its before/after is the fact an auditor asks for. A save that changed nothing writes no row.
        if (changedFields.Count > 0)
        {
            await _audit.RecordAsync(UserAuditEvents.Updated, _tenantContext.TenantId, user.Id,
                new Dictionary<string, object?>
                {
                    ["changedFields"] = changedFields,
                    ["previousIsActive"] = wasActive,
                    ["isActive"] = user.IsActive,
                    ["correlationId"] = request.CorrelationId
                }, ct);
        }

        var roles = await _userRoleRepository.GetRolesByUserAsync(user.Id, _tenantContext.TenantId, ct);

        return Response<UserDto>.Success(new UserDto(user.Id, user.Email, user.FirstName, user.LastName, user.IsActive, roles, user.TenantId,
            user.LastLoginAt, user.FailedLoginAttempts, user.MustChangePassword, "TenantPolicy",
            AccountKind: user.AccountKind.ToString(), Status: UserLifecycle.StatusOf(user)));
    }
}
