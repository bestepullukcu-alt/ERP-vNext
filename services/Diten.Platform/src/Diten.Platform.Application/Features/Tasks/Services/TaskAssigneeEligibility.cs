using Diten.Platform.Domain.Entities.Organization;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>Why a seat may or may not receive work. Ordered most-severe-last, and the people picker relies on
/// that order to report a person with several seats by their BEST outcome.</summary>
public enum TaskAssigneeVerdict
{
    /// <summary>Not an active position in a live unit — Draft, archived, or a unit that is gone.</summary>
    PositionNotActive = 1,

    /// <summary>A genuinely usable seat, but outside the actor's scope (BL-057).</summary>
    OutOfScope = 2,

    Assignable = 3
}

/// <summary>
/// Who work may be assigned to — ONE rule, asked by both pickers and by every write that hands work to somebody.
///
/// <para>The picker answers "who can I choose" and the write answers "may I choose this one". Written twice they
/// drift, and the drift is invisible in the direction that matters: the picker narrows, the endpoint stays wide,
/// and work lands on somebody the product no longer considers assignable. That drift had already started — the
/// people picker carried its own copy of the position-and-unit test while this comment claimed it called here,
/// and no write path asked the scope at all. <see cref="Judge"/> is now the single place; the pickers and
/// <see cref="TaskAssignmentGuard"/> all call it.</para>
///
/// <para>A seat is assignable when it is an ACTIVE position in a LIVE unit and, for an assignment, inside the
/// actor's <see cref="TaskAssignmentScope"/>. "Currently holds" belongs to <see cref="ITaskSeatDirectory"/> and
/// the seats handed in here are already windowed by it; re-testing the window here would put the rule in two
/// places, which is exactly the shape this type exists to prevent one level up.</para>
///
/// <para>The unit matters because a position without a resolvable unit cannot be told apart from a namesake
/// elsewhere — the picker refuses to show such a row, so a write must refuse to accept one.</para>
/// </summary>
public static class TaskAssigneeEligibility
{
    /// <summary>
    /// May work be handed to the holder of <paramref name="position"/>?
    ///
    /// <para><paramref name="scope"/> null means a DECISION question (approver, reviewer, "waiting on"), which is
    /// scope-exempt by design — see <c>TaskPersonLookupPurpose</c>. An ASSIGNMENT always passes the actor's
    /// resolved scope, and an empty scope refuses everyone (fail-closed).</para>
    /// </summary>
    /// <param name="unit">The position's unit when the answer is not <see cref="TaskAssigneeVerdict.PositionNotActive"/>
    /// — the pickers need its label, and looking it up twice would be a second join to disagree with.</param>
    public static TaskAssigneeVerdict Judge(
        Position? position,
        IReadOnlyDictionary<Guid, OrganizationUnit> unitById,
        TaskAssignmentScope? scope,
        out OrganizationUnit? unit)
    {
        unit = null;

        if (position is null || position.IsArchived || position.Status != PositionStatus.Active)
        {
            return TaskAssigneeVerdict.PositionNotActive;
        }

        if (!unitById.TryGetValue(position.OrganizationUnitId, out var found) || found.IsArchived)
        {
            return TaskAssigneeVerdict.PositionNotActive;
        }

        unit = found;

        // BL-057. Asked of the scope rather than re-derived: Allows() is the one statement of the three legs.
        return scope is null || scope.Allows(position.Id, found.Id, found.LegalEntityId)
            ? TaskAssigneeVerdict.Assignable
            : TaskAssigneeVerdict.OutOfScope;
    }

    /// <summary>
    /// The user ids that may be NAMED on a decision (scope-exempt), given the current org data.
    /// <paramref name="activeAssignments"/> must come from <see cref="ITaskSeatDirectory"/> — there is no "as of"
    /// parameter because the caller no longer chooses the moment; the directory does.
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
                Judge(positionById.GetValueOrDefault(assignment.PositionId), unitById, scope: null, out _)
                == TaskAssigneeVerdict.Assignable)
            .Select(assignment => assignment.UserId)
            .ToHashSet();
    }
}
