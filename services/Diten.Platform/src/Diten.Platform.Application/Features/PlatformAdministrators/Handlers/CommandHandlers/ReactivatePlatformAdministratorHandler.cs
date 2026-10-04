using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.PlatformAdministrators.Commands;
using Diten.Platform.Application.Security;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Handlers.CommandHandlers;

public sealed class ReactivatePlatformAdministratorHandler : IRequestHandler<ReactivatePlatformAdministratorCommand, Response<NoContent>>
{
    private readonly IPlatformAdministratorRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public ReactivatePlatformAdministratorHandler(IPlatformAdministratorRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Response<NoContent>> Handle(ReactivatePlatformAdministratorCommand request, CancellationToken ct)
    {
        var administrator = await _repository.GetByIdAsync(request.Id, ct);
        if (administrator is null)
        {
            return Response<NoContent>.Fail("Platform administrator not found.", 404);
        }

        // BL-521 FIX4 item 3 — the record is written back whole, so it must be the version the client saw, and the write is
        // conditional on the version that was read: a stale snapshot is never written over a newer record.
        if (administrator.Version != request.Request.Version)
        {
            return Response<NoContent>.Fail(AdminSafetyMessages.ChangedByAnotherUser, 409);
        }

        var loadedVersion = administrator.Version;
        administrator.Status = AdministratorStatus.Active;
        administrator.LastStatusReason = string.IsNullOrWhiteSpace(request.Request.Reason) ? null : request.Request.Reason.Trim();
        PlatformAdministratorMutationSupport.MarkUpdated(administrator, _currentUser);

        var updated = await _repository.UpdateAsync(administrator, loadedVersion, ct);
        return updated
            ? Response<NoContent>.Success(204)
            : Response<NoContent>.Fail(AdminSafetyMessages.ChangedByAnotherUser, 409);
    }
}
