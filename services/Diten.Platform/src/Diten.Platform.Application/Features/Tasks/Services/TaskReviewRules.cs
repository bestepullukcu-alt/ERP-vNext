using Diten.Platform.Domain.Entities.Tasks;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// The ONE rule that decides whether a task's review requirement is complete enough to be honoured.
///
/// <para><b>Why it exists as a type instead of an <c>if</c>.</b> A review can only be started if MOD-0023 is given
/// at least one candidate to route it to — its own validator refuses an empty candidate list. Approval learned
/// this a phase earlier and answered it with the same check written out three times (the create validator, the
/// create handler and the update handler). Review reached production with the check written ZERO times, so the
/// form happily produced a task whose review could never start: the workflow was refused, the task stayed in
/// progress, and `submitReview` answered 409 forever with nothing the user could do about it.</para>
///
/// <para>One rule, three call sites, so the create path and the update path cannot drift. The update path matters
/// most: <see cref="TaskModels.UpdateTaskItemRequest"/> is a FULL REPLACE, so an edit that simply omits the
/// reviewer would otherwise strip it from a task whose review requirement is still on.</para>
/// </summary>
public static class TaskReviewRules
{
    /// <summary>
    /// The message every path reports, in the same voice as approval's — the two are read side by side in the
    /// same form, and two different phrasings for one shape of mistake reads as two different mistakes.
    /// </summary>
    public const string ReviewerRequiredMessage = "A reviewer is required when a review is requested.";

    /// <summary>
    /// True when the requirement is on but nobody has been named to route it to — the state MOD-0023 cannot start
    /// a review from.
    /// </summary>
    public static bool ReviewerMissing(bool reviewRequired, Guid? reviewerCandidateUserId)
        => reviewRequired && (reviewerCandidateUserId is null || reviewerCandidateUserId == Guid.Empty);

    /// <summary>
    /// The same question asked of a task that already exists, for the update path — where the incoming value and
    /// the stored one both matter.
    /// </summary>
    public static bool ReviewerMissing(TaskItem task)
    {
        ArgumentNullException.ThrowIfNull(task);
        return ReviewerMissing(task.ReviewRequired, task.ReviewerCandidateUserId);
    }
}
