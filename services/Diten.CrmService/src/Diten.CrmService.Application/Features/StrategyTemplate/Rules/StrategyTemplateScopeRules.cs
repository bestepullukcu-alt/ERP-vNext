using System.Text.RegularExpressions;
using Diten.CrmService.Domain.Entities;
using TemplateEntity = Diten.CrmService.Domain.Entities.StrategyTemplate;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Rules;

/// <summary>
/// MOD-0167 FU04 (WP-ST-SCOPE) — play scope normalisation and the single-reference invariant, as PURE functions: no
/// repository, no clock, no I/O. Everything about "where does this play live?" that does not need another row is decided
/// here, once.
///
/// <para><b>A deliberate MIRROR of <c>CampaignScopeRules</c>, not a reuse of it.</b> The two rule sets currently agree,
/// but they are kept separate because the two scopes need not stay identical: a campaign's applicability rules and a
/// play's may diverge, and sharing one implementation would forbid a divergence before it is needed. What IS reused are
/// the outbound dependency SEAMs (the MDM legal-entity validator/catalog, the territory-BU catalog, the reference-data
/// validator/reader) — an outbound dependency window is not meaning, so cloning it would only buy two HTTP clients.</para>
///
/// <para><b>Discriminated, never combined.</b> A write names ONE <see cref="StrategyTemplateScopeTypes"/> level and
/// supplies exactly the reference that level needs. Supplying a second reference is refused rather than ignored: silently
/// dropping a value the author typed would let them believe they filed the play somewhere they did not.</para>
///
/// <para>Reference EXISTENCE (is this a published country? a referenceable legal entity?) is deliberately NOT decided
/// here — that needs I/O and lives in <c>StrategyTemplateScopeWriteValidator</c>, which runs before any write.</para>
/// </summary>
public static class StrategyTemplateScopeRules
{
    private static readonly Regex CountryPattern = new("^[A-Z]{2}$", RegexOptions.Compiled);

    /// <summary>A normalised, invariant-satisfying scope. <see cref="ScopeRef"/> is the address's second half.</summary>
    public sealed record NormalizedScope(
        string ScopeType,
        string? CountryScope,
        Guid? LegalEntityId,
        string? BusinessUnitId,
        string? ScopeRef)
    {
        public bool IsCountry => string.Equals(ScopeType, StrategyTemplateScopeTypes.Country, StringComparison.Ordinal);
        public bool IsLegalEntity => string.Equals(ScopeType, StrategyTemplateScopeTypes.LegalEntity, StringComparison.Ordinal);
        public bool IsBusinessUnit => string.Equals(ScopeType, StrategyTemplateScopeTypes.BusinessUnit, StringComparison.Ordinal);
    }

    /// <summary>A refusal: the message the author reads and the machine-readable code beside it.</summary>
    public sealed record Failure(string Error, string ReasonCode, int StatusCode = 400);

    public static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>ISO alpha-2, upper-cased. The code is the address, so its casing cannot be the author's choice.</summary>
    public static string? NormalizeCountry(string? value) => Trim(value)?.ToUpperInvariant();

    /// <summary>
    /// Pre-scope backward compatibility — the scope a write MEANS when it names no <c>ScopeType</c> at all.
    /// <para>Before WP-ST-SCOPE a play was tenant-wide, or it carried a business-unit context. Those two shapes are still
    /// unambiguous, so they are DERIVED rather than refused — a caller written against the earlier contract keeps working
    /// untouched:</para>
    /// <list type="bullet">
    /// <item><description>no country, no legal entity, no business unit → <c>tenant</c>;</description></item>
    /// <item><description>a business unit and nothing else → <c>business-unit</c>.</description></item>
    /// </list>
    /// <para>Returns <c>null</c> when a country or a legal entity is present without a <c>ScopeType</c>. Those levels did
    /// not exist before, so nothing legacy can be meaning them, and guessing would be inventing intent.</para>
    /// </summary>
    public static string? DeriveScopeType(string? countryScope, Guid? legalEntityId, string? businessUnitId)
    {
        var hasCountry = NormalizeCountry(countryScope) is not null;
        var hasLegalEntity = legalEntityId is { } id && id != Guid.Empty;
        var hasBusinessUnit = Trim(businessUnitId) is not null;

        if (hasCountry || hasLegalEntity)
        {
            return null;
        }

        return hasBusinessUnit ? StrategyTemplateScopeTypes.BusinessUnit : StrategyTemplateScopeTypes.Tenant;
    }

    /// <summary>The refusal for a write that omits <c>ScopeType</c> while naming a level that did not exist before.</summary>
    public static Failure ScopeTypeRequired()
        => new(
            "ScopeType is required when a country or a legal entity is supplied. "
            + $"Known values: {string.Join(", ", StrategyTemplateScopeTypes.All)}.",
            StrategyTemplateErrorCodes.ScopeTypeUnknown);

    /// <summary>
    /// Normalises a write's scope and enforces the single-reference invariant. Returns the normalised scope, or the
    /// failure the handler must answer with (always 400: a malformed scope is a bad request, not a conflict).
    /// <para>An ABSENT <c>ScopeType</c> is derived (<see cref="DeriveScopeType"/>) rather than refused, so a caller
    /// written against the pre-scope contract still works. An absent one that cannot be derived is refused; a PRESENT but
    /// unknown one always is.</para>
    /// </summary>
    public static (NormalizedScope? Scope, Failure? Failure) Normalize(
        string? scopeType, string? countryScope, Guid? legalEntityId, string? businessUnitId)
    {
        var type = StrategyTemplateScopeTypes.Normalize(scopeType);
        if (type.Length == 0)
        {
            var derived = DeriveScopeType(countryScope, legalEntityId, businessUnitId);
            if (derived is null)
            {
                return (null, ScopeTypeRequired());
            }

            type = derived;
        }

        if (!StrategyTemplateScopeTypes.IsKnown(type))
        {
            return (null, new Failure(
                $"Unknown ScopeType '{scopeType}'. Known values: {string.Join(", ", StrategyTemplateScopeTypes.All)}.",
                StrategyTemplateErrorCodes.ScopeTypeUnknown));
        }

        var country = NormalizeCountry(countryScope);
        var legalEntity = legalEntityId is { } id && id != Guid.Empty ? id : (Guid?)null;
        var businessUnit = Trim(businessUnitId);

        var supplied = (country is null ? 0 : 1) + (legalEntity is null ? 0 : 1) + (businessUnit is null ? 0 : 1);

        switch (type)
        {
            case StrategyTemplateScopeTypes.Tenant:
                if (supplied != 0)
                {
                    // Not cleared silently: an author who filled a reference meant to scope the play somewhere.
                    return (null, ScopeMismatch("tenant scope takes no country, legal entity or business unit"));
                }

                return (new NormalizedScope(type, null, null, null, null), null);

            case StrategyTemplateScopeTypes.Country:
                if (country is null)
                {
                    return (null, ScopeMissing("country"));
                }

                if (supplied != 1)
                {
                    return (null, ScopeMismatch("country scope takes a country only"));
                }

                if (country.Length != StrategyTemplateScopeLimits.CountryScopeLength || !CountryPattern.IsMatch(country))
                {
                    return (null, new Failure(
                        "CountryScope must be an ISO alpha-2 code (two letters).",
                        StrategyTemplateErrorCodes.ScopeCountryInvalid));
                }

                return (new NormalizedScope(type, country, null, null, country), null);

            case StrategyTemplateScopeTypes.LegalEntity:
                if (legalEntity is null)
                {
                    return (null, ScopeMissing("legal entity"));
                }

                if (supplied != 1)
                {
                    return (null, ScopeMismatch("legal-entity scope takes a legal entity only"));
                }

                return (new NormalizedScope(type, null, legalEntity, null, legalEntity.Value.ToString("D")), null);

            default:
                if (businessUnit is null)
                {
                    return (null, ScopeMissing("business unit"));
                }

                if (supplied != 1)
                {
                    return (null, ScopeMismatch("business-unit scope takes a business unit only"));
                }

                if (businessUnit.Length > StrategyTemplateScopeLimits.MaxBusinessUnitIdLength)
                {
                    return (null, new Failure(
                        $"BusinessUnitId must be at most {StrategyTemplateScopeLimits.MaxBusinessUnitIdLength} characters.",
                        StrategyTemplateErrorCodes.ScopeBusinessUnitUnknown));
                }

                return (new NormalizedScope(type, null, null, businessUnit, businessUnit), null);
        }
    }

    /// <summary>Applies a normalised scope to a play. One place, so create and update cannot drift.</summary>
    public static void Apply(TemplateEntity template, NormalizedScope scope)
    {
        template.ScopeType = scope.ScopeType;
        template.CountryScope = scope.CountryScope;
        template.LegalEntityId = scope.LegalEntityId;
        template.BusinessUnitId = scope.BusinessUnitId;
    }

    /// <summary>Two scope references, compared the way the address key is built (trimmed, case-insensitive).</summary>
    public static bool SameScopeRef(string? left, string? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        return left is not null && right is not null
            && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Does a play already sit at this address? Drives the "did the reference change?" question.</summary>
    public static bool SameScope(TemplateEntity template, NormalizedScope scope)
        => string.Equals(template.EffectiveScopeType(), scope.ScopeType, StringComparison.Ordinal)
           && SameScopeRef(template.ScopeRef(), scope.ScopeRef);

    /// <summary>A human-readable address for a refusal message ("business-unit:alpha", "tenant").</summary>
    public static string Describe(string scopeType, string? scopeRef)
        => scopeRef is null ? scopeType : $"{scopeType}:{scopeRef}";

    private static Failure ScopeMismatch(string expectation)
        => new(
            $"The scope references do not match ScopeType — {expectation}.",
            StrategyTemplateErrorCodes.ScopeAmbiguous);

    private static Failure ScopeMissing(string reference)
        => new(
            $"ScopeType requires a {reference}.",
            StrategyTemplateErrorCodes.ScopeReferenceRequired);
}
