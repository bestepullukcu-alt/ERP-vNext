using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.PlatformAdministrators.Commands;
using Diten.Platform.Application.Security;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Handlers.CommandHandlers;

public sealed class UpdatePlatformAdministratorHandler : IRequestHandler<UpdatePlatformAdministratorCommand, Response<NoContent>>
{
    private readonly IPlatformAdministratorRepository _repository;
    private readonly IPlatformAdministratorProvisioningService _provisioningService;
    private readonly ICurrentUserContext _currentUser;
    private readonly IActorSafetyGuard _safetyGuard;

    public UpdatePlatformAdministratorHandler(
        IPlatformAdministratorRepository repository,
        IPlatformAdministratorProvisioningService provisioningService,
        ICurrentUserContext currentUser,
        IActorSafetyGuard safetyGuard)
    {
        _repository = repository;
        _provisioningService = provisioningService;
        _currentUser = currentUser;
        _safetyGuard = safetyGuard;
    }

    /// <summary>The message of a refused self change: the existing self-action sentence, which the administrators
    /// screen already shows in the user's language.</summary>
    public const string SelfChangeMessage = "You cannot perform this action on your own account.";

    public async Task<Response<NoContent>> Handle(UpdatePlatformAdministratorCommand request, CancellationToken ct)
    {
        var administrator = await _repository.GetByIdAsync(request.Id, ct);
        if (administrator is null)
        {
            return Response<NoContent>.Fail("Platform administrator not found.", 404);
        }

        var normalizedEmail = PlatformAdministratorParsing.NormalizeEmail(request.Request.Email);
        var newActorType = PlatformAdministratorParsing.ParseActorType(request.Request.ActorType);
        var newStatus = PlatformAdministratorParsing.ParseStatus(request.Request.Status);
        var newRoles = PlatformAdministratorParsing.ParseRoles(request.Request.Roles).ToList();

        // BL-521 FIX2 item 5(a) — FIX1 made the record's actor type and status take effect on the very next request, so
        // an administrator may not change its OWN actor type, status or e-mail (the address its record and its sign-in
        // are found by). Other own fields (name, user name) stay editable.
        if (await _safetyGuard.IsSelfAsync(administrator.Id, ct)
            && (newActorType != administrator.ActorType
                || newStatus != administrator.Status
                || !string.Equals(normalizedEmail, PlatformAdministratorParsing.NormalizeEmail(administrator.Email), StringComparison.Ordinal)))
        {
            return Response<NoContent>.Fail(SelfChangeMessage, 403, AdminSafetyReasonCodes.SelfChangeForbidden);
        }

        // Item 5(b) — demoting (to partner, or out of SuperAdmin) or suspending / disabling the last active platform
        // administrator holding SuperAdmin is refused 409, as suspend / delete / role removal already are.
        var staysPlatformSuperAdmin = newStatus == AdministratorStatus.Active
                                      && newActorType == ActorType.PlatformAdmin
                                      && newRoles.Contains(AdministratorRole.SuperAdmin);
        if (IsActivePlatformSuperAdmin(administrator) && !staysPlatformSuperAdmin)
        {
            var lastAdminGuard = await _safetyGuard.EnsureNotLastActiveSuperAdminAsync(administrator.Id, AdminSafetyAction.Disable, ct);
            if (lastAdminGuard is not null) return lastAdminGuard;
        }

        if (await _repository.ExistsByEmailAsync(normalizedEmail, administrator.Id, ct))
        {
            return Response<NoContent>.Fail("Administrator email already exists.", 409);
        }

        var normalizedUserName = PlatformAdministratorParsing.NormalizeUserName(request.Request.UserName);
        if (await _repository.ExistsByUserNameAsync(normalizedUserName, administrator.Id, ct))
        {
            return Response<NoContent>.Fail("Administrator username already exists.", 409);
        }

        administrator.Email = normalizedEmail;
        administrator.NormalizedEmail = normalizedEmail;
        administrator.UserName = request.Request.UserName.Trim();
        administrator.NormalizedUserName = normalizedUserName;
        administrator.DisplayName = request.Request.DisplayName.Trim();
        administrator.Status = newStatus;
        administrator.Roles = newRoles;
        PlatformAdministratorMutationSupport.ApplyScope(
            administrator,
            newActorType,
            request.Request.PartnerId,
            request.Request.AllowedTenantIds);
        PlatformAdministratorMutationSupport.MarkUpdated(administrator, _currentUser);

        var updated = await _repository.UpdateAsync(administrator, request.Request.Version, ct);
        if (!updated)
        {
            return Response<NoContent>.Fail("The administrator was changed by another user. Reload and try again.", 409);
        }

        await _provisioningService.SyncAsync(new PlatformAdministratorProvisioningSyncRequest(
            administrator.Email,
            administrator.UserName,
            administrator.DisplayName,
            administrator.ActorType.ToString(),
            administrator.Roles.Select(x => x.ToString()).ToList()), ct);

        return Response<NoContent>.Success(204);
    }

    private static bool IsActivePlatformSuperAdmin(PlatformAdministrator administrator) =>
        !administrator.IsDeleted
        && administrator.Status == AdministratorStatus.Active
        && administrator.ActorType == ActorType.PlatformAdmin
        && administrator.Roles.Contains(AdministratorRole.SuperAdmin);
}
