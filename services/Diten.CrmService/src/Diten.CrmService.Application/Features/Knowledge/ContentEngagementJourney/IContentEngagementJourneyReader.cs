using Diten.CrmService.Application.Common;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney;

/// <summary>Read-only filter for the consumption seam (§8.4).</summary>
public sealed record ContentEngagementJourneyCriteria(
    Guid? SubjectId = null,
    Guid? TopicId = null,
    Guid? AudienceProfileId = null,
    string? Language = null,
    DateTimeOffset? EffectiveAt = null);

/// <summary>
/// MOD-0162 FU05 read-only consumption seam (§8.4) that a future MOD-0155 / MOD-0309 consumer reads. It is NOT an
/// engine: it returns only <c>published</c> + effective journeys and their deterministically ordered
/// (StageOrder → StageCode) ACTIVE stages with the KnowledgePath resolved. It does NOT score, pick a "best" journey,
/// recommend, compute a current stage, advance a stage, evaluate a branch, assign a target or read/write completion.
/// When nothing matches it returns empty — no default is invented. draft / review / approved / inactive / archived
/// journeys never reach a consumer.
/// </summary>
public interface IContentEngagementJourneyReader
{
    Task<IReadOnlyList<ContentEngagementJourneyDto>> ResolvePublishedJourneysAsync(
        ContentEngagementJourneyCriteria criteria, CancellationToken cancellationToken);

    Task<IReadOnlyList<ContentEngagementJourneyStageDto>> GetOrderedStagesAsync(
        Guid journeyId, DateTimeOffset effectiveAt, CancellationToken cancellationToken);

    /// <summary>
    /// WP-VP-3C (K-7f) — the published, effective journeys that tell each of <paramref name="productIds"/>: the journeys
    /// whose Subject has the product as its PRIMARY MDM Global Product link (<c>ChainContextResolver.PrimaryGlobalProduct</c>,
    /// the single definition), narrowed by the criteria's language / audience. Every requested product is a key (an empty
    /// list when nothing tells it). The default answers nothing (a reader that cannot tell products apart); the production
    /// reader costs three reads for any number of products (subjects, journeys, paths).
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<ContentEngagementJourneyDto>>> ResolvePublishedJourneysForProductsAsync(
        IReadOnlyCollection<Guid> productIds, ContentEngagementJourneyCriteria criteria, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyDictionary<Guid, IReadOnlyList<ContentEngagementJourneyDto>>>(
            productIds.Distinct().ToDictionary(id => id, _ => (IReadOnlyList<ContentEngagementJourneyDto>)Array.Empty<ContentEngagementJourneyDto>()));

    /// <summary>WP-VP-3C (K-7f) — <see cref="ResolvePublishedJourneysForProductsAsync"/> for one product.</summary>
    async Task<IReadOnlyList<ContentEngagementJourneyDto>> ResolvePublishedJourneysForProductAsync(
        Guid productId, string? language, Guid? audienceProfileId, DateTimeOffset? effectiveAt, CancellationToken cancellationToken)
        => (await ResolvePublishedJourneysForProductsAsync(
                new[] { productId },
                new ContentEngagementJourneyCriteria(AudienceProfileId: audienceProfileId, Language: language, EffectiveAt: effectiveAt),
                cancellationToken))
            .TryGetValue(productId, out var rows) ? rows : Array.Empty<ContentEngagementJourneyDto>();
}

/// <summary>Default seam implementation. Read-only: it never mutates a journey, a stage or a FU04 KnowledgePath.</summary>
public sealed class ContentEngagementJourneyReader : IContentEngagementJourneyReader
{
    private readonly ITenantContext _tenant;
    private readonly IContentEngagementJourneyRepository _journeys;
    private readonly IKnowledgePathRepository _paths;
    private readonly ISubjectRepository? _subjects;

    public ContentEngagementJourneyReader(
        ITenantContext tenant, IContentEngagementJourneyRepository journeys, IKnowledgePathRepository paths,
        // WP-VP-3C — the product → subject link (optional: without it no journey is found by product).
        ISubjectRepository? subjects = null)
    {
        _subjects = subjects;
        _tenant = tenant;
        _journeys = journeys;
        _paths = paths;
    }

    public async Task<IReadOnlyList<ContentEngagementJourneyDto>> ResolvePublishedJourneysAsync(
        ContentEngagementJourneyCriteria criteria, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Array.Empty<ContentEngagementJourneyDto>();
        }

        var effectiveAt = criteria.EffectiveAt ?? DateTimeOffset.UtcNow;
        var rows = (await _journeys.ListAsync(tenantId, cancellationToken))
            .Where(j => !j.IsArchived() && j.IsPublished() && j.IsEffectiveAt(effectiveAt));

        if (criteria.SubjectId is { } subjectId && subjectId != Guid.Empty)
        {
            rows = rows.Where(j => j.SubjectId == subjectId);
        }

        if (criteria.TopicId is { } topicId && topicId != Guid.Empty)
        {
            rows = rows.Where(j => j.TopicId == topicId);
        }

        if (criteria.AudienceProfileId is { } profileId && profileId != Guid.Empty)
        {
            rows = rows.Where(j => j.AudienceProfileId == profileId);
        }

        if (!string.IsNullOrWhiteSpace(criteria.Language))
        {
            var lang = criteria.Language.Trim();
            rows = rows.Where(j => string.Equals(j.LanguageCode, lang, StringComparison.OrdinalIgnoreCase));
        }

        var list = rows.ToList();
        var ctx = new ContentEngagementJourneyMapper.ResolutionContext(
            await _paths.ListAsync(tenantId, cancellationToken));

        return list.Select(j => ContentEngagementJourneyMapper.ToDto(j, ctx, effectiveAt)).ToList();
    }

    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<ContentEngagementJourneyDto>>> ResolvePublishedJourneysForProductsAsync(
        IReadOnlyCollection<Guid> productIds, ContentEngagementJourneyCriteria criteria, CancellationToken cancellationToken)
    {
        var wanted = productIds.Where(id => id != Guid.Empty).Distinct().ToList();
        var result = wanted.ToDictionary(id => id, _ => (IReadOnlyList<ContentEngagementJourneyDto>)Array.Empty<ContentEngagementJourneyDto>());
        if (wanted.Count == 0 || _subjects is null || _tenant.TenantId is not { } tenantId)
        {
            return result;
        }

        // ONE subject read: subject → its primary product (the single definition), only for the products asked about.
        var productOfSubject = new Dictionary<Guid, Guid>();
        foreach (var subject in await _subjects.ListAsync(tenantId, cancellationToken))
        {
            if (subject.IsDeleted || subject.IsArchived())
            {
                continue;
            }

            if (Chain.ChainContextResolver.PrimaryGlobalProduct(subject) is { } product && result.ContainsKey(product.Id))
            {
                productOfSubject[subject.Id] = product.Id;
            }
        }

        if (productOfSubject.Count == 0)
        {
            return result;
        }

        // ONE journey read + ONE path read (the published / effective / language / audience rules of the existing read).
        var journeys = await ResolvePublishedJourneysAsync(criteria with { SubjectId = null, TopicId = null }, cancellationToken);
        foreach (var group in journeys
                     .Where(j => productOfSubject.ContainsKey(j.SubjectId))
                     .GroupBy(j => productOfSubject[j.SubjectId]))
        {
            result[group.Key] = group.OrderBy(j => j.JourneyCode, StringComparer.Ordinal).ToList();
        }

        return result;
    }

    public async Task<IReadOnlyList<ContentEngagementJourneyStageDto>> GetOrderedStagesAsync(
        Guid journeyId, DateTimeOffset effectiveAt, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Array.Empty<ContentEngagementJourneyStageDto>();
        }

        var journey = await _journeys.GetByIdAsync(tenantId, journeyId, cancellationToken);
        if (journey is null || journey.IsArchived() || !journey.IsPublished() || !journey.IsEffectiveAt(effectiveAt))
        {
            return Array.Empty<ContentEngagementJourneyStageDto>();
        }

        var ctx = new ContentEngagementJourneyMapper.ResolutionContext(
            await _paths.ListAsync(tenantId, cancellationToken));

        // Active stages only, deterministically ordered (StageOrder → StageCode).
        return ContentEngagementJourneyMapper.ToStageDtos(journey, ctx, effectiveAt, includeArchived: false);
    }
}
