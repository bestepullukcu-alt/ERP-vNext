using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
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

public sealed class ContentSetContextResolver : IContentSetContextResolver
{
    /// <summary>The Subject ExternalReference SourceSystem of an MDM Global Product link (taxonomy.js subject picker).</summary>
    public const string GlobalProductSourceSystem = "global-product";

    private readonly IConceptChainTemplateRepository _templates;
    private readonly ISubjectRepository _subjects;
    private readonly IAudienceProfileRepository _profiles;

    public ContentSetContextResolver(
        IConceptChainTemplateRepository templates, ISubjectRepository subjects, IAudienceProfileRepository profiles)
    {
        _templates = templates;
        _subjects = subjects;
        _profiles = profiles;
    }

    public async Task<ContentSetContextDto> ResolveAsync(Guid tenantId, ContentSet set, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(set);

        var template = await _templates.GetByIdAsync(tenantId, set.Template.ConceptChainTemplateId, cancellationToken);
        var subject = template is null ? null : await _subjects.GetByIdAsync(tenantId, template.SubjectId, cancellationToken);
        var product = PrimaryGlobalProduct(subject);

        var audiences = new List<ContentSetAudienceDto>();
        foreach (var id in template?.ForWhomAudienceProfileIds.Distinct() ?? Enumerable.Empty<Guid>())
        {
            var profile = await _profiles.GetByIdAsync(tenantId, id, cancellationToken);
            audiences.Add(new ContentSetAudienceDto(id, profile?.ProfileCode, profile?.ProfileName));
        }

        return new ContentSetContextDto(
            set.CountryCode,
            set.LanguageCode,
            product?.Id,
            product?.Reference.ExternalCode,
            product?.Reference.ExternalName,
            audiences.Select(a => a.AudienceProfileId).ToList(),
            audiences);
    }

    /// <summary>The subject's primary MDM Global Product link (SourceSystem <c>global-product</c>, IsPrimary, a Guid
    /// ExternalId), or null. Shared with the SB-2 release producer — the single definition of "the chain's product".</summary>
    public static (Guid Id, KnowledgeExternalReference Reference)? PrimaryGlobalProduct(Subject? subject)
    {
        foreach (var reference in subject?.ExternalReferences ?? Enumerable.Empty<KnowledgeExternalReference>())
        {
            if (reference.IsPrimary
                && string.Equals(reference.SourceSystem?.Trim(), GlobalProductSourceSystem, StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(reference.ExternalId, out var id) && id != Guid.Empty)
            {
                return (id, reference);
            }
        }

        return null;
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
/// WP-SB-1R — country / language validation of a content set against MOD-0048 BRD (never a local list):
/// the country must be an active <c>COUNTRY_CODES</c> value (400 <c>country_invalid</c>), the language one of that
/// country's <c>country-content-languages</c> (400 <c>language_not_in_country</c>); a set that cannot be read is 503
/// <c>reference_set_unavailable</c>.
/// </summary>
public static class ContentSetContextValidation
{
    public sealed record Outcome(string? Country, string? Language, IReadOnlyList<string>? Errors, int StatusCode)
    {
        public bool IsValid => Errors is null;
    }

    public static async Task<Outcome> ValidateAsync(
        IReferenceDataCatalogReader? catalog, string? country, string? language, CancellationToken cancellationToken)
    {
        var countryCode = ClaimV2Checks.NormalizeCountry(country);
        var languageCode = ClaimV2Checks.NormalizeLanguage(language);
        if (countryCode.Length == 0)
        {
            return Fail(ContentSetContextErrors.CountryInvalid, "CountryCode is required.", 400);
        }

        if (languageCode.Length == 0)
        {
            return Fail(ContentSetContextErrors.LanguageNotInCountry, "LanguageCode is required.", 400);
        }

        var countries = await ReadAsync(catalog, ClaimReferenceSets.CountryCodes, cancellationToken);
        if (countries is null)
        {
            return Fail(ContentSetContextErrors.ReferenceSetUnavailable,
                $"Reference set '{ClaimReferenceSets.CountryCodes}' cannot be read; the country cannot be validated.", 503);
        }

        if (!countries.Values.Any(v => v.IsActive && !v.IsDeprecated
                && string.Equals(ClaimV2Checks.NormalizeCountry(v.ValueCode), countryCode, StringComparison.Ordinal)))
        {
            return Fail(ContentSetContextErrors.CountryInvalid,
                $"CountryCode '{countryCode}' is not an active value of '{ClaimReferenceSets.CountryCodes}'.", 400);
        }

        var languageSet = await ReadAsync(catalog, ClaimReferenceSets.CountryContentLanguages, cancellationToken);
        if (languageSet is null)
        {
            return Fail(ContentSetContextErrors.ReferenceSetUnavailable,
                $"Reference set '{ClaimReferenceSets.CountryContentLanguages}' cannot be read; the language cannot be validated.",
                503);
        }

        var row = languageSet.Values.FirstOrDefault(v => v.IsActive && !v.IsDeprecated
            && string.Equals(ClaimV2Checks.NormalizeCountry(v.ValueCode), countryCode, StringComparison.Ordinal));
        var allowed = row?.Attributes is { } attributes
                      && attributes.TryGetValue(ClaimReferenceSets.LanguagesAttribute, out var raw)
            ? raw.Split(new[] { ',', ';', ' ', '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(ClaimV2Checks.NormalizeLanguage).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        if (!allowed.Contains(languageCode))
        {
            return Fail(ContentSetContextErrors.LanguageNotInCountry,
                $"LanguageCode '{languageCode}' is not a content language of '{countryCode}'"
                + (allowed.Count == 0 ? " (the country has none configured)." : $" ({string.Join(", ", allowed)})."), 400);
        }

        return new Outcome(countryCode, languageCode, null, 0);
    }

    /// <summary>Components not in <paramref name="language"/> (their pinned language), or empty.</summary>
    public static IReadOnlyList<ContentSetComponent> ComponentsNotIn(IEnumerable<ContentSetComponent> components, string language)
        => components.Where(c => !string.Equals(
                ClaimV2Checks.NormalizeLanguage(c.LanguageCode), ClaimV2Checks.NormalizeLanguage(language), StringComparison.Ordinal))
            .ToList();

    private static async Task<ReferenceSetSnapshot?> ReadAsync(
        IReferenceDataCatalogReader? catalog, string setCode, CancellationToken cancellationToken)
    {
        if (catalog is null)
        {
            return null;
        }

        try
        {
            var set = await catalog.GetPublishedValuesAsync(setCode, cancellationToken);
            return set.IsPublished ? set : null;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private static Outcome Fail(string code, string message, int status) => new(null, null, new[] { code, message }, status);
}
