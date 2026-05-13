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

        var newRoles = PlatformAdministratorParsing.ParseRoles(request.Request.Roles).ToList();
        var willRemoveSuperAdmin =
            administrator.Roles.Contains(AdministratorRole.SuperAdmin)
            && !newRoles.Contains(AdministratorRole.SuperAdmin);

        // §7.21 rule 3 — role self-downgrade. Only fires when the actor is the target
        // AND the change actually strips their SuperAdmin role.
        if (willRemoveSuperAdmin)
        {
            var selfGuard = await _safetyGuard.EnsureNotSelfAsync(request.Id, AdminSafetyAction.RemoveRole, ct);
            if (selfGuard is not null) return selfGuard;
        }

        // §7.21 rule 2 — last SuperAdmin protection on role-remove path
        if (willRemoveSuperAdmin)
        {
            var lastAdminGuard = await _safetyGuard.EnsureNotLastActiveSuperAdminAsync(
                request.Id, AdminSafetyAction.RemoveRole, ct);
            if (lastAdminGuard is not null) return lastAdminGuard;
        }

        administrator.Roles = newRoles;
        PlatformAdministratorMutationSupport.MarkUpdated(administrator, _currentUser);

        var updated = await _repository.UpdateAsync(administrator, request.Request.Version, ct);
        return updated
            ? Response<NoContent>.Success(204)
            : Response<NoContent>.Fail("The administrator was changed by another user. Reload and try again.", 409);
    }
}
