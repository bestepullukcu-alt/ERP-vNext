using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// MOD-0024 — translates assignment INTENT (self / person / position pool) into the executable contract's
/// projection triple (pack §12 K5). Keeping this in one place is what makes pools addable without rewriting the
/// Task Center: the surface reads only the contract fields, never MOD-0024's internal target enum.
///
/// | Target | assignmentMode | ownershipState | admissionState   |
/// |--------|----------------|----------------|------------------|
/// | Self   | direct         | owned          | admitted         |
/// | Person | direct         | assigned       | pendingAcceptance|
/// | Pool   | groupQueue     | unowned        | pendingClaim     |
///
/// A claimed pool task projects as owned + admitted (it now has a holder), which is precisely why the pool tab
/// empties when someone takes the work.
/// </summary>
public interface ITaskAssignmentResolver
{
    TaskAssignmentProjection Resolve(TaskItem task);
}

/// <summary>The three contract fields that describe who holds a work item and how it was offered.</summary>
public sealed record TaskAssignmentProjection(string AssignmentMode, string OwnershipState, string AdmissionState);

public sealed class TaskAssignmentResolver : ITaskAssignmentResolver
{
    // fixture-contract.js ASSIGNMENT_MODES / OWNERSHIP_STATES / ADMISSION_STATES
    private const string ModeDirect = "direct";
    private const string ModeGroupQueue = "groupQueue";
    private const string Owned = "owned";
    private const string Assigned = "assigned";
    private const string Unowned = "unowned";
    private const string Admitted = "admitted";
    private const string PendingAcceptance = "pendingAcceptance";
    private const string PendingClaim = "pendingClaim";

    public TaskAssignmentProjection Resolve(TaskItem task)
    {
        ArgumentNullException.ThrowIfNull(task);

        switch (task.AssignmentTarget)
        {
            case TaskAssignmentTarget.SelfAssigned:
                // The creator is the holder from the first moment: no acceptance gate.
                return new TaskAssignmentProjection(ModeDirect, Owned, Admitted);

            case TaskAssignmentTarget.Person:
                // Someone else assigned it. Until accepted it is theirs but not yet owned — the Inbox
                // acceptance gate. Once accepted the holder owns it.
                return task.AssigneeUserId is not null && task.CompletedAt is null && IsAccepted(task)
                    ? new TaskAssignmentProjection(ModeDirect, Owned, Admitted)
                    : new TaskAssignmentProjection(ModeDirect, Assigned, PendingAcceptance);

            case TaskAssignmentTarget.PositionPool:
                // Unclaimed → nobody owns it and it sits in the pool. Claimed → a real holder, so it leaves the
                // pool and behaves like owned work.
                return task.AssigneeUserId is null
                    ? new TaskAssignmentProjection(ModeGroupQueue, Unowned, PendingClaim)
                    : new TaskAssignmentProjection(ModeGroupQueue, Owned, Admitted);

            default:
                throw new InvalidOperationException($"Unmapped TaskAssignmentTarget: {task.AssignmentTarget}");
        }
    }

    /// <summary>
    /// A person-assigned task counts as accepted once work has actually moved past the acceptance gate. Phase 1
    /// has no separate Accepted flag; lifecycle progression is the signal (Open/Planned = not yet accepted).
    /// </summary>
    private static bool IsAccepted(TaskItem task)
        => task.Lifecycle is not (TaskLifecycle.Open or TaskLifecycle.Planned);
}
