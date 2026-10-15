using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.PlatformAdministrators.Commands;
using Diten.Platform.Application.Security;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Handlers.CommandHandlers;

public sealed class SuspendPlatformAdministratorHandler : IRequestHandler<SuspendPlatformAdministratorCommand, Response<NoContent>>
{
    private readonly IPlatformAdministratorRepository _repository;
    private readonly ICurrentUserContext _currentUser;
    private readonly IActorSafetyGuard _safetyGuard;

    public SuspendPlatformAdministratorHandler(
        IPlatformAdministratorRepository repository,
        ICurrentUserContext currentUser,
        IActorSafetyGuard safetyGuard)
    {
        _repository = repository;
        _currentUser = currentUser;
        _safetyGuard = safetyGuard;
    }

    public async Task<Response<NoContent>> Handle(SuspendPlatformAdministratorCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        // §7.21 rule 1 — self-action guard
        var selfGuard = await _safetyGuard.EnsureNotSelfAsync(request.Id, AdminSafetyAction.Suspend, ct);
        if (selfGuard is not null) return selfGuard;

        var administrator = await _repository.GetByIdAsync(request.Id, ct);
        if (administrator is null)
        {
            return Response<NoContent>.Fail("Platform administrator not found.", 404);
        }

        // BL-521 FIX4 item 3 — every rule below decides on the record as loaded, so the client must have edited THAT
        // version; the write is then conditional on the version that was read, never on the one the client names.
        if (administrator.Version != request.Request.Version)
        {
            return Response<NoContent>.Fail(AdminSafetyMessages.ChangedByAnotherUser, 409);
        }

        var loadedVersion = administrator.Version;

        if (PlatformAdministratorMutationSupport.IsSystemSeed(administrator))
        {
            return Response<NoContent>.Fail(AdminSafetyMessages.SeedCannotBeSuspended, 409, AdminSafetyReasonCodes.SystemSeedProtected);
        }

        // §7.21 rule 2 — last SuperAdmin protection
        var lastAdminGuard = await _safetyGuard.EnsureNotLastActiveSuperAdminAsync(
            request.Id, AdminSafetyAction.Suspend, ct);
        if (lastAdminGuard is not null) return lastAdminGuard;

        var leavesSuperAdminSet = PlatformAdministratorMutationSupport.IsActivePlatformSuperAdmin(administrator);
        administrator.Status = AdministratorStatus.Suspended;
        administrator.LastStatusReason = request.Request.Reason?.Trim();
        PlatformAdministratorMutationSupport.MarkUpdated(administrator, _currentUser);

        // BL-521 FIX3 item 3 — suspending an active platform super administrator is written under the guard.
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
