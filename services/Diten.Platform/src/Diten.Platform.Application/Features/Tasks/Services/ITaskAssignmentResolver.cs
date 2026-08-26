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
    /// Acceptance is a FACT the task carries, not something read off its lifecycle.
    ///
    /// <para><b>BL-042 — what this used to say and why it was wrong.</b> The rule was
    /// <c>Lifecycle is not (Open or Planned)</c>, on the reasoning that work moving past the gate implied
    /// acceptance. It does not: planning a task moves it to Planned WITHOUT accepting it, and from there accept
    /// could never take effect — the handler only promoted Open, so a planned task stayed Planned, stayed
    /// "not accepted", and stayed in the Inbox forever while the endpoint reported success.</para>
    ///
    /// <para>Lifecycle answers "how far along is this work". Acceptance answers "did the assignee take it on".
    /// They are different questions, and every extra meaning hung on the lifecycle field reproduces this class.
    /// </para>
    /// </summary>
    private static bool IsAccepted(TaskItem task) => task.AcceptedByUserId is not null;
}
