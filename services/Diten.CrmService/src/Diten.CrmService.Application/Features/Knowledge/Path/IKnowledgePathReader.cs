using Diten.CrmService.Application.Common;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.Knowledge.Path;

/// <summary>Read-only filter for the consumption seam (§8.4).</summary>
public sealed record KnowledgePathCriteria(
    Guid? SubjectId = null,
    Guid? TopicId = null,
    Guid? AudienceProfileId = null,
    string? Language = null,
    DateTimeOffset? EffectiveAt = null);

/// <summary>
/// MOD-0162 FU04 read-only consumption seam (§8.4) that a future MOD-0155 / MOD-0309 consumer reads. It is NOT an
/// engine: it returns only <c>published</c> + effective paths and their deterministically ordered (StepOrder → StepCode)
/// ACTIVE steps with content resolved. It does NOT score, pick a "best" path, recommend, evaluate a branch, or read/write
/// completion. When nothing matches it returns empty — no default is invented. draft / review / approved / inactive /
/// archived paths never reach a consumer.
/// </summary>
public interface IKnowledgePathReader
{
    Task<IReadOnlyList<KnowledgePathDto>> ResolvePublishedPathsAsync(
        KnowledgePathCriteria criteria, CancellationToken cancellationToken);

    Task<IReadOnlyList<KnowledgePathStepDto>> GetOrderedStepsAsync(
        Guid pathId, DateTimeOffset effectiveAt, CancellationToken cancellationToken);
}

/// <summary>Default seam implementation. Read-only: it never mutates a path, a step, content or a concept node.</summary>
public sealed class KnowledgePathReader : IKnowledgePathReader
{
    private readonly ITenantContext _tenant;
    private readonly IKnowledgePathRepository _paths;
    private readonly IKnowledgeContentRepository _contents;
    private readonly IConceptNodeRepository _nodes;

    public KnowledgePathReader(
        ITenantContext tenant, IKnowledgePathRepository paths, IKnowledgeContentRepository contents,
        IConceptNodeRepository nodes)
    {
        _tenant = tenant;
        _paths = paths;
        _contents = contents;
        _nodes = nodes;
    }

    public async Task<IReadOnlyList<KnowledgePathDto>> ResolvePublishedPathsAsync(
        KnowledgePathCriteria criteria, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Array.Empty<KnowledgePathDto>();
        }

        var effectiveAt = criteria.EffectiveAt ?? DateTimeOffset.UtcNow;
        var rows = (await _paths.ListAsync(tenantId, cancellationToken))
            .Where(p => !p.IsArchived() && p.IsPublished() && p.IsEffectiveAt(effectiveAt));

        if (criteria.SubjectId is { } subjectId && subjectId != Guid.Empty)
        {
            rows = rows.Where(p => p.SubjectId == subjectId);
        }

        if (criteria.TopicId is { } topicId && topicId != Guid.Empty)
        {
            rows = rows.Where(p => p.TopicId == topicId);
        }

        if (criteria.AudienceProfileId is { } profileId && profileId != Guid.Empty)
        {
            rows = rows.Where(p => p.AudienceProfileId == profileId);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Language))
        {
            var lang = criteria.Language.Trim();
            rows = rows.Where(p => string.Equals(p.LanguageCode, lang, StringComparison.OrdinalIgnoreCase));
        }

        var list = rows.ToList();
        var ctx = new KnowledgePathMapper.ResolutionContext(
            await _contents.ListAsync(tenantId, cancellationToken),
            await _nodes.ListAsync(tenantId, cancellationToken));

        return list.Select(p => KnowledgePathMapper.ToDto(p, ctx, effectiveAt)).ToList();
    }

    public async Task<IReadOnlyList<KnowledgePathStepDto>> GetOrderedStepsAsync(
        Guid pathId, DateTimeOffset effectiveAt, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Array.Empty<KnowledgePathStepDto>();
        }

        var path = await _paths.GetByIdAsync(tenantId, pathId, cancellationToken);
        if (path is null || path.IsArchived() || !path.IsPublished() || !path.IsEffectiveAt(effectiveAt))
        {
            return Array.Empty<KnowledgePathStepDto>();
        }

        var ctx = new KnowledgePathMapper.ResolutionContext(
            await _contents.ListAsync(tenantId, cancellationToken),
            await _nodes.ListAsync(tenantId, cancellationToken));

        // Active steps only, deterministically ordered (StepOrder → StepCode).
        return KnowledgePathMapper.ToStepDtos(path, ctx, effectiveAt, includeArchived: false);
    }
}
