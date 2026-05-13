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

        if (administrator.Email.Equals("admin@diten.com", StringComparison.OrdinalIgnoreCase))
        {
            return Response<NoContent>.Fail("The system seed administrator cannot be suspended.", 409);
        }

        // §7.21 rule 2 — last SuperAdmin protection
        var lastAdminGuard = await _safetyGuard.EnsureNotLastActiveSuperAdminAsync(
            request.Id, AdminSafetyAction.Suspend, ct);
        if (lastAdminGuard is not null) return lastAdminGuard;

        administrator.Status = AdministratorStatus.Suspended;
        administrator.LastStatusReason = request.Request.Reason?.Trim();
        PlatformAdministratorMutationSupport.MarkUpdated(administrator, _currentUser);

        var updated = await _repository.UpdateAsync(administrator, request.Request.Version, ct);
        return updated
            ? Response<NoContent>.Success(204)
            : Response<NoContent>.Fail("The administrator was changed by another user. Reload and try again.", 409);
    }
}
