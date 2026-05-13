using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.PlatformAdministrators.Commands;
using Diten.Platform.Application.Security;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Handlers.CommandHandlers;

public sealed class BulkDeletePlatformAdministratorsHandler
    : IRequestHandler<BulkDeletePlatformAdministratorsCommand, Response<NoContent>>
{
    private readonly IPlatformAdministratorRepository _repository;
    private readonly ICurrentUserContext _currentUser;
    private readonly IActorSafetyGuard _safetyGuard;

    public BulkDeletePlatformAdministratorsHandler(
        IPlatformAdministratorRepository repository,
        ICurrentUserContext currentUser,
        IActorSafetyGuard safetyGuard)
    {
        _repository = repository;
        _currentUser = currentUser;
        _safetyGuard = safetyGuard;
    }

    public async Task<Response<NoContent>> Handle(BulkDeletePlatformAdministratorsCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);

        // §7.21 rule 4 — silently strip current user from the bulk list (no error).
        var ids = request.Items.Select(item => item.Id).ToList();
        var safety = await _safetyGuard.FilterSelfFromBulkAsync(ids, ct);

        var effectiveItems = request.Items
            .Where(item => !safety.SkippedSelfIds.Contains(item.Id))
            .ToList();

        // All targets were the current user → nothing to do; respond success (200/204).
        if (effectiveItems.Count == 0)
        {
            return Response<NoContent>.Success(204);
        }

        foreach (var item in effectiveItems)
        {
            var administrator = await _repository.GetByIdAsync(item.Id, ct);
            if (administrator is null)
            {
                return Response<NoContent>.Fail("One or more administrators could not be found.", 404);
            }

            // §7.21 rule 2 — last SuperAdmin protection per id in the bulk loop.
            var lastAdminGuard = await _safetyGuard.EnsureNotLastActiveSuperAdminAsync(
                item.Id, AdminSafetyAction.Delete, ct);
            if (lastAdminGuard is not null) return lastAdminGuard;

            var deleted = await _repository.SoftDeleteAsync(item.Id, item.Version, _currentUser.ActorName, ct);
            if (!deleted)
            {
                return Response<NoContent>.Fail("One or more administrators could not be deleted. Reload and try again.", 409);
            }
        }

        return Response<NoContent>.Success(204);
    }
}
