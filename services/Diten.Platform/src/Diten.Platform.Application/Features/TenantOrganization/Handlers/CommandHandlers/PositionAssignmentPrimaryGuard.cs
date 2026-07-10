using Diten.Platform.Domain.Entities.Organization;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.TenantOrganization.Handlers.CommandHandlers;

// MOD-0288 v1 — at most ONE Primary assignment per position at any point in time. This REPLACES the old blanket
// interval-overlap rule: Secondary / Acting / Delegated assignments may overlap each other and a Primary; only
// two Primary intervals on the same position are disallowed. Cancelled assignments never conflict.
internal static class PositionAssignmentPrimaryGuard
{
    public static async Task<bool> HasPrimaryOverlapAsync(
        IPositionAssignmentRepository assignments,
        Guid positionId,
        DateTimeOffset effectiveFrom,
        DateTimeOffset? effectiveTo,
        Guid? excludeId,
        CancellationToken ct)
    {
        var all = await assignments.GetAllAsync(ct);
        return all.Any(a =>
            (excludeId is null || a.Id != excludeId.Value)
            && a.PositionId == positionId
            && a.AssignmentType == AssignmentType.Primary
            && !a.IsCancelled
            && a.DeletedAt is null
            && Overlaps(effectiveFrom, effectiveTo, a.EffectiveFrom, a.EffectiveTo));
    }

    // Half-open intervals [from, to); a null end means open-ended (+infinity).
    private static bool Overlaps(DateTimeOffset aFrom, DateTimeOffset? aTo, DateTimeOffset bFrom, DateTimeOffset? bTo)
        => aFrom < (bTo ?? DateTimeOffset.MaxValue) && bFrom < (aTo ?? DateTimeOffset.MaxValue);
}
