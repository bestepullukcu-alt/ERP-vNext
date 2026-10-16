using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.PlatformAdministrators.Commands;
using Diten.Platform.Application.Security;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Handlers.CommandHandlers;

public sealed class AssignPlatformAdministratorRolesHandler : IRequestHandler<AssignPlatformAdministratorRolesCommand, Response<NoContent>>
{
    private readonly IPlatformAdministratorRepository _repository;
    private readonly ICurrentUserContext _currentUser;
    private readonly IActorSafetyGuard _safetyGuard;

    public AssignPlatformAdministratorRolesHandler(
        IPlatformAdministratorRepository repository,
        ICurrentUserContext currentUser,
        IActorSafetyGuard safetyGuard)
    {
        _repository = repository;
        _currentUser = currentUser;
        _safetyGuard = safetyGuard;
    }

    public async Task<Response<NoContent>> Handle(AssignPlatformAdministratorRolesCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        var administrator = await _repository.GetByIdAsync(request.Id, ct);
        if (administrator is null)
        {
            return Response<NoContent>.Fail("Platform administrator not found.", 404);
        }

        // BL-521 FIX4 item 3 — the guarded / unguarded decision below is made on the record as loaded, so it is written at
        // the version that was read; a client version other than that one is refused before anything is decided.
        if (administrator.Version != request.Request.Version)
        {
            return Response<NoContent>.Fail(AdminSafetyMessages.ChangedByAnotherUser, 409);
        }

        var loadedVersion = administrator.Version;
        var newRoles = PlatformAdministratorParsing.ParseRoles(request.Request.Roles).ToList();

        // BL-521 FIX3 item 1 — an administrator does not change its OWN roles, in either direction. Before, only removing
        // its own SuperAdmin was refused here; adding it was allowed, and the update path let it through as well. (The
        // permission filter lets every confirmed platform administrator past every key — the owner keeps that rule, BL-521
        // — so this record rule is the one between a ReadOnly administrator and SuperAdmin.)
        if (!newRoles.ToHashSet().SetEquals(administrator.Roles) && await _safetyGuard.IsSelfAsync(request.Id, ct))
        {
            return Response<NoContent>.Fail(UpdatePlatformAdministratorHandler.SelfChangeMessage, 403, AdminSafetyReasonCodes.SelfChangeForbidden);
        }

        var willRemoveSuperAdmin =
            administrator.Roles.Contains(AdministratorRole.SuperAdmin)
            && !newRoles.Contains(AdministratorRole.SuperAdmin);
        var leavesSuperAdminSet = willRemoveSuperAdmin && PlatformAdministratorMutationSupport.IsActivePlatformSuperAdmin(administrator);

        // §7.21 rule 2 — last SuperAdmin protection on role-remove path
        if (willRemoveSuperAdmin)
        {
            var lastAdminGuard = await _safetyGuard.EnsureNotLastActiveSuperAdminAsync(
                request.Id, AdminSafetyAction.RemoveRole, ct);
            if (lastAdminGuard is not null) return lastAdminGuard;
        }

        administrator.Roles = newRoles;
        PlatformAdministratorMutationSupport.MarkUpdated(administrator, _currentUser);

        // FIX3 item 3 — taking an active platform super administrator out of the set is written under the guard.
        var outcome = leavesSuperAdminSet
            ? await _repository.UpdateGuardedAsync(administrator, loadedVersion, ct)
            : await _repository.UpdateAsync(administrator, loadedVersion, ct)
                ? SuperAdminGuardedWriteResult.Written
                : SuperAdminGuardedWriteResult.VersionConflict;
        return outcome == SuperAdminGuardedWriteResult.Written
            ? Response<NoContent>.Success(204)
            : PlatformAdministratorMutationSupport.RefusedWrite(outcome, AdminSafetyMessages.ChangedByAnotherUser);
    }
}
