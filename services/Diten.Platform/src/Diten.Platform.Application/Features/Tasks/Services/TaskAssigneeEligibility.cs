using Diten.Platform.Domain.Entities.Organization;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// Who work may be assigned to — ONE rule, shared by the people picker and by the reassign endpoint.
///
/// <para>The picker answers "who can I choose" and reassign answers "may I choose this one". Written twice they
/// drift, and the drift is invisible in the direction that matters: the picker narrows, the endpoint stays wide,
/// and work lands on somebody the product no longer considers assignable. Both call this.</para>
///
/// <para>A person is assignable when they currently HOLD an active position in a live unit: an assignment that
/// has begun and not ended, on a position that is Active and not archived, in an organization unit that is not
/// archived. The unit matters because a position without a resolvable unit cannot be told apart from a namesake
/// elsewhere — the picker refuses to show such a row, so reassign must refuse to accept one.</para>
/// </summary>
public static class TaskAssigneeEligibility
{
    /// <summary>The user ids that may hold work, given the current org data.</summary>
    public static HashSet<Guid> ResolveAssignableUserIds(
        IEnumerable<PositionAssignment> assignments,
        IEnumerable<Position> positions,
        IEnumerable<OrganizationUnit> organizationUnits,
        DateTimeOffset asOf)
    {
        var positionById = positions.ToDictionary(position => position.Id);
        var unitById = organizationUnits.ToDictionary(unit => unit.Id);

        return assignments
            .Where(assignment => IsActive(assignment, asOf))
            .Where(assignment =>
                positionById.TryGetValue(assignment.PositionId, out var position)
                && !position.IsArchived
                && position.Status == PositionStatus.Active
                && unitById.TryGetValue(position.OrganizationUnitId, out var unit)
                && !unit.IsArchived)
            .Select(assignment => assignment.UserId)
            .ToHashSet();
    }

    /// <summary>Half-open interval, matching the position and org-unit lookups.</summary>
    private static bool IsActive(PositionAssignment assignment, DateTimeOffset asOf)
        => !assignment.IsCancelled
           && assignment.EffectiveFrom <= asOf
           && (assignment.EffectiveTo is null || assignment.EffectiveTo > asOf);
}
