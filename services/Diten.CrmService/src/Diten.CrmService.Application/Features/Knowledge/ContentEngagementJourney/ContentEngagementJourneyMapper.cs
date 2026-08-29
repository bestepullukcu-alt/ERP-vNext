using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney;

using JourneyEntity = Diten.CrmService.Domain.Entities.ContentEngagementJourney;

/// <summary>
/// MOD-0162 FU05 aggregate ↔ DTO projection + deterministic KnowledgePath resolution (§8.3). Reads never echo TenantId
/// (server-resolved). The path is resolved per <c>PathVersionPinPolicy</c> and the resolution status is ALWAYS surfaced
/// (pinned / resolved-latest / unresolved) — a stage is never hidden, dropped or filled with a guess. The FU04 path is
/// read ONLY: its steps are never copied into the journey document (only an active-step COUNT is surfaced) and no
/// FU04 aggregate is mutated. No score / best-next / current-stage / advancement is computed anywhere here.
/// </summary>
public static class ContentEngagementJourneyMapper
{
    /// <summary>Read-side lookups the resolver needs — tenant KnowledgePaths by id and by code (FU04, read-only).</summary>
    public sealed class ResolutionContext
    {
        public IReadOnlyDictionary<Guid, KnowledgePath> PathsById { get; }
        public ILookup<string, KnowledgePath> PathsByCode { get; }

        public ResolutionContext(IReadOnlyList<KnowledgePath> paths)
        {
            PathsById = paths.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());
            PathsByCode = paths.ToLookup(p => p.PathCode ?? string.Empty, StringComparer.OrdinalIgnoreCase);
        }

        public static ResolutionContext Empty { get; } = new(Array.Empty<KnowledgePath>());
    }

    private sealed record StageResolution(
        Guid? ResolvedKnowledgePathId,
        string? ResolvedPathVersion,
        string? ResolvedPathName,
        int? ResolvedPathStepCount,
        string Status,
        Guid? PathSubjectId,
        string? PathLanguage);

    private static StageResolution Resolve(
        ContentEngagementJourneyStage stage, ResolutionContext ctx, DateTimeOffset effectiveAt)
    {
        if (string.Equals(
                stage.PathVersionPinPolicy,
                ContentEngagementJourneyPathPin.LatestPublished,
                StringComparison.OrdinalIgnoreCase))
        {
            var candidate = ctx.PathsByCode[stage.PathCode ?? string.Empty]
                .Where(p => !p.IsArchived() && p.IsPublished() && p.IsEffectiveAt(effectiveAt))
                .OrderByDescending(p => p.EffectiveFrom)
                .ThenByDescending(p => p.PathVersion, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            return candidate is null
                ? new StageResolution(null, null, null, null,
                    ContentEngagementJourneyPathResolutionStatuses.Unresolved, null, null)
                : new StageResolution(
                    candidate.Id, candidate.PathVersion, candidate.PathName, candidate.ActiveSteps().Count(),
                    ContentEngagementJourneyPathResolutionStatuses.ResolvedLatest,
                    candidate.SubjectId, candidate.LanguageCode);
        }

        // pinned (default): the stage stays fixed to its RecommendedKnowledgePathId even if the path later archives.
        var pinned = ctx.PathsById.TryGetValue(stage.RecommendedKnowledgePathId, out var path) ? path : null;
        return new StageResolution(
            stage.RecommendedKnowledgePathId,
            pinned?.PathVersion,
            pinned?.PathName,
            pinned?.ActiveSteps().Count(),
            ContentEngagementJourneyPathResolutionStatuses.Pinned,
            pinned?.SubjectId,
            pinned?.LanguageCode);
    }

    /// <summary>FU01B §7 repeat report: how many ACTIVE stages of this journey use the same path code. A repeat is
    /// never forbidden — it is made VISIBLE.</summary>
    private static int PathUsageCount(JourneyEntity journey, ContentEngagementJourneyStage stage)
        => journey.Stages.Count(s =>
            !s.IsArchived() &&
            string.Equals(s.PathCode ?? string.Empty, stage.PathCode ?? string.Empty, StringComparison.OrdinalIgnoreCase));

    public static ContentEngagementJourneyStageDto ToStageDto(
        JourneyEntity journey, ContentEngagementJourneyStage stage, ResolutionContext ctx, DateTimeOffset effectiveAt)
    {
        var resolution = Resolve(stage, ctx, effectiveAt);

        var crossSubject = resolution.PathSubjectId is { } sub && sub != journey.SubjectId;
        var crossLanguage = !string.IsNullOrWhiteSpace(journey.LanguageCode)
            && !string.IsNullOrWhiteSpace(resolution.PathLanguage)
            && !string.Equals(journey.LanguageCode, resolution.PathLanguage, StringComparison.OrdinalIgnoreCase);

        return new ContentEngagementJourneyStageDto(
            stage.StageId,
            stage.StageOrder,
            stage.StageCode,
            stage.StageName,
            stage.StageObjective,
            stage.StageType,
            stage.RecommendedKnowledgePathId,
            stage.PathCode,
            stage.PathVersionPinPolicy,
            stage.IsRequired,
            stage.Repeatable,
            stage.MinVisitNumber,
            stage.MaxVisitNumber,
            stage.AdvancementRule,
            stage.FallbackStageId,
            stage.Notes,
            stage.BranchConditions
                .Select(b => new ContentEngagementJourneyBranchConditionDto(
                    b.ConditionCode, b.Description, b.TargetStageId))
                .ToList(),
            stage.StageStatus,
            resolution.ResolvedKnowledgePathId,
            resolution.ResolvedPathVersion,
            resolution.ResolvedPathName,
            resolution.ResolvedPathStepCount,
            resolution.Status,
            crossSubject,
            crossLanguage,
            PathUsageCount(journey, stage),
            stage.ArchivedAt,
            stage.ArchivedBy,
            stage.CreatedAt,
            stage.CreatedBy,
            stage.UpdatedAt,
            stage.UpdatedBy,
            stage.IsArchived());
    }

    /// <summary>Ordered active stages (StageOrder → StageCode); archived stages included only when
    /// <paramref name="includeArchived"/> (kept at the tail, still ordered).</summary>
    public static IReadOnlyList<ContentEngagementJourneyStageDto> ToStageDtos(
        JourneyEntity journey, ResolutionContext ctx, DateTimeOffset effectiveAt, bool includeArchived)
        => journey.Stages
            .Where(s => includeArchived || !s.IsArchived())
            .OrderBy(s => s.IsArchived())
            .ThenBy(s => s.StageOrder)
            .ThenBy(s => s.StageCode, StringComparer.OrdinalIgnoreCase)
            .Select(s => ToStageDto(journey, s, ctx, effectiveAt))
            .ToList();

    public static ContentEngagementJourneyDto ToDto(
        JourneyEntity journey, ResolutionContext ctx, DateTimeOffset effectiveAt)
    {
        var stages = ToStageDtos(journey, ctx, effectiveAt, includeArchived: true);
        var activeStages = stages.Where(s => !s.IsArchived).ToList();

        var resolutions = journey.Stages
            .Where(s => !s.IsArchived())
            .Select(s => Resolve(s, ctx, effectiveAt))
            .ToList();

        var languages = resolutions
            .Select(r => r.PathLanguage)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var subjects = resolutions
            .Select(r => r.PathSubjectId)
            .Where(sub => sub is not null)
            .Distinct()
            .ToList();

        return new ContentEngagementJourneyDto(
            journey.Id,
            journey.JourneyCode,
            journey.JourneyName,
            journey.Description,
            journey.SubjectId,
            journey.TopicId,
            journey.AudienceProfileId,
            journey.Objective,
            journey.LanguageCode,
            journey.JourneyVersion,
            journey.JourneyStatus,
            journey.EffectiveFrom,
            journey.EffectiveTo,
            journey.Source,
            stages,
            activeStages.Count,
            activeStages.Count(s => s.IsRequired),
            activeStages.Count(s => s.Repeatable),
            IsMixedLanguage: languages.Count > 1,
            IsMixedSubject: subjects.Count > 1,
            HasUnresolvedStagePath: activeStages.Any(s =>
                s.PathResolutionStatus == ContentEngagementJourneyPathResolutionStatuses.Unresolved),
            HasRepeatedPaths: activeStages.Any(s => s.PathUsageCountInJourney > 1),
            journey.IsStageSetFrozen(),
            journey.StageSetFrozenAt,
            journey.PublishedAt,
            journey.PublishedBy,
            journey.SupersedesJourneyId,
            journey.Version,
            journey.CreatedAt,
            journey.CreatedBy,
            journey.UpdatedAt,
            journey.UpdatedBy,
            journey.ArchivedAt,
            journey.ArchivedBy,
            journey.IsArchived());
    }

    public static ContentEngagementJourneyListItemDto ToListItem(
        JourneyEntity journey, ResolutionContext ctx, DateTimeOffset effectiveAt)
    {
        var active = journey.Stages.Where(s => !s.IsArchived()).ToList();
        var hasUnresolved = active.Any(s =>
            Resolve(s, ctx, effectiveAt).Status == ContentEngagementJourneyPathResolutionStatuses.Unresolved);
        var hasRepeated = active
            .GroupBy(s => s.PathCode ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Any(g => g.Count() > 1);

        return new ContentEngagementJourneyListItemDto(
            journey.Id,
            journey.JourneyCode,
            journey.JourneyName,
            journey.SubjectId,
            journey.TopicId,
            journey.AudienceProfileId,
            journey.LanguageCode,
            journey.JourneyVersion,
            journey.JourneyStatus,
            journey.EffectiveFrom,
            journey.EffectiveTo,
            journey.Source,
            active.Count,
            active.Count(s => s.IsRequired),
            active.Count(s => s.Repeatable),
            hasUnresolved,
            hasRepeated,
            journey.IsStageSetFrozen(),
            journey.CreatedAt,
            journey.CreatedBy,
            journey.UpdatedAt,
            journey.UpdatedBy,
            journey.ArchivedAt,
            journey.IsArchived());
    }
}
