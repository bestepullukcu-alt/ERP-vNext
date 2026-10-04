using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Security;

/// <summary>
/// Default implementation of <see cref="IActorSafetyGuard"/>. See master-plan §7.21.
/// All rejections are normalized to HTTP 409 with English source-of-truth messages;
/// frontend renders the message verbatim or via L10n key lookup.
/// </summary>
public sealed class ActorSafetyGuard : IActorSafetyGuard
{
    private readonly ICurrentUserContext _currentUser;
    private readonly IPlatformAdministratorRepository _administrators;

    public ActorSafetyGuard(
        ICurrentUserContext currentUser,
        IPlatformAdministratorRepository administrators)
    {
        _currentUser = currentUser;
        _administrators = administrators;
    }

    public async Task<bool> IsSelfAsync(Guid targetActorId, CancellationToken ct = default) =>
        targetActorId == await ResolveCurrentPlatformAdministratorIdAsync(ct);

    public async Task<Response<NoContent>?> EnsureNotSelfAsync(
        Guid targetActorId,
        AdminSafetyAction action,
        CancellationToken ct = default)
    {
        if (!await IsSelfAsync(targetActorId, ct))
        {
            return null;
        }

        var message = action switch
        {
            AdminSafetyAction.RemoveRole          => "You cannot remove your own administrative role.",
            AdminSafetyAction.RevokePermission    => "You cannot revoke your own administrative permissions.",
            AdminSafetyAction.RemoveTenantScope   => "You cannot remove yourself from a tenant you currently operate.",
            _                                     => "You cannot perform this action on your own account."
        };

        return Response<NoContent>.Fail(message, 409);
    }

    public async Task<Response<NoContent>?> EnsureNotLastActiveSuperAdminAsync(
        Guid targetActorId,
        AdminSafetyAction action,
        CancellationToken ct = default)
    {
        // Probe: how many active SuperAdmins remain if `targetActorId` is excluded?
        var remaining = await _administrators.CountActiveSuperAdminsAsync(
            excludeId: targetActorId,
            ct);

        if (remaining > 0)
        {
            return null;
        }

        // Only block when the target IS a live, active PLATFORM administrator holding SuperAdmin — otherwise the count
        // would be unchanged and there is nothing to protect against. BL-521 FIX2 item 5: a PartnerAdmin record with the
        // SuperAdmin role is not one (the count leaves it out too).
        var target = await _administrators.GetByIdAsync(targetActorId, ct);
        if (target is null
            || target.IsDeleted
            || target.Status != Domain.Enums.AdministratorStatus.Active
            || target.ActorType != Domain.Enums.ActorType.PlatformAdmin
            || !target.Roles.Contains(Domain.Enums.AdministratorRole.SuperAdmin))
        {
            return null;
        }

        return Response<NoContent>.Fail(
            AdminSafetyMessages.LastActiveSuperAdmin,
            409,
            AdminSafetyReasonCodes.LastActiveSuperAdmin);
    }

    public async Task<BulkSafetyResult> FilterSelfFromBulkAsync(
        IReadOnlyCollection<Guid> targetActorIds,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(targetActorIds);

        var currentUserId = await ResolveCurrentPlatformAdministratorIdAsync(ct);
        var skipped = new List<Guid>(capacity: 1);
        var effective = new List<Guid>(capacity: targetActorIds.Count);

        foreach (var id in targetActorIds)
        {
            if (id == currentUserId)
            {
                skipped.Add(id);
            }
            else
            {
                effective.Add(id);
            }
        }

        return new BulkSafetyResult(effective, skipped);
    }

    private async Task<Guid> ResolveCurrentPlatformAdministratorIdAsync(CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(_currentUser.Email))
        {
            var normalizedEmail = _currentUser.Email.Trim().ToLowerInvariant();
            var administrator = await _administrators.GetByNormalizedEmailAsync(normalizedEmail, ct);
            if (administrator is not null)
            {
                return administrator.Id;
            }
        }

        return _currentUser.UserId;
    }
}
