using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.CycleCapacity.Services;

/// <summary>
/// MOD-0155 FU06 — <b>D-COUNTRY = B</b>, in one place: which country does the working calendar get asked about, and
/// which optional narrowing may be passed with it?
/// <para>The tension this resolves is real. <c>CyclePeriod.ScopeType</c> is DISCRIMINATED — a period lives at exactly
/// one of tenant / country / legal-entity / business-unit — while the working calendar ALWAYS needs a country code.
/// Only a country-scoped period can name one; a tenant-scoped period (the common default) has none to derive.</para>
/// <para>So the capacity carries a <c>CalendarCountryCode</c>. It is a <b>calendar query parameter, not a scope</b>:
/// it takes no part in the aggregate's identity, its uniqueness or any precedence. When the period IS country-scoped
/// the value is DERIVED from it and the caller's own value is ignored, so the two can never disagree.</para>
/// <para><b>Pure.</b> No I/O of any kind — the governed-vocabulary check on an authored code is a separate,
/// reference-data concern and lives in the handler.</para>
/// </summary>
public interface ICycleCapacityCountryResolver
{
    CycleCapacityCountryResolution Resolve(CyclePeriodSnapshot period, string? authoredCountryCode);
}

/// <summary>
/// What the calendar should be asked. <see cref="CountryCode"/> is null only when nothing could be derived and nothing
/// was authored — a refusal, not a default.
/// </summary>
/// <param name="CountryCode">Upper-cased ISO alpha-2, or null when unresolvable.</param>
/// <param name="IsDerived">
/// True when the code came from the period's own country scope. The UI renders the field read-only in that case, and
/// the handler skips the governed-vocabulary check because the period already passed it.
/// </param>
/// <param name="LegalEntityId">
/// Passed straight through to the working calendar's optional <c>legalEntityId</c> narrowing when the period is
/// legal-entity scoped — free precision that costs no extra call. Null otherwise.
/// </param>
/// <param name="Failure">Set when the country could not be established.</param>
public sealed record CycleCapacityCountryResolution(
    string? CountryCode,
    bool IsDerived,
    Guid? LegalEntityId,
    CycleCapacityValidation.Failure? Failure);

/// <summary>
/// The single implementation. It exists as a registered service rather than a static helper so the handlers depend on
/// the decision rather than on a call site, but it holds no state and reaches nothing.
/// </summary>
public sealed class CycleCapacityCountryResolver : ICycleCapacityCountryResolver
{
    public CycleCapacityCountryResolution Resolve(CyclePeriodSnapshot period, string? authoredCountryCode)
    {
        var legalEntityId = string.Equals(period.ScopeType, CyclePeriodScopeTypes.LegalEntity, StringComparison.Ordinal)
                            && period.LegalEntityId is { } id
                            && id != Guid.Empty
            ? id
            : (Guid?)null;

        // A country-scoped period already names the country, and it was validated against the governed set when the
        // period was written. Deriving it here makes the two impossible to contradict.
        if (string.Equals(period.ScopeType, CyclePeriodScopeTypes.Country, StringComparison.Ordinal)
            && Normalize(period.CountryScope) is { } derived)
        {
            return new CycleCapacityCountryResolution(derived, IsDerived: true, legalEntityId, Failure: null);
        }

        // NOTE — the business unit is deliberately NOT mapped onto the calendar's organizationUnitId.
        // CyclePeriod.BusinessUnitId is a published MOD-0048 VALUE CODE (a string); the calendar's organizationUnitId is
        // an organization-unit GUID. They are different things, and coercing one into the other would silently select
        // the wrong calendar. A business-unit-scoped period therefore does not narrow the calendar (F-WC-ORG-UNIT).
        // BusinessUnitCountryContext is not used either: its own contract calls it "documentation, never identity",
        // and it is null on rows written before it existed — deriving a calendar from it would be a guess.
        if (Normalize(authoredCountryCode) is { } authored)
        {
            return new CycleCapacityCountryResolution(authored, IsDerived: false, legalEntityId, Failure: null);
        }

        return new CycleCapacityCountryResolution(
            null,
            IsDerived: false,
            legalEntityId,
            new CycleCapacityValidation.Failure(
                "A calendar country is required: this cycle period is not country-scoped, so no country can be derived "
                + "from it and one must be chosen. It is used only to read the working calendar — it does not change "
                + "the period's scope.",
                CycleCapacityReasonCodes.CountryRequired));
    }

    private static string? Normalize(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
}
