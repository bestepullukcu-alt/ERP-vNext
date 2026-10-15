using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.PlatformAdministrators.Commands;
using Diten.Platform.Application.Security;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.PlatformAdministrators.Handlers.CommandHandlers;

public sealed class BulkDeletePlatformAdministratorsHandler
    : IRequestHandler<BulkDeletePlatformAdministratorsCommand, Response<NoContent>>
{
    private const string BulkConflictMessage = "One or more administrators could not be deleted. Reload and try again.";

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

        // BL-521 FIX5 item 7 — the same administrator listed twice is a malformed request: 400, nothing is deleted. (It used
        // to delete once and answer 409 for the second occurrence, which no longer existed.)
        if (request.Items.GroupBy(item => item.Id).Any(group => group.Count() > 1))
        {
            return Response<NoContent>.Fail(AdminSafetyMessages.BulkDuplicateIds, 400, AdminSafetyReasonCodes.BulkDuplicateIds);
        }

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

        // BL-521 FIX3 item 2(c) — every target is read BEFORE anything is deleted: the system seed administrator in the list
        // refuses the whole request, as the single delete refuses it (before, the bulk path deleted it).
        var targets = new List<(PlatformAdministratorBulkDeleteItemRequest Item, PlatformAdministrator Administrator)>(effectiveItems.Count);
        foreach (var item in effectiveItems)
        {
            var administrator = await _repository.GetByIdAsync(item.Id, ct);
            if (administrator is null)
            {
                return Response<NoContent>.Fail("One or more administrators could not be found.", 404);
            }

            // BL-521 FIX4 item 3 — every target is decided on the record as loaded and written at the version that was
            // read: a version other than the client's refuses the whole request before anything is deleted.
            if (administrator.Version != item.Version)
            {
                return Response<NoContent>.Fail(BulkConflictMessage, 409);
            }

            if (PlatformAdministratorMutationSupport.IsSystemSeed(administrator))
            {
                return Response<NoContent>.Fail(AdminSafetyMessages.SeedCannotBeDeleted, 409, AdminSafetyReasonCodes.SystemSeedProtected);
            }

            targets.Add((item, administrator));
        }

        // BL-521 FIX5 item 7 — THE LAST-SUPER-ADMINISTRATOR RULE IS DECIDED FOR THE WHOLE SET BEFORE ANYTHING IS DELETED.
        // Decided one target at a time as they were deleted, a set that would take every active platform super
        // administrator out of the system deleted the first ones and only then refused the last — an error answer over a
        // half-done delete. A refusal anywhere in the set now deletes nothing.
        var setGuard = await _safetyGuard.EnsureNotLastActiveSuperAdminsAsync(
            targets.Select(target => target.Administrator).ToList(), AdminSafetyAction.Delete, ct);
        if (setGuard is not null) return setGuard;

        var deleted = new List<Guid>(targets.Count);
        foreach (var (item, administrator) in targets)
        {
            // §7.21 rule 2 — last SuperAdmin protection per id in the bulk loop (what the set check cannot see: a change
            // another request made after it).
            var lastAdminGuard = await _safetyGuard.EnsureNotLastActiveSuperAdminAsync(
                item.Id, AdminSafetyAction.Delete, ct);
            if (lastAdminGuard is not null) return Honest(lastAdminGuard, deleted, item.Id);

            // FIX3 item 3 — deleting an active platform super administrator is written under the guard.
            var outcome = PlatformAdministratorMutationSupport.IsActivePlatformSuperAdmin(administrator)
                ? await _repository.SoftDeleteGuardedAsync(item.Id, administrator.Version, _currentUser.ActorName, ct)
                : await _repository.SoftDeleteAsync(item.Id, administrator.Version, _currentUser.ActorName, ct)
                    ? SuperAdminGuardedWriteResult.Written
                    : SuperAdminGuardedWriteResult.VersionConflict;
            if (outcome != SuperAdminGuardedWriteResult.Written)
            {
                return Honest(PlatformAdministratorMutationSupport.RefusedWrite(outcome, BulkConflictMessage), deleted, item.Id);
            }

            deleted.Add(item.Id);
        }

        return Response<NoContent>.Success(204);
    }

    /// <summary>
    /// A refusal that comes while deleting (another request changed a target after the whole set was decided) must say what
    /// already happened. The batch is NOT one transaction — deleting administrators other than active super administrators
    /// needs none, and a standalone development database has none — so when something was already deleted the answer is
    /// <see cref="AdminSafetyReasonCodes.BulkDeletePartial"/>: the refusal's own status, the sentence the screen shows, and
    /// the ids that were deleted and the one that was refused (the rest were not processed). When nothing was deleted yet it
    /// is the plain refusal.
    /// </summary>
    private static Response<NoContent> Honest(Response<NoContent> refusal, IReadOnlyList<Guid> deleted, Guid refusedId) =>
        deleted.Count == 0
            ? refusal
            : Response<NoContent>.Fail(
                [AdminSafetyMessages.BulkDeletePartial, "deleted: " + string.Join(", ", deleted), "refused: " + refusedId],
                refusal.StatusCode,
                AdminSafetyReasonCodes.BulkDeletePartial);
}
