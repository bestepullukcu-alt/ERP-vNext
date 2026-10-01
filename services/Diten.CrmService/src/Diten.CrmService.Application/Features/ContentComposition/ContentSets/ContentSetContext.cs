using Diten.CrmService.Application.Features.Knowledge.Chain;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.ContentComposition.ContentSets;

/// <summary>WP-SB-1R — one "for whom" audience profile of the set context (id + its code / name for display and for
/// the eligibility audience dimension).</summary>
public sealed record ContentSetAudienceDto(Guid AudienceProfileId, string? ProfileCode, string? ProfileName);

/// <summary>
/// WP-SB-1R (bridge-decision §7) — the context of a content set. Country + language are the set's own fields (chosen by
/// the author); product + audience are DERIVED from the pinned composition template and never stored on the set:
/// product = the template subject's primary MDM Global Product link, audience = the template's "for whom" profiles.
/// </summary>
public sealed record ContentSetContextDto(
    string? CountryCode,
    string? LanguageCode,
    Guid? ProductId,
    string? ProductCode,
    string? ProductName,
    IReadOnlyList<Guid> AudienceProfileIds,
    IReadOnlyList<ContentSetAudienceDto> Audiences);

/// <summary>WP-SB-1R — where a content set's context is resolved. WP-KP-4: only the obsolete set reads use it now.</summary>
public interface IContentSetContextResolver
{
    Task<ContentSetContextDto> ResolveAsync(Guid tenantId, ContentSet set, CancellationToken cancellationToken);
}

/// <summary>WP-KP-1 — the set adapter over the shared <see cref="ChainContextResolver"/>: country + language are the set's
/// own, product + audience come from the one chain resolver (kept for the read-only set reads, WP-KP-4).</summary>
public sealed class ContentSetContextResolver : IContentSetContextResolver
{
    private readonly IChainContextResolver _chain;

    public ContentSetContextResolver(
        IConceptChainTemplateRepository templates, ISubjectRepository subjects, IAudienceProfileRepository profiles)
        => _chain = new ChainContextResolver(templates, subjects, profiles);

    public async Task<ContentSetContextDto> ResolveAsync(Guid tenantId, ContentSet set, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(set);

        var derived = await _chain.ResolveAsync(tenantId, set.Template.ConceptChainTemplateId, cancellationToken);
        return new ContentSetContextDto(
            set.CountryCode,
            set.LanguageCode,
            derived.ProductId,
            derived.ProductCode,
            derived.ProductName,
            derived.AudienceProfileIds,
            derived.Audiences.Select(a => new ContentSetAudienceDto(a.AudienceProfileId, a.ProfileCode, a.ProfileName))
                .ToList());
    }
}
