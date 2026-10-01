using Diten.CrmService.Application.Features.Knowledge.Path.Review;
using Diten.CrmService.Domain.Entities;
using Journey = Diten.CrmService.Domain.Entities.ContentEngagementJourney;

namespace Diten.CrmService.Application.Features.Knowledge.Path.Release;

/// <summary>A published journey's stage that points at a path (its pinned version or, when the stage follows the
/// code, the latest published one).</summary>
public sealed record KnowledgePathJourneyUse(Journey Journey, ContentEngagementJourneyStage Stage)
{
    public string Label => $"{Journey.JourneyName} · {Stage.StageName}";
}

/// <summary>
/// WP-KP-3 — the release rules of a knowledge path, in ONE place. They came from the WP-SB-2 content-set release
/// producer (which now calls them — its behaviour is unchanged until KP-4 removes it):
/// <list type="bullet">
/// <item>a component is releasable only while it is published and not archived;</item>
/// <item>a release is single-language;</item>
/// <item>a path is "in use" by a published, non-archived journey whose active stage is pinned to it, or follows its
/// code (<c>latest-published</c>);</item>
/// <item>person-based release SoD (D-KP-8, §9 Q2 default): the publisher is not the revision's submitter. The stronger
/// design rule (not one of the MLR approvers) would be ONE more condition in <see cref="CanRelease"/>.</item>
/// </list>
/// The claim usability gate is WP-CL-BE-6's <c>KnowledgeContentClaimLinks.CheckPublishAsync</c> (already shared).
/// </summary>
public static class KnowledgePathReleaseRules
{
    /// <summary>The single release SoD point: the person who submitted the revision never releases it.</summary>
    public static bool CanRelease(KnowledgePathRevision revision, string? actor)
        => !string.IsNullOrWhiteSpace(actor)
           && !KnowledgePathReviewRules.SamePerson(actor, revision.CreatedBy)
           && !KnowledgePathReviewRules.SamePerson(actor, revision.ReviewRound.SubmittedBy);

    public static bool IsReleasableContent(KnowledgeContent? content)
        => content is not null && !content.IsArchived()
           && string.Equals(content.ContentStatus, KnowledgeContentStatuses.Published, StringComparison.OrdinalIgnoreCase);

    /// <summary>The distinct (trimmed, case-insensitive) languages; a release needs exactly one.</summary>
    public static IReadOnlyList<string> DistinctLanguages(IEnumerable<string?> languages)
        => languages.Select(l => l?.Trim() ?? string.Empty).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    /// <summary>The published, non-archived journeys' active stages using <paramref name="path"/>: pinned to this
    /// version, or — when <paramref name="includeLatestPublished"/> — following its code (counted conservatively).</summary>
    public static IReadOnlyList<KnowledgePathJourneyUse> PublishedJourneyUses(
        IEnumerable<Journey> journeys, KnowledgePath path, bool includeLatestPublished)
        => journeys
            .Where(j => j.IsPublished() && !j.IsArchived())
            .SelectMany(j => j.ActiveStages().Where(s => UsesPath(s, path, includeLatestPublished))
                .Select(s => new KnowledgePathJourneyUse(j, s)))
            .ToList();

    /// <summary>Every journey stage (any status) referencing the path — the usage read.</summary>
    public static bool UsesPath(ContentEngagementJourneyStage stage, KnowledgePath path, bool includeLatestPublished)
    {
        var followsLatest = string.Equals(stage.PathVersionPinPolicy, ContentEngagementJourneyPathPin.LatestPublished, StringComparison.OrdinalIgnoreCase);
        // Pinned-only (includeLatestPublished=false): a following stage moves on with the code, so it is not a use.
        return (stage.RecommendedKnowledgePathId == path.Id && (includeLatestPublished || !followsLatest))
               || (includeLatestPublished && followsLatest
                   && string.Equals(stage.PathCode, path.PathCode, StringComparison.OrdinalIgnoreCase));
    }
}
