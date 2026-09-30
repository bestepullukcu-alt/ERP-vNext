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

/// <summary>WP-SB-1R — the ONE place a content set's context is resolved (set DTO, eligibility, revision freeze, the
/// SB-2 release producer). Read-only.</summary>
public interface IContentSetContextResolver
{
    Task<ContentSetContextDto> ResolveAsync(Guid tenantId, ContentSet set, CancellationToken cancellationToken);
}

/// <summary>WP-KP-1 — the set adapter over the shared <see cref="ChainContextResolver"/>: country + language are the set's
/// own, product + audience come from the one chain resolver (removed with the set in KP-4).</summary>
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

    public static ContentSetContextSnapshot ToSnapshot(ContentSetContextDto context) => new()
    {
        CountryCode = context.CountryCode,
        LanguageCode = context.LanguageCode,
        ProductId = context.ProductId,
        ProductCode = context.ProductCode,
        ProductName = context.ProductName,
        AudienceProfileIds = context.AudienceProfileIds.ToList()
    };
}

/// <summary>
/// WP-SB-1R — the set-only language rule. Country / language validation itself is the shared
/// <see cref="ChainContextValidation"/> (WP-KP-1).
/// </summary>
public static class ContentSetContextValidation
{
    /// <summary>Components not in <paramref name="language"/> (their pinned language), or empty.</summary>
    public static IReadOnlyList<ContentSetComponent> ComponentsNotIn(IEnumerable<ContentSetComponent> components, string language)
        => components.Where(c => !ChainContextValidation.SameLanguage(c.LanguageCode, language)).ToList();
}
