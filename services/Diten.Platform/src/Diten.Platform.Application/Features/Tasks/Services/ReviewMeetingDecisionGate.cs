using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// MOD-0357 S9 (owner, 2026-09-13; CT fix-up F1, 2026-09-15) — the ONE statement of the review-meeting gate's rule:
/// "this task's TYPE requires a review meeting, and no non-cancelled linked meeting has published minutes yet".
/// <see cref="IReviewMeetingGateReader"/> answers the meeting/minutes half; this answers the whole question for the
/// three callers that must never drift on it — <c>TaskWorkItemProvider</c> (the projection's disabled hint),
/// <c>TransitionTaskItemHandler</c> (refuses → Done) and <c>SubmitTaskForReviewHandler</c> (refuses → PendingReview).
///
/// <para><b>It gates the DECISION, never the work.</b> Only the task's own decision transitions — complete and submit
/// for review — wait for published minutes. <c>start</c>/resume is never gated: scheduling and holding the meeting is
/// part of the work, so the work must be able to begin (MOD-0024 pack "Review meeting policy"; MOD-0357 K3).</para>
///
/// <para><b>Approval boundary.</b> A LOCAL MOD-0024 precondition over a RecordLink this module reads. It never calls
/// <c>IWorkflowTransitionGate</c> or <c>ITaskApprovalService</c>, and never opens, decides or alters a MOD-0023
/// instance. The handlers ask it BEFORE any MOD-0023 handoff, so a refusal leaves nothing behind in MOD-0023.</para>
/// </summary>
public static class ReviewMeetingDecisionGate
{
    /// <summary>One sentence for both handlers, so a refused complete and a refused submit read the same.</summary>
    public const string RefusalMessage = "This task's review meeting has not published its minutes yet.";

    /// <summary>The pure rule. Optional, NotAllowed and an absent/unresolvable type never block.</summary>
    public static bool Blocks(TaskReviewMeetingRequirement? requirement, bool unlocked)
        => requirement == TaskReviewMeetingRequirement.Required && !unlocked;

    /// <summary>
    /// The handlers' re-check before a decision commits. The TYPE is read first and the reader is asked ONLY for a
    /// Required type, so Optional/NotAllowed work never pays for — or depends on — the meeting read.
    ///
    /// <para>FAIL-CLOSED on a missing seam, the same posture as the workflow gate: an absent reader, or an absent type
    /// repository for a task that HAS a type, counts as blocked — neither can prove the requirement is met. DI always
    /// registers both, so only a hand-built construction ever reaches those branches.</para>
    /// </summary>
    public static async Task<bool> BlocksDecisionAsync(
        TaskItem task, ITaskTypeRepository? types, IReviewMeetingGateReader? reader, CancellationToken ct)
    {
        if (task.TaskTypeId is not { } typeId)
        {
            return false;
        }

        if (types is null)
        {
            return true;
        }

        var type = await types.GetByIdAsync(typeId, ct);
        if (type?.ReviewMeetingRequirement != TaskReviewMeetingRequirement.Required)
        {
            return false;
        }

        var unlocked = reader is not null && await reader.HasUnlockedReviewMeetingAsync(task.Id, ct);
        return Blocks(type.ReviewMeetingRequirement, unlocked);
    }

    /// <summary>The refusal both decision handlers return: 409 + <see cref="TaskReasonCodes.ReviewMeetingRequired"/>.</summary>
    public static Response<NoContent> Refusal(string correlationId)
        => Response<NoContent>.Fail(RefusalMessage, 409, TaskReasonCodes.ReviewMeetingRequired, correlationId);
}
