using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney;

/// <summary>
/// MOD-0162 FU05 read-only KnowledgePath access (§8.3 / §2.2/AC-FU04-2). This is the ONLY place FU05 touches FU04, and
/// it touches it through <see cref="IKnowledgePathRepository"/> READS alone: no FU04 aggregate is created, updated,
/// archived or published here, and the FU04 consumption seam (<c>IKnowledgePathReader</c> / <c>KnowledgePathCriteria</c>)
/// is NOT widened — FU05 needs a by-code lookup that the seam deliberately does not offer, so it does that lookup
/// itself instead of changing a shipped signature. Nothing about the path's steps is copied.
/// </summary>
public sealed class ContentEngagementJourneyPathResolver
{
    private readonly IKnowledgePathRepository _paths;

    public ContentEngagementJourneyPathResolver(IKnowledgePathRepository paths) => _paths = paths;

    /// <summary>All tenant paths, wrapped as the mapper's resolution context (by id + by code).</summary>
    public async Task<ContentEngagementJourneyMapper.ResolutionContext> BuildContextAsync(
        Guid tenantId, CancellationToken cancellationToken)
    {
        var paths = await _paths.ListAsync(tenantId, cancellationToken);
        return new ContentEngagementJourneyMapper.ResolutionContext(paths);
    }

    /// <summary>Single path read for the write-side guard (V-S05/V-S06). Returns null when the id does not exist for
    /// this tenant — the caller turns that into a 400, never into a silent skip.</summary>
    public Task<KnowledgePath?> GetPathAsync(Guid tenantId, Guid pathId, CancellationToken cancellationToken)
        => _paths.GetByIdAsync(tenantId, pathId, cancellationToken);

    /// <summary>V-S05/V-S06: a stage may only bind to a path that is non-archived, <c>published</c> and effective at
    /// <paramref name="effectiveAt"/>. Returns the 400 message or null.</summary>
    public static string? ValidateBindablePath(KnowledgePath? path, DateTimeOffset effectiveAt)
    {
        if (path is null)
        {
            return "RecommendedKnowledgePathId must reference a KnowledgePath of this tenant.";
        }

        if (path.IsArchived())
        {
            return "RecommendedKnowledgePathId references an archived KnowledgePath; " +
                   "an archived path cannot be bound to a stage.";
        }

        if (!path.IsPublished())
        {
            return "RecommendedKnowledgePathId must reference a PUBLISHED KnowledgePath " +
                   $"(current status: {path.PathStatus}).";
        }

        return path.IsEffectiveAt(effectiveAt)
            ? null
            : "RecommendedKnowledgePathId must reference a KnowledgePath that is effective now.";
    }
}
