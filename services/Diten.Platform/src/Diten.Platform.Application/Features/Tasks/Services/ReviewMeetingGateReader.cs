using Diten.Platform.Application.Features.Meetings.RecordLinks;
using Diten.Platform.Domain.Entities.Meetings;
using Diten.Platform.Domain.Enums.Meetings;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Tasks.Services;

/// <summary>
/// MOD-0357 S9 (owner, 2026-09-13) — the ONE place that answers "has this task's review-meeting requirement been
/// satisfied yet". A LOCAL, MOD-0024-owned precondition: it never touches <c>IWorkflowTransitionGate</c> or
/// <c>ITaskApprovalService</c>, and it never decides an approval outcome — it only reads the
/// <see cref="RecordLinkTypes.ReviewMeeting"/> link(s) a task carries and asks whether at least one non-cancelled
/// linked meeting has a PUBLISHED minutes version.
///
/// <para><b>Used by callers that must never drift</b> — <c>TaskWorkItemProvider</c> (the projection's own
/// hint, disabling the DECISION actions <c>submitReview</c>/<c>complete</c> with a visible reason; never
/// <c>start</c>) and the decision handlers <c>TransitionTaskItemHandler</c> (→ Done) and
/// <c>SubmitTaskForReviewHandler</c> (the actual re-check before the write commits). All of them reach it through
/// <see cref="ReviewMeetingDecisionGate"/>, which owns the "type requires it and it is not unlocked" rule.</para>
///
/// <para><b>The rule.</b> Multiple linked meetings unlock on ANY one publishing (not all). A CANCELLED meeting's
/// minutes — published or not — never unlock the gate. A merely SCHEDULED meeting with no minutes yet does not
/// unlock it. A corrected/superseded minutes version needs no special handling: <c>MeetingMinutesVersion</c> is
/// append-only and <see cref="IMeetingMinutesVersionRepository.GetLatestByMeetingIdAsync"/> already returns the
/// current truth, and a correction of a published minutes is itself created as Published — so "the latest version
/// is Published" is correct and sufficient on its own.</para>
/// </summary>
public interface IReviewMeetingGateReader
{
    /// <summary>Single-task read — the handler's own re-check before it commits a transition.</summary>
    Task<bool> HasUnlockedReviewMeetingAsync(Guid taskId, CancellationToken ct);

    /// <summary>
    /// Batched read for MANY tasks in ONE pass — the projection's own read, over a whole page of work items.
    /// A per-task call here would be an N+1 across the board, the same discipline every other container on
    /// <c>TaskWorkItemProvider</c>'s projection already follows (checklist, children, attachments, dependencies).
    /// A task absent from the result carries no live "reviewMeeting" link at all — treat that as "not unlocked".
    /// </summary>
    Task<IReadOnlyDictionary<Guid, bool>> ResolveUnlockedReviewMeetingsAsync(
        IReadOnlyCollection<Guid> taskIds, CancellationToken ct);
}

public sealed class ReviewMeetingGateReader : IReviewMeetingGateReader
{
    private readonly IRecordLinkService _recordLinks;
    private readonly IMeetingRepository _meetings;
    private readonly IMeetingMinutesVersionRepository _minutesVersions;

    public ReviewMeetingGateReader(
        IRecordLinkService recordLinks,
        IMeetingRepository meetings,
        IMeetingMinutesVersionRepository minutesVersions)
    {
        _recordLinks = recordLinks;
        _meetings = meetings;
        _minutesVersions = minutesVersions;
    }

    public async Task<bool> HasUnlockedReviewMeetingAsync(Guid taskId, CancellationToken ct)
    {
        var result = await ResolveUnlockedReviewMeetingsAsync([taskId], ct);
        return result.TryGetValue(taskId, out var unlocked) && unlocked;
    }

    public async Task<IReadOnlyDictionary<Guid, bool>> ResolveUnlockedReviewMeetingsAsync(
        IReadOnlyCollection<Guid> taskIds, CancellationToken ct)
    {
        if (taskIds.Count == 0)
        {
            return new Dictionary<Guid, bool>();
        }

        var links = await _recordLinks.ListByTargetAsync(taskIds, ct);
        var reviewLinksByTask = links
            .Where(link => link.LinkType == RecordLinkTypes.ReviewMeeting
                           && link.SourceModuleCode == RecordLinkModuleCodes.Meetings)
            .GroupBy(link => link.TargetRecordId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<RecordLink>)group.ToList());

        if (reviewLinksByTask.Count == 0)
        {
            return new Dictionary<Guid, bool>();
        }

        var meetingIds = reviewLinksByTask.Values
            .SelectMany(taskLinks => taskLinks.Select(link => link.SourceRecordId))
            .Distinct()
            .ToList();
        var meetingsById = (await _meetings.ListByIdsAsync(meetingIds, ct)).ToDictionary(m => m.Id);

        // One latest-version read per candidate meeting — never per task — so a task with several linked
        // meetings does not multiply the read, and a meeting linked to several tasks is read once.
        var latestByMeeting = new Dictionary<Guid, MeetingMinutesVersion?>();
        foreach (var meetingId in meetingIds)
        {
            latestByMeeting[meetingId] = await _minutesVersions.GetLatestByMeetingIdAsync(meetingId, ct);
        }

        var result = new Dictionary<Guid, bool>();
        foreach (var (taskId, taskLinks) in reviewLinksByTask)
        {
            var unlocked = taskLinks.Any(link =>
                meetingsById.TryGetValue(link.SourceRecordId, out var meeting)
                // A cancelled meeting's minutes — published or not — never unlock the gate (owner rule).
                && meeting.Lifecycle != MeetingLifecycle.Cancelled
                && latestByMeeting.TryGetValue(link.SourceRecordId, out var latest)
                && latest?.Status == MinutesStatus.Published);
            result[taskId] = unlocked;
        }

        return result;
    }
}
