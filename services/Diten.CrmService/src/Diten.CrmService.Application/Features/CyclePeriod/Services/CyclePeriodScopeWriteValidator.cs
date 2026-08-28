using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.CyclePeriod.Contract;
using Diten.CrmService.Application.Features.CyclePeriod.Read;
using Diten.CrmService.Application.Features.CyclePeriod.Rules;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.CyclePeriod.Services;

/// <summary>
/// MOD-0165 FU07 — the write path's scope gate, in ONE place so create and draft-edit can never drift apart.
/// <para>It runs the pack's mandated order and stops at the first refusal:</para>
/// <list type="number">
/// <item><description>normalise + the single-reference invariant (pure — <see cref="CyclePeriodScopeRules"/>);</description></item>
/// <item><description>governed vocabulary: the country against <c>COUNTRY_CODES</c>, the business unit against the
/// same published <c>business-unit</c> set MOD-0151 Territory uses. An unpublished SET and an unknown VALUE are
/// reported as different failures, because one is fixed by an operator and the other by retyping;</description></item>
/// <item><description>the MDM legal entity, fail-closed.</description></item>
/// </list>
/// <para><b>Everything here happens BEFORE any insert or replace.</b> That ordering is the whole point: a dependency
/// outage must never be able to leave a half-authored period behind.</para>
/// <para>The Territory catalog is consulted LAST and only to STAMP provenance. It cannot refuse a write — a period has
/// to be authorable before its field plan exists, and pinning a period's identity to Territory's lifecycle would make
/// an existing period uneditable the day its plan is superseded.</para>
/// </summary>
public sealed class CyclePeriodScopeWriteValidator
{
    private readonly IReferenceDataValidator _references;
    private readonly ICyclePeriodLegalEntityValidator _legalEntities;
    private readonly ITerritoryBusinessUnitCatalog _territory;

    public CyclePeriodScopeWriteValidator(
        IReferenceDataValidator references,
        ICyclePeriodLegalEntityValidator legalEntities,
        ITerritoryBusinessUnitCatalog territory)
    {
        _references = references;
        _legalEntities = legalEntities;
        _territory = territory;
    }

    /// <summary>The accepted scope plus its provenance stamp, or the failure the handler answers with.</summary>
    public sealed record Result(
        CyclePeriodScopeRules.NormalizedScope? Scope,
        string? BusinessUnitSource,
        CyclePeriodValidation.Failure? Failure);

    public async Task<Result> ValidateAsync(
        string? scopeType,
        string? countryScope,
        Guid? legalEntityId,
        string? businessUnitId,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken)
    {
        var (scope, failure) = CyclePeriodScopeRules.Normalize(scopeType, countryScope, legalEntityId, businessUnitId);
        if (failure is not null || scope is null)
        {
            return new Result(null, null, failure);
        }

        if (scope.IsCountry)
        {
            var countryFailure = await ValidateReferenceAsync(
                CyclePeriodReferenceSets.CountrySet, scope.CountryScope!,
                CyclePeriodErrorCodes.CountryUnknown, "country", cancellationToken);
            if (countryFailure is not null)
            {
                return new Result(null, null, countryFailure);
            }
        }

        if (scope.IsBusinessUnit)
        {
            var businessUnitFailure = await ValidateReferenceAsync(
                CyclePeriodReferenceSets.BusinessUnitSet, scope.BusinessUnitId!,
                CyclePeriodErrorCodes.BusinessUnitUnknown, "business unit", cancellationToken);
            if (businessUnitFailure is not null)
            {
                return new Result(null, null, businessUnitFailure);
            }
        }

        if (scope.IsLegalEntity)
        {
            var verdict = await _legalEntities.ValidateAsync(scope.LegalEntityId!.Value, cancellationToken);
            if (verdict.DependencyUnavailable)
            {
                // 503, nothing written: we do not know, so we must not tell the author their input was wrong.
                return new Result(null, null, new CyclePeriodValidation.Failure(
                    "The legal entity could not be verified because the master-data service did not answer. "
                    + "Nothing was saved — please try again.",
                    CyclePeriodErrorCodes.LegalEntityDependencyUnavailable,
                    503));
            }

            if (!verdict.IsReferenceable)
            {
                return new Result(null, null, new CyclePeriodValidation.Failure(
                    "The legal entity does not exist, is not active, or may not be referenced.",
                    CyclePeriodErrorCodes.LegalEntityNotReferenceable));
            }
        }

        return new Result(scope, await StampAsync(scope, startDate, endDate, cancellationToken), null);
    }

    /// <summary>
    /// Where did this business-unit code come from — a matching territory plan, or the author's own knowledge? A stamp
    /// for the reader only: uniqueness, the overlap ban and the resolver all ignore it, so two periods carrying the
    /// same code are the same scope however each was authored.
    /// <para>A Territory read that fails is not a write failure. The stamp degrades to <c>manual</c>, which is the
    /// honest answer — we could not prove the plan covers it.</para>
    /// </summary>
    private async Task<string?> StampAsync(
        CyclePeriodScopeRules.NormalizedScope scope,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken)
    {
        if (!scope.IsBusinessUnit)
        {
            return null;
        }

        try
        {
            var candidates = await _territory.GetCandidatesAsync(null, startDate, endDate, cancellationToken);
            return candidates.Any(c => string.Equals(
                       c.BusinessUnitCode, scope.BusinessUnitId, StringComparison.OrdinalIgnoreCase))
                ? CyclePeriodBusinessUnitSources.Territory
                : CyclePeriodBusinessUnitSources.Manual;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return CyclePeriodBusinessUnitSources.Manual;
        }
    }

    private async Task<CyclePeriodValidation.Failure?> ValidateReferenceAsync(
        string setCode, string value, string unknownCode, string label, CancellationToken cancellationToken)
    {
        var result = await _references.ValidateAsync(setCode, value, cancellationToken);
        return result.Status switch
        {
            ReferenceValidationStatus.Valid => null,
            ReferenceValidationStatus.SetMissing => new CyclePeriodValidation.Failure(
                $"The governed reference set '{setCode}' is not published yet, so a {label} cannot be validated. "
                + "An operator must publish it before periods can be scoped this way.",
                CyclePeriodErrorCodes.ReferenceSetUnpublished),
            _ => new CyclePeriodValidation.Failure(
                $"'{value}' is not a published value of '{setCode}'.", unknownCode)
        };
    }
}
