using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.ContentComposition.Claims;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.Knowledge.Chain;

/// <summary>WP-KP-1 — one "for whom" audience profile of a chain (id + its code / name for display and for the
/// eligibility audience dimension).</summary>
public sealed record ChainAudienceDto(Guid AudienceProfileId, string? ProfileCode, string? ProfileName);

/// <summary>
/// WP-KP-1 (moved from WP-SB-1R's set resolver) — what a chain template DERIVES for whatever is built on it (a knowledge
/// path, a content set): product = the template subject's primary MDM Global Product link, audience = the template's
/// "for whom" profiles. Never stored on the path / set.
/// </summary>
public sealed record ChainDerivedContextDto(
    Guid? ProductId,
    string? ProductCode,
    string? ProductName,
    IReadOnlyList<Guid> AudienceProfileIds,
    IReadOnlyList<ChainAudienceDto> Audiences)
{
    public static ChainDerivedContextDto Empty { get; } =
        new(null, null, null, Array.Empty<Guid>(), Array.Empty<ChainAudienceDto>());

    /// <summary>The single "for whom" profile, or null (none or several). Written to a path's AudienceProfileId so the
    /// existing audience filters keep working.</summary>
    public Guid? SingleAudienceProfileId => AudienceProfileIds.Count == 1 ? AudienceProfileIds[0] : null;
}

/// <summary>WP-KP-1 — the ONE place a chain's derived context is resolved (knowledge path, content set, the SB-2 release
/// producer). Read-only.</summary>
public interface IChainContextResolver
{
    Task<ChainDerivedContextDto> ResolveAsync(Guid tenantId, Guid chainTemplateId, CancellationToken cancellationToken);

    Task<ChainDerivedContextDto> ResolveAsync(
        Guid tenantId, ConceptChainTemplate? template, CancellationToken cancellationToken);
}

public sealed class ChainContextResolver : IChainContextResolver
{
    /// <summary>The Subject ExternalReference SourceSystem of an MDM Global Product link (taxonomy.js subject picker).</summary>
    public const string GlobalProductSourceSystem = "global-product";

    private readonly IConceptChainTemplateRepository _templates;
    private readonly ISubjectRepository _subjects;
    private readonly IAudienceProfileRepository _profiles;

    public ChainContextResolver(
        IConceptChainTemplateRepository templates, ISubjectRepository subjects, IAudienceProfileRepository profiles)
    {
        _templates = templates;
        _subjects = subjects;
        _profiles = profiles;
    }

    public async Task<ChainDerivedContextDto> ResolveAsync(
        Guid tenantId, Guid chainTemplateId, CancellationToken cancellationToken)
        => await ResolveAsync(tenantId, await _templates.GetByIdAsync(tenantId, chainTemplateId, cancellationToken),
            cancellationToken);

    public async Task<ChainDerivedContextDto> ResolveAsync(
        Guid tenantId, ConceptChainTemplate? template, CancellationToken cancellationToken)
    {
        var subject = template is null ? null : await _subjects.GetByIdAsync(tenantId, template.SubjectId, cancellationToken);
        var product = PrimaryGlobalProduct(subject);

        var audiences = new List<ChainAudienceDto>();
        foreach (var id in template?.ForWhomAudienceProfileIds.Distinct() ?? Enumerable.Empty<Guid>())
        {
            var profile = await _profiles.GetByIdAsync(tenantId, id, cancellationToken);
            audiences.Add(new ChainAudienceDto(id, profile?.ProfileCode, profile?.ProfileName));
        }

        return new ChainDerivedContextDto(
            product?.Id,
            product?.Reference.ExternalCode,
            product?.Reference.ExternalName,
            audiences.Select(a => a.AudienceProfileId).ToList(),
            audiences);
    }

    /// <summary>The subject's primary MDM Global Product link (SourceSystem <c>global-product</c>, IsPrimary, a Guid
    /// ExternalId), or null — the single definition of "the chain's product".</summary>
    public static (Guid Id, KnowledgeExternalReference Reference)? PrimaryGlobalProduct(Domain.Entities.Subject? subject)
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
}

/// <summary>
/// WP-KP-1 (moved from WP-SB-1R's set validation) — country / language validation against MOD-0048 BRD (never a local
/// list): the country must be an active <c>COUNTRY_CODES</c> value (400 <c>country_invalid</c>), the language one of that
/// country's <c>country-content-languages</c> (400 <c>language_not_in_country</c>); a set that cannot be read is 503
/// <c>reference_set_unavailable</c>.
/// </summary>
public static class ChainContextValidation
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
            return Fail(ChainContextErrors.CountryInvalid, "CountryCode is required.", 400);
        }

        if (languageCode.Length == 0)
        {
            return Fail(ChainContextErrors.LanguageNotInCountry, "LanguageCode is required.", 400);
        }

        var countries = await ReadAsync(catalog, ClaimReferenceSets.CountryCodes, cancellationToken);
        if (countries is null)
        {
            return Fail(ChainContextErrors.ReferenceSetUnavailable,
                $"Reference set '{ClaimReferenceSets.CountryCodes}' cannot be read; the country cannot be validated.", 503);
        }

        if (!countries.Values.Any(v => v.IsActive && !v.IsDeprecated
                && string.Equals(ClaimV2Checks.NormalizeCountry(v.ValueCode), countryCode, StringComparison.Ordinal)))
        {
            return Fail(ChainContextErrors.CountryInvalid,
                $"CountryCode '{countryCode}' is not an active value of '{ClaimReferenceSets.CountryCodes}'.", 400);
        }

        var languageSet = await ReadAsync(catalog, ClaimReferenceSets.CountryContentLanguages, cancellationToken);
        if (languageSet is null)
        {
            return Fail(ChainContextErrors.ReferenceSetUnavailable,
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
            return Fail(ChainContextErrors.LanguageNotInCountry,
                $"LanguageCode '{languageCode}' is not a content language of '{countryCode}'"
                + (allowed.Count == 0 ? " (the country has none configured)." : $" ({string.Join(", ", allowed)})."), 400);
        }

        return new Outcome(countryCode, languageCode, null, 0);
    }

    /// <summary>True when <paramref name="contentLanguage"/> is the single language <paramref name="language"/>.</summary>
    public static bool SameLanguage(string? contentLanguage, string? language)
        => string.Equals(
            ClaimV2Checks.NormalizeLanguage(contentLanguage), ClaimV2Checks.NormalizeLanguage(language),
            StringComparison.Ordinal);

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
