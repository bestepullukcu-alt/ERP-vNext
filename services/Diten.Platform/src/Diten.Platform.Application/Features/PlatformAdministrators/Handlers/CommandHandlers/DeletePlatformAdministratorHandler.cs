using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.PlatformAdministrators.Commands;
using Diten.Platform.Application.Security;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Handlers.CommandHandlers;

public sealed class DeletePlatformAdministratorHandler : IRequestHandler<DeletePlatformAdministratorCommand, Response<NoContent>>
{
    private readonly IPlatformAdministratorRepository _repository;
    private readonly ICurrentUserContext _currentUser;
    private readonly IActorSafetyGuard _safetyGuard;

    public DeletePlatformAdministratorHandler(
        IPlatformAdministratorRepository repository,
        ICurrentUserContext currentUser,
        IActorSafetyGuard safetyGuard)
    {
        _repository = repository;
        _currentUser = currentUser;
        _safetyGuard = safetyGuard;
    }

    public async Task<Response<NoContent>> Handle(DeletePlatformAdministratorCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        // §7.21 rule 1 — self-action guard
        var selfGuard = await _safetyGuard.EnsureNotSelfAsync(request.Id, AdminSafetyAction.Delete, ct);
        if (selfGuard is not null) return selfGuard;

        var administrator = await _repository.GetByIdAsync(request.Id, ct);
        if (administrator is null)
        {
            return Response<NoContent>.Fail("Platform administrator not found.", 404);
        }

        // BL-521 FIX4 item 3 — decided on the record as loaded, written at the version that was read.
        if (administrator.Version != request.Request.Version)
        {
            return Response<NoContent>.Fail(AdminSafetyMessages.ChangedByAnotherUser, 409);
        }

        var loadedVersion = administrator.Version;

        if (PlatformAdministratorMutationSupport.IsSystemSeed(administrator))
        {
            return Response<NoContent>.Fail(AdminSafetyMessages.SeedCannotBeDeleted, 409, AdminSafetyReasonCodes.SystemSeedProtected);
        }

        // §7.21 rule 2 — last SuperAdmin protection
        var lastAdminGuard = await _safetyGuard.EnsureNotLastActiveSuperAdminAsync(
            request.Id, AdminSafetyAction.Delete, ct);
        if (lastAdminGuard is not null) return lastAdminGuard;

        // BL-521 FIX3 item 3 — deleting an active platform super administrator is written under the guard.
        var outcome = PlatformAdministratorMutationSupport.IsActivePlatformSuperAdmin(administrator)
            ? await _repository.SoftDeleteGuardedAsync(request.Id, loadedVersion, _currentUser.ActorName, ct)
            : await _repository.SoftDeleteAsync(request.Id, loadedVersion, _currentUser.ActorName, ct)
                ? SuperAdminGuardedWriteResult.Written
                : SuperAdminGuardedWriteResult.VersionConflict;
        return outcome == SuperAdminGuardedWriteResult.Written
            ? Response<NoContent>.Success(204)
            : PlatformAdministratorMutationSupport.RefusedWrite(outcome, AdminSafetyMessages.ChangedByAnotherUser);
    }
}
