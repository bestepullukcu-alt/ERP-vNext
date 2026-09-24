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
    private readonly ILogger<UpdateUserCommandHandler> _logger;

    public UpdateUserCommandHandler(
        IUserRepository userRepository,
        IUserRoleRepository userRoleRepository,
        ITenantContext tenantContext,
        AccountKindWriter kindWriter,
        ILogger<UpdateUserCommandHandler> logger)
    {
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _tenantContext = tenantContext;
        _kindWriter = kindWriter;
        _logger = logger;
    }

    public async Task<Response<UserDto>> Handle(UpdateUserCommand request, CancellationToken ct)
    {
        var user = await _userRepository.GetByIdAndTenantAsync(request.Id, _tenantContext.TenantId, ct);
        if (user == null) return Response<UserDto>.Fail("User not found.", 404);

        // WP-AUTH-USER-KIND-UPDATE-01 — the kind rides on the edit form's "Update" under create's rule: classifying is
        // a SEPARATE right from editing. A supplied kind that CHANGES the account, from a caller without
        // auth.users.account-kind.manage, refuses the whole update (403 PERM_DENIED, nothing written) — never silently
        // dropped, which would hide a permission gap behind a success. Re-sending the current kind is no change and
        // needs no right: the form always posts the field.
        var hasKind = !string.IsNullOrWhiteSpace(request.AccountKind);
        var newKind = user.AccountKind;
        if (hasKind && !AccountKindWriter.TryParse(request.AccountKind, out newKind))
        {
            return Response<UserDto>.Fail("AccountKind must be one of: Unknown, Human, Service.", 400);
        }

        if (hasKind && newKind != user.AccountKind && !request.CallerCanManageAccountKind)
        {
            return Response<UserDto>.Fail(
                "Setting the account kind requires the auth.users.account-kind.manage permission.",
                [new ResponseError(CreateUserCommandHandler.PermissionDeniedCode)],
                403);
        }

        // WP-AUTH-INVITED-LIFECYCLE-01 — the edit form is the second administrator door to activation; it answers
        // exactly like the kebab's enable (UserLifecycle), before anything is written.
        if (UserLifecycle.RefusesActivation(user, request.IsActive))
        {
            return UserLifecycle.InvitationPendingRefusal<UserDto>();
        }

        user.UpdateProfile(request.FirstName, request.LastName);
        if (request.IsActive) user.Activate(); else user.Deactivate();
        var kindChange = hasKind ? _kindWriter.Apply(user, newKind) : null;

        // One tenant-scoped replace for the profile AND the kind; the audit row follows the persisted change.
        await _userRepository.UpdateForTenantAsync(user, _tenantContext.TenantId, ct);
        if (kindChange is not null)
        {
            await _kindWriter.RecordAsync(user, kindChange, _tenantContext.TenantId, request.CorrelationId, ct);
            _logger.LogInformation(
                "Account kind changed via update. UserId={UserId} TenantId={TenantId} {PreviousKind}->{NewKind} CorrelationId={CorrelationId}",
                user.Id, _tenantContext.TenantId, kindChange.Previous, kindChange.Next, request.CorrelationId);
        }

        var roles = await _userRoleRepository.GetRolesByUserAsync(user.Id, _tenantContext.TenantId, ct);

        return Response<UserDto>.Success(new UserDto(user.Id, user.Email, user.FirstName, user.LastName, user.IsActive, roles, user.TenantId,
            user.LastLoginAt, user.FailedLoginAttempts, user.MustChangePassword, "TenantPolicy",
            AccountKind: user.AccountKind.ToString(), Status: UserLifecycle.StatusOf(user)));
    }
}
