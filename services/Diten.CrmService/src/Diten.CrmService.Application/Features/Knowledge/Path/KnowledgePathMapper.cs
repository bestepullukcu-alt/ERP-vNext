using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Knowledge.Path;

/// <summary>
/// MOD-0162 FU04 aggregate ↔ DTO projection + deterministic content resolution (§8.3). Reads never echo TenantId
/// (server-resolved). Content is resolved per <c>VersionPinPolicy</c> and the resolution status is ALWAYS surfaced
/// (pinned / resolved-latest / unresolved) — a step is never hidden, dropped or filled with a guess. Concept node
/// fields are label reads only (the node is never mutated). No score / best-next / recommendation is computed.
/// </summary>
public static class KnowledgePathMapper
{
    /// <summary>Read-side lookups the resolver needs — tenant content (by id + by code) and concept nodes (by id).</summary>
    public sealed class ResolutionContext
    {
        public IReadOnlyDictionary<Guid, KnowledgeContent> ContentsById { get; }
        public ILookup<string, KnowledgeContent> ContentsByCode { get; }
        public IReadOnlyDictionary<Guid, ConceptNode> NodesById { get; }

        public ResolutionContext(
            IReadOnlyList<KnowledgeContent> contents, IReadOnlyList<ConceptNode> nodes)
        {
            ContentsById = contents.GroupBy(c => c.Id).ToDictionary(g => g.Key, g => g.First());
            ContentsByCode = contents.ToLookup(c => c.ContentCode ?? string.Empty, StringComparer.OrdinalIgnoreCase);
            NodesById = nodes.GroupBy(n => n.Id).ToDictionary(g => g.Key, g => g.First());
        }

        public static ResolutionContext Empty { get; } =
            new(Array.Empty<KnowledgeContent>(), Array.Empty<ConceptNode>());
    }

    private sealed record StepResolution(
        Guid? ResolvedContentId,
        string? ResolvedContentVersion,
        string? ResolvedContentTitle,
        string Status,
        Guid? ContentSubjectId,
        string? ContentLanguage);

    private static StepResolution Resolve(KnowledgePathStep step, ResolutionContext ctx, DateTimeOffset effectiveAt)
    {
        if (string.Equals(step.VersionPinPolicy, KnowledgePathVersionPin.LatestPublished, StringComparison.OrdinalIgnoreCase))
        {
            var candidate = ctx.ContentsByCode[step.ContentCode ?? string.Empty]
                .Where(c => c.IsConsumableAt(effectiveAt))
                .OrderByDescending(c => c.EffectiveFrom)
                .ThenByDescending(c => c.ContentVersion, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();

            return candidate is null
                ? new StepResolution(null, null, null,
                    KnowledgePathContentResolutionStatuses.Unresolved, null, null)
                : new StepResolution(candidate.Id, candidate.ContentVersion, candidate.ContentTitle,
                    KnowledgePathContentResolutionStatuses.ResolvedLatest, candidate.SubjectId, candidate.LanguageCode);
        }

        // pinned (default): the step stays fixed to its ContentId even if the content later archives.
        var pinned = ctx.ContentsById.TryGetValue(step.ContentId, out var content) ? content : null;
        return new StepResolution(
            step.ContentId, pinned?.ContentVersion, pinned?.ContentTitle,
            KnowledgePathContentResolutionStatuses.Pinned, pinned?.SubjectId, pinned?.LanguageCode);
    }

    public static KnowledgePathStepDto ToStepDto(
        KnowledgePath path, KnowledgePathStep step, ResolutionContext ctx, DateTimeOffset effectiveAt)
    {
        var resolution = Resolve(step, ctx, effectiveAt);
        var node = step.ConceptNodeId is { } nodeId && ctx.NodesById.TryGetValue(nodeId, out var n) ? n : null;

        var crossSubject = resolution.ContentSubjectId is { } sub && sub != path.SubjectId;
        var crossLanguage = !string.IsNullOrWhiteSpace(path.LanguageCode)
            && !string.IsNullOrWhiteSpace(resolution.ContentLanguage)
            && !string.Equals(path.LanguageCode, resolution.ContentLanguage, StringComparison.OrdinalIgnoreCase);

        return new KnowledgePathStepDto(
            step.StepId,
            step.StepOrder,
            step.StepCode,
            step.StepTitle,
            step.StepType,
            step.ContentId,
            step.ContentCode,
            step.VersionPinPolicy,
            step.IsRequired,
            step.CompletionRule,
            step.PrerequisiteStepId,
            step.ConceptNodeId,
            step.EstimatedDurationMinutes,
            step.Notes,
            step.BranchConditions
                .Select(b => new KnowledgePathBranchConditionDto(b.ConditionCode, b.Description, b.TargetStepId))
                .ToList(),
            step.StepStatus,
            resolution.ResolvedContentId,
            resolution.ResolvedContentVersion,
            resolution.ResolvedContentTitle,
            resolution.Status,
            crossSubject,
            crossLanguage,
            node?.ConceptNodeCode,
            node?.ConceptNodeName,
            step.ArchivedAt,
            step.ArchivedBy,
            step.CreatedAt,
            step.CreatedBy,
            step.UpdatedAt,
            step.UpdatedBy,
            step.IsArchived());
    }

    /// <summary>Ordered active steps (StepOrder → StepCode); archived steps included only when
    /// <paramref name="includeArchived"/> (kept at the tail, still ordered).</summary>
    public static IReadOnlyList<KnowledgePathStepDto> ToStepDtos(
        KnowledgePath path, ResolutionContext ctx, DateTimeOffset effectiveAt, bool includeArchived)
    {
        var ordered = path.Steps
            .Where(s => includeArchived || !s.IsArchived())
            .OrderBy(s => s.IsArchived())
            .ThenBy(s => s.StepOrder)
            .ThenBy(s => s.StepCode, StringComparer.OrdinalIgnoreCase)
            .Select(s => ToStepDto(path, s, ctx, effectiveAt))
            .ToList();
        return ordered;
    }

    public static KnowledgePathDto ToDto(KnowledgePath path, ResolutionContext ctx, DateTimeOffset effectiveAt)
    {
        var steps = ToStepDtos(path, ctx, effectiveAt, includeArchived: true);
        var activeSteps = steps.Where(s => !s.IsArchived).ToList();

        var languages = activeSteps
            .Select(s => Resolve(path.Steps.First(x => x.StepId == s.StepId), ctx, effectiveAt).ContentLanguage)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var subjects = activeSteps
            .Select(s => Resolve(path.Steps.First(x => x.StepId == s.StepId), ctx, effectiveAt).ContentSubjectId)
            .Where(sub => sub is not null)
            .Distinct()
            .ToList();

        return new KnowledgePathDto(
            path.Id,
            path.PathCode,
            path.PathName,
            path.Description,
            path.SubjectId,
            path.TopicId,
            path.AudienceProfileId,
            path.Objective,
            path.LanguageCode,
            path.PathVersion,
            path.PathStatus,
            path.EffectiveFrom,
            path.EffectiveTo,
            path.Source,
            steps,
            activeSteps.Count,
            activeSteps.Count(s => s.IsRequired),
            IsMixedLanguage: languages.Count > 1,
            IsMixedSubject: subjects.Count > 1,
            HasUnresolvedStepContent:
                activeSteps.Any(s => s.ContentResolutionStatus == KnowledgePathContentResolutionStatuses.Unresolved),
            path.IsStepSetFrozen(),
            path.StepSetFrozenAt,
            path.PublishedAt,
            path.PublishedBy,
            path.SupersedesPathId,
            path.Version,
            path.CreatedAt,
            path.CreatedBy,
            path.UpdatedAt,
            path.UpdatedBy,
            path.ArchivedAt,
            path.ArchivedBy,
            path.IsArchived());
    }

    public static KnowledgePathListItemDto ToListItem(KnowledgePath path, ResolutionContext ctx, DateTimeOffset effectiveAt)
    {
        var active = path.Steps.Where(s => !s.IsArchived()).ToList();
        var hasUnresolved = active.Any(s =>
            Resolve(s, ctx, effectiveAt).Status == KnowledgePathContentResolutionStatuses.Unresolved);

        return new KnowledgePathListItemDto(
            path.Id,
            path.PathCode,
            path.PathName,
            path.SubjectId,
            path.TopicId,
            path.AudienceProfileId,
            path.LanguageCode,
            path.PathVersion,
            path.PathStatus,
            path.EffectiveFrom,
            path.EffectiveTo,
            path.Source,
            active.Count,
            active.Count(s => s.IsRequired),
            hasUnresolved,
            path.IsStepSetFrozen(),
            path.CreatedAt,
            path.CreatedBy,
            path.UpdatedAt,
            path.UpdatedBy,
            path.ArchivedAt,
            path.IsArchived());
    }
}
