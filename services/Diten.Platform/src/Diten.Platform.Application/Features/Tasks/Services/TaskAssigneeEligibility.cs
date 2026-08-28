using Diten.Platform.Domain.Entities.Organization;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// Who work may be assigned to — ONE rule, shared by the people picker and by the reassign endpoint.
///
/// <para>The picker answers "who can I choose" and reassign answers "may I choose this one". Written twice they
/// drift, and the drift is invisible in the direction that matters: the picker narrows, the endpoint stays wide,
/// and work lands on somebody the product no longer considers assignable. Both call this.</para>
///
/// <para>A person is assignable when they currently HOLD an active position in a live unit. This type answers
/// only the POSITION-and-UNIT half of that sentence; "currently holds" belongs to
/// <see cref="ITaskSeatDirectory"/> and the assignments handed in here are already windowed by it. Re-testing
/// the window here would put the rule in two places, which is exactly the shape this type exists to prevent
/// one level up.</para>
///
/// <para>The unit matters because a position without a resolvable unit cannot be told apart from a namesake
/// elsewhere — the picker refuses to show such a row, so reassign must refuse to accept one.</para>
/// </summary>
public static class TaskAssigneeEligibility
{
    /// <summary>
    /// The user ids that may hold work, given the current org data. <paramref name="activeAssignments"/> must
    /// come from <see cref="ITaskSeatDirectory"/> — there is no "as of" parameter because the caller no longer
    /// chooses the moment; the directory does.
    /// </summary>
    public static HashSet<Guid> ResolveAssignableUserIds(
        IEnumerable<PositionAssignment> activeAssignments,
        IEnumerable<Position> positions,
        IEnumerable<OrganizationUnit> organizationUnits)
    {
        var positionById = positions.ToDictionary(position => position.Id);
        var unitById = organizationUnits.ToDictionary(unit => unit.Id);

        return activeAssignments
            .Where(assignment =>
                positionById.TryGetValue(assignment.PositionId, out var position)
                && !position.IsArchived
                && position.Status == PositionStatus.Active
                && unitById.TryGetValue(position.OrganizationUnitId, out var unit)
                && !unit.IsArchived)
            .Select(assignment => assignment.UserId)
            .ToHashSet();
    }
}
