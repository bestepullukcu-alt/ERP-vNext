using Diten.Platform.Domain.Enums.Tasks;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// The ONE rule that decides whether an assignment intent is complete enough to act on (pack §12 K5).
///
/// <para><b>Why it was extracted.</b> It lived inside <c>CreateTaskItemHandler</c>'s switch, which was fine while
/// creation was the only thing that assigned work. Recurrence rules now carry an assignment too, and writing the
/// same check a second time there is exactly the shape that produced the reviewer defect a slice ago: approval's
/// identical rule written out three times, and review reaching production with none.</para>
///
/// <para><b>SelfAssigned is conditional, and that condition is the whole point.</b> "Self" only means something
/// when there IS a caller. A recurring sweep has none — the current-user context answers <c>Guid.Empty</c> with
/// no HTTP request behind it — so a rule that said SelfAssigned produced work assigned to nobody, visible in no
/// list, while still consuming its period. That is why <paramref name="allowSelfAssigned"/> exists rather than a
/// second copy of this method.</para>
/// </summary>
public static class TaskAssignmentIntentRules
{
    public const string SelfAssignedNotAvailableMessage =
        "A recurring rule cannot assign work to \"self\": nobody is running it. Name a person or a pool.";

    public const string AssigneeRequiredMessage = "An assignee is required when assigning to a person.";

    public const string PoolPositionRequiredMessage = "A position is required when pooling a task.";

    public const string RequestNotAvailableMessage =
        "A request is sent by a person, to a person. A recurring rule or a template cannot send one.";

    public const string OfferedToRequiredMessage = "Say who the request is for.";

    public const string RequestCarriesNoHolderMessage =
        "A request must not carry an assignee or a pool; it has no holder until it is accepted.";

    public const string OfferedToOnlyOnRequestMessage =
        "A person to ask can only be named on a request.";

    public const string PoolCarriesNoAssigneeMessage =
        "A pooled task must not carry an assignee; it is claimed later.";

    /// <summary>
    /// The reason this intent cannot be acted on, or null when it is complete. The MESSAGE is returned alongside
    /// so the two never drift — a reason code with the wrong sentence beside it is worse than either alone.
    /// </summary>
    public static (string ReasonCode, string Message)? Validate(
        TaskAssignmentTarget target,
        Guid? assigneeUserId,
        Guid? poolPositionId,
        bool allowSelfAssigned,
        Guid? offeredToUserId = null,
        // BL-509 — only a person filling the create form may send a request. A recurrence rule and a template name
        // nobody who could be asked, so both leave this false and an Offered target is refused for them.
        bool allowOffered = false)
    {
        if (target != TaskAssignmentTarget.Offered && !IsMissing(offeredToUserId))
        {
            return (TaskReasonCodes.AssignmentTargetInvalid, OfferedToOnlyOnRequestMessage);
        }

        switch (target)
        {
            case TaskAssignmentTarget.SelfAssigned:
                return allowSelfAssigned
                    ? null
                    : (TaskReasonCodes.AssignmentTargetInvalid, SelfAssignedNotAvailableMessage);

            case TaskAssignmentTarget.Person:
                return IsMissing(assigneeUserId)
                    ? (TaskReasonCodes.AssignmentTargetInvalid, AssigneeRequiredMessage)
                    : null;

            case TaskAssignmentTarget.PositionPool:
                if (IsMissing(poolPositionId))
                {
                    return (TaskReasonCodes.AssignmentTargetInvalid, PoolPositionRequiredMessage);
                }

                // A pool task has NO holder until someone claims it — that is the point of a pool, and an
                // assignee travelling with one means the caller misunderstood the target.
                return IsMissing(assigneeUserId)
                    ? null
                    : (TaskReasonCodes.AssignmentTargetInvalid, PoolCarriesNoAssigneeMessage);

            case TaskAssignmentTarget.Offered:
                if (!allowOffered)
                {
                    return (TaskReasonCodes.AssignmentTargetInvalid, RequestNotAvailableMessage);
                }

                if (IsMissing(offeredToUserId))
                {
                    return (TaskReasonCodes.AssignmentTargetInvalid, OfferedToRequiredMessage);
                }

                // Nobody holds a request until it is accepted, and it is not pooled either.
                return IsMissing(assigneeUserId) && IsMissing(poolPositionId)
                    ? null
                    : (TaskReasonCodes.AssignmentTargetInvalid, RequestCarriesNoHolderMessage);

            default:
                return (TaskReasonCodes.AssignmentTargetInvalid, "Unsupported assignment target.");
        }
    }

    /// <summary>
    /// <c>Guid.Empty</c> counts as missing, not as an identity. It is what an unfilled form field deserializes
    /// to, and it is precisely the value a background job's current-user context returns.
    /// </summary>
    private static bool IsMissing(Guid? id) => id is null || id == Guid.Empty;
}
