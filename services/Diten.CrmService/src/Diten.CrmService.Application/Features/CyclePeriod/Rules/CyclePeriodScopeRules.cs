using System.Text.RegularExpressions;
using Diten.CrmService.Domain.Entities;
using PeriodEntity = Diten.CrmService.Domain.Entities.CyclePeriod;

namespace Diten.CrmService.Application.Features.CyclePeriod.Rules;

/// <summary>
/// MOD-0165 FU07 scope normalisation and the single-reference invariant, as PURE functions: no repository, no clock, no
/// I/O. Everything about "where does this period live?" that does not need another row is decided here, once.
/// <para><b>Discriminated, never combined.</b> A write names ONE <see cref="CyclePeriodScopeTypes"/> level and supplies
/// exactly the reference that level needs. Supplying a second reference is refused rather than ignored: silently
/// dropping a value the author typed would let them believe they created a period they did not create.</para>
/// <para><b>Normalisation is part of identity.</b> A country code is upper-cased, a business-unit code is trimmed, and
/// the scope key is built from the normalised values — otherwise "TR" and "tr" would become two calendars covering the
/// same days without ever tripping the overlap ban.</para>
/// <para>Reference EXISTENCE (is this a published country? a referenceable legal entity?) is deliberately NOT decided
/// here — that needs I/O and lives in the handler's validators, which run before any write.</para>
/// </summary>
public static class CyclePeriodScopeRules
{
    private static readonly Regex CountryPattern = new("^[A-Z]{2}$", RegexOptions.Compiled);

    /// <summary>A normalised, invariant-satisfying scope. <see cref="ScopeRef"/> is the second half of the identity key.
    /// </summary>
    public sealed record NormalizedScope(
        string ScopeType,
        string? CountryScope,
        Guid? LegalEntityId,
        string? BusinessUnitId,
        string? ScopeRef)
    {
        public bool IsCountry => string.Equals(ScopeType, CyclePeriodScopeTypes.Country, StringComparison.Ordinal);
        public bool IsLegalEntity => string.Equals(ScopeType, CyclePeriodScopeTypes.LegalEntity, StringComparison.Ordinal);
        public bool IsBusinessUnit => string.Equals(ScopeType, CyclePeriodScopeTypes.BusinessUnit, StringComparison.Ordinal);
    }

    public static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>ISO alpha-2, upper-cased. The code is the identity, so its casing cannot be the author's choice.</summary>
    public static string? NormalizeCountry(string? value) => Trim(value)?.ToUpperInvariant();

    /// <summary>
    /// FU06 backward compatibility — the scope a write MEANS when it names no <c>ScopeType</c> at all.
    /// <para>FU06 had no scope field: a period was tenant-wide, or it carried a business unit. Those two shapes are
    /// still unambiguous, so they are DERIVED rather than refused — an FU06 caller keeps working untouched:</para>
    /// <list type="bullet">
    /// <item><description>no country, no legal entity, no business unit → <c>tenant</c>;</description></item>
    /// <item><description>a business unit and nothing else → <c>business-unit</c>.</description></item>
    /// </list>
    /// <para>Returns <c>null</c> when a country or a legal entity is present without a <c>ScopeType</c>. Those levels
    /// did not exist in FU06, so nothing legacy can be meaning them, and guessing between "country" and something else
    /// would be inventing intent — the author must say which level they mean. Derivation is a compatibility bridge for
    /// shapes that were already unambiguous, never an inference engine for new ones.</para>
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

        return hasBusinessUnit ? CyclePeriodScopeTypes.BusinessUnit : CyclePeriodScopeTypes.Tenant;
    }

    /// <summary>The refusal for a write that omits <c>ScopeType</c> while naming a level FU06 never had.</summary>
    public static CyclePeriodValidation.Failure ScopeTypeRequired()
        => new(
            "ScopeType is required when a country or a legal entity is supplied. "
            + $"Known values: {string.Join(", ", CyclePeriodScopeTypes.All)}.",
            Contract.CyclePeriodErrorCodes.ScopeTypeUnknown);

    /// <summary>
    /// Normalises a write's scope and enforces the single-reference invariant. Returns the normalised scope, or the
    /// failure the handler must answer with (always 400: a malformed scope is a bad request, not a conflict).
    /// <para>An ABSENT <c>ScopeType</c> is derived (<see cref="DeriveScopeType"/>) rather than refused, so a caller
    /// written against FU06 — which had no such field — still works exactly as it did. An absent one that cannot be
    /// derived is still refused; a PRESENT but unknown one always was and still is.</para>
    /// </summary>
    public static (NormalizedScope? Scope, CyclePeriodValidation.Failure? Failure) Normalize(
        string? scopeType, string? countryScope, Guid? legalEntityId, string? businessUnitId)
    {
        var type = CyclePeriodScopeTypes.Normalize(scopeType);
        if (type.Length == 0)
        {
            // FU06 shape: no scope field at all. Derive it, or say plainly that this level needs to be named.
            var derived = DeriveScopeType(countryScope, legalEntityId, businessUnitId);
            if (derived is null)
            {
                return (null, ScopeTypeRequired());
            }

            type = derived;
        }

        if (!CyclePeriodScopeTypes.IsKnown(type))
        {
            return (null, new CyclePeriodValidation.Failure(
                $"Unknown ScopeType '{scopeType}'. Known values: {string.Join(", ", CyclePeriodScopeTypes.All)}.",
                Contract.CyclePeriodErrorCodes.ScopeTypeUnknown));
        }

        var country = NormalizeCountry(countryScope);
        var legalEntity = legalEntityId is { } id && id != Guid.Empty ? id : (Guid?)null;
        var businessUnit = Trim(businessUnitId);

        var supplied = (country is null ? 0 : 1) + (legalEntity is null ? 0 : 1) + (businessUnit is null ? 0 : 1);

        switch (type)
        {
            case CyclePeriodScopeTypes.Tenant:
                if (supplied != 0)
                {
                    // Not cleared silently: an author who filled a reference meant to scope the period somewhere.
                    return (null, ScopeMismatch("tenant scope takes no country, legal entity or business unit"));
                }

                return (new NormalizedScope(type, null, null, null, null), null);

            case CyclePeriodScopeTypes.Country:
                if (country is null)
                {
                    return (null, ScopeMissing("country"));
                }

                if (supplied != 1)
                {
                    return (null, ScopeMismatch("country scope takes a country only"));
                }

                if (country.Length != CyclePeriodLimits.CountryScopeLength || !CountryPattern.IsMatch(country))
                {
                    return (null, new CyclePeriodValidation.Failure(
                        "CountryScope must be an ISO alpha-2 code (two letters).",
                        Contract.CyclePeriodErrorCodes.CountryInvalid));
                }

                return (new NormalizedScope(type, country, null, null, country), null);

            case CyclePeriodScopeTypes.LegalEntity:
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

                if (businessUnit.Length > CyclePeriodLimits.MaxBusinessUnitIdLength)
                {
                    return (null, new CyclePeriodValidation.Failure(
                        $"BusinessUnitId must be at most {CyclePeriodLimits.MaxBusinessUnitIdLength} characters.",
                        Contract.CyclePeriodErrorCodes.BusinessUnitInvalid));
                }

                return (new NormalizedScope(type, null, null, businessUnit, businessUnit), null);
        }
    }

    /// <summary>
    /// The scope a FILTER or a RESOLVE argument names, normalised the same way a write is. A caller that passes
    /// nothing for a level means "skip that level" — which is what keeps an FU06-shaped call answering exactly as it
    /// did before FU07 existed.
    /// </summary>
    public static string? NormalizeScopeRefFor(string scopeType, string? country, Guid? legalEntityId, string? businessUnitId)
        => scopeType switch
        {
            CyclePeriodScopeTypes.Country => NormalizeCountry(country),
            CyclePeriodScopeTypes.LegalEntity => legalEntityId is { } id && id != Guid.Empty ? id.ToString("D") : null,
            CyclePeriodScopeTypes.BusinessUnit => Trim(businessUnitId),
            _ => null
        };

    /// <summary>Applies a normalised scope to an entity. One place, so create and draft-edit cannot drift.
    /// <para>Both trailing arguments are business-unit-only DOCUMENTATION and are cleared at every other level here,
    /// rather than in each handler: a rule written twice is a rule that eventually holds once.</para></summary>
    public static void Apply(
        PeriodEntity period,
        NormalizedScope scope,
        string? businessUnitSource,
        string? businessUnitCountryContext = null)
    {
        period.ScopeType = scope.ScopeType;
        period.CountryScope = scope.CountryScope;
        period.LegalEntityId = scope.LegalEntityId;
        period.BusinessUnitId = scope.BusinessUnitId;
        period.BusinessUnitSource = scope.IsBusinessUnit ? businessUnitSource : null;
        // Upper-cased like a country code so "tr / alpha" and "TR / alpha" cannot read as two different things. It is
        // NOT validated against the governed set: an informational field must never be able to refuse a write.
        period.BusinessUnitCountryContext = scope.IsBusinessUnit
            ? NormalizeCountry(businessUnitCountryContext)
            : null;
    }

    /// <summary>Does an entity already sit at this scope? Used by the immutability guard and the draft-edit path.</summary>
    public static bool SameScope(PeriodEntity period, NormalizedScope scope)
        => string.Equals(period.EffectiveScopeType(), scope.ScopeType, StringComparison.Ordinal)
           && CyclePeriodOverlapRules.SameScopeRef(period.ScopeRef(), scope.ScopeRef);

    /// <summary>A human-readable address for a refusal message ("business-unit:alpha", "tenant-wide").</summary>
    public static string Describe(string scopeType, string? scopeRef)
        => scopeRef is null ? scopeType : $"{scopeType}:{scopeRef}";

    private static CyclePeriodValidation.Failure ScopeMismatch(string expectation)
        => new(
            $"The scope references do not match ScopeType — {expectation}.",
            Contract.CyclePeriodErrorCodes.ScopeAmbiguous);

    private static CyclePeriodValidation.Failure ScopeMissing(string reference)
        => new(
            $"ScopeType requires a {reference}.",
            Contract.CyclePeriodErrorCodes.ScopeReferenceRequired);
}
