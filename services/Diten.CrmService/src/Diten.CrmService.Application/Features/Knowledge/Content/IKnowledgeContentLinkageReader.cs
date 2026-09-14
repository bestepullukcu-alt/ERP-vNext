using System.Text.Json.Serialization;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.Knowledge.Content;

/// <summary>
/// MOD-0162 FU02 — read-only content linkage seam. This is the contract a FUTURE consumer (MOD-0165 Campaign, later
/// MOD-0155) reads to answer "which published, effective content matches this subject/topic/audience/language/brand/
/// product/campaign context?". It returns a LIST and makes NO decision: no scoring, no "best content", no
/// recommendation, no visit/route plan. It never mutates Campaign (or anything else) — it only reads knowledge content.
/// </summary>
public interface IKnowledgeContentLinkageReader
{
    Task<IReadOnlyList<KnowledgeContentDto>> ResolvePublishedContentAsync(
        KnowledgeContentLinkageCriteria criteria, CancellationToken cancellationToken);

    /// <summary>SCMM-13 (docx §13) — resolve the variant of a logical component (<paramref name="contentSetId"/>) for a
    /// specific language. There is <b>NO silent fallback</b>: it returns the variant IN THAT LANGUAGE or an explicit
    /// <see cref="KnowledgeContentVariantResolutionStatus.Unresolved"/> — it never substitutes another language.</summary>
    Task<KnowledgeContentVariantResolution> ResolveVariantAsync(
        Guid contentSetId, string languageCode, CancellationToken cancellationToken);
}

/// <summary>Disjoint result of a language-variant resolution. Fail-closed: a missing language is a first-class
/// <c>Unresolved</c> outcome (with a reason), never a quiet fall-through to a different language.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum KnowledgeContentVariantResolutionStatus
{
    Resolved,
    Unresolved
}

/// <summary>The variant resolution outcome. On <c>Resolved</c>, <see cref="Content"/> is the matching-language variant;
/// on <c>Unresolved</c>, <see cref="Content"/> is null and <see cref="Reason"/> says why (no language substitution).</summary>
public sealed record KnowledgeContentVariantResolution(
    KnowledgeContentVariantResolutionStatus Status,
    KnowledgeContentDto? Content,
    string? Reason)
{
    public static KnowledgeContentVariantResolution Resolved(KnowledgeContentDto content)
        => new(KnowledgeContentVariantResolutionStatus.Resolved, content, null);

    public static KnowledgeContentVariantResolution Unresolved(string reason)
        => new(KnowledgeContentVariantResolutionStatus.Unresolved, null, reason);
}

/// <summary>Read criteria for the linkage seam. Every field is optional; an omitted field is not a filter. Only
/// published + effective content is ever returned.</summary>
public sealed record KnowledgeContentLinkageCriteria(
    Guid? SubjectId = null,
    Guid? TopicId = null,
    Guid? AudienceProfileId = null,
    string? LanguageCode = null,
    Guid? CampaignId = null,
    Guid? BrandId = null,
    Guid? ProductId = null,
    DateTimeOffset? EffectiveAt = null);

/// <summary>Default implementation over the content repository. Registered in Persistence DI. Applies the published +
/// effective gate in memory (the effective window is a DateTimeOffset BSON array, never a server-side key).</summary>
public sealed class KnowledgeContentLinkageReader : IKnowledgeContentLinkageReader
{
    private readonly ITenantContext _tenant;
    private readonly IKnowledgeContentRepository _repository;

    public KnowledgeContentLinkageReader(ITenantContext tenant, IKnowledgeContentRepository repository)
    {
        _tenant = tenant;
        _repository = repository;
    }

    public async Task<IReadOnlyList<KnowledgeContentDto>> ResolvePublishedContentAsync(
        KnowledgeContentLinkageCriteria criteria, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Array.Empty<KnowledgeContentDto>();
        }

        var at = criteria.EffectiveAt ?? DateTimeOffset.UtcNow;
        IEnumerable<Domain.Entities.KnowledgeContent> rows = await _repository.ListAsync(tenantId, cancellationToken);

        // Published + effective only — the seam never surfaces draft/inactive/archived content to a consumer.
        rows = rows.Where(c => c.IsConsumableAt(at));

        if (criteria.SubjectId is { } subjectId && subjectId != Guid.Empty)
        {
            rows = rows.Where(c => c.SubjectId == subjectId);
        }

        if (criteria.TopicId is { } topicId && topicId != Guid.Empty)
        {
            rows = rows.Where(c => c.TopicId == topicId);
        }

        if (criteria.AudienceProfileId is { } profileId && profileId != Guid.Empty)
        {
            rows = rows.Where(c => c.AudienceProfileId == profileId);
        }

        if (!string.IsNullOrWhiteSpace(criteria.LanguageCode))
        {
            var language = criteria.LanguageCode.Trim();
            rows = rows.Where(c => string.Equals(c.LanguageCode, language, StringComparison.OrdinalIgnoreCase));
        }

        if (criteria.CampaignId is { } campaignId && campaignId != Guid.Empty)
        {
            rows = rows.Where(c => c.CampaignId == campaignId);
        }

        if (criteria.BrandId is { } brandId && brandId != Guid.Empty)
        {
            rows = rows.Where(c => c.BrandId == brandId);
        }

        if (criteria.ProductId is { } productId && productId != Guid.Empty)
        {
            rows = rows.Where(c => c.ProductId == productId);
        }

        return rows.Select(KnowledgeMapper.ToDto).ToList();
    }

    public async Task<KnowledgeContentVariantResolution> ResolveVariantAsync(
        Guid contentSetId, string languageCode, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return KnowledgeContentVariantResolution.Unresolved("tenant-context-required");
        }

        if (contentSetId == Guid.Empty)
        {
            return KnowledgeContentVariantResolution.Unresolved("content-set-required");
        }

        if (string.IsNullOrWhiteSpace(languageCode))
        {
            return KnowledgeContentVariantResolution.Unresolved("language-required");
        }

        var language = languageCode.Trim();
        var rows = await _repository.ListAsync(tenantId, cancellationToken);

        // Exact language within the requested set only. A miss is an explicit Unresolved — NEVER another language.
        var match = rows.FirstOrDefault(c =>
            c.ContentSetId == contentSetId
            && !c.IsArchived()
            && string.Equals(c.LanguageCode, language, StringComparison.OrdinalIgnoreCase));

        return match is null
            ? KnowledgeContentVariantResolution.Unresolved("no-variant-for-language")
            : KnowledgeContentVariantResolution.Resolved(KnowledgeMapper.ToDto(match));
    }
}
