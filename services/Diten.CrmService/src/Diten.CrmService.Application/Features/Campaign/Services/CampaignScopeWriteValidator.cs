using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.Campaign.Rules;
using Diten.CrmService.Application.Features.CyclePeriod.Services;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Campaign.Services;

/// <summary>
/// MOD-0165 FU09 — the campaign write path's scope gate, in ONE place so create and update can never drift apart.
///
/// <para>It runs the same order the cycle-period gate runs and stops at the first refusal:</para>
/// <list type="number">
/// <item><description>normalise + the single-reference invariant (pure — <see cref="CampaignScopeRules"/>);</description></item>
/// <item><description>governed vocabulary: the country against <c>COUNTRY_CODES</c>, the business unit against the
/// same published <c>business-unit</c> set MOD-0151 Territory uses. An unpublished SET and an unknown VALUE are
/// reported as different failures, because one is fixed by an operator and the other by retyping;</description></item>
/// <item><description>the MDM legal entity, fail-closed.</description></item>
/// </list>
///
/// <para><b>Everything here happens BEFORE any insert or replace.</b> A dependency outage must never be able to leave
/// a half-authored campaign behind.</para>
///
/// <para><b>The business-unit vocabulary check is conditional, and that is the point.</b> A campaign's scope is
/// EDITABLE, so this validator runs on every update — but before FU09 the business unit was an opaque context string,
/// and existing campaigns may carry codes the governed set never had. Validating those on every write would make such
/// a campaign permanently uneditable: an author could not even fix a typo in its description. So the check runs only
/// when the reference actually CHANGES. Whoever touches the code has to make it valid; whoever does not, is not
/// punished for someone else's data.</para>
///
/// <para><b>It reuses the cycle period's MDM validator rather than cloning it.</b> That seam is read-only and answers
/// exactly one question — "may this legal entity be referenced?" — which is not a cycle-period question, it is an MDM
/// question. A second copy would mean two HTTP clients, two timeout policies and two different behaviours the day MDM
/// is slow. The scope RULES are mirrored because they encode meaning that will diverge; an outbound dependency window
/// is not meaning.</para>
/// </summary>
public sealed class CampaignScopeWriteValidator
{
    private readonly IReferenceDataValidator _references;
    private readonly ICyclePeriodLegalEntityValidator _legalEntities;

    public CampaignScopeWriteValidator(
        IReferenceDataValidator references,
        ICyclePeriodLegalEntityValidator legalEntities)
    {
        _references = references;
        _legalEntities = legalEntities;
    }

    /// <summary>The accepted scope, or the failure the handler answers with.</summary>
    public sealed record Result(CampaignScopeRules.NormalizedScope? Scope, CampaignScopeRules.Failure? Failure);

    /// <param name="current">
    /// The campaign being updated, or <c>null</c> on create. Used ONLY to decide whether the business-unit reference
    /// changed — never to widen what is accepted.
    /// </param>
    public async Task<Result> ValidateAsync(
        string? scopeType,
        string? countryScope,
        Guid? legalEntityId,
        string? businessUnitId,
        Domain.Entities.Campaign? current,
        CancellationToken cancellationToken)
    {
        var (scope, failure) = CampaignScopeRules.Normalize(scopeType, countryScope, legalEntityId, businessUnitId);
        if (failure is not null || scope is null)
        {
            return new Result(null, failure);
        }

        if (scope.IsCountry)
        {
            var countryFailure = await ValidateReferenceAsync(
                CampaignScopeReferenceSets.CountrySet, scope.CountryScope!,
                CampaignReasonCodes.CampaignCountryUnknown, "country", cancellationToken);
            if (countryFailure is not null)
            {
                return new Result(null, countryFailure);
            }
        }

        if (scope.IsBusinessUnit && BusinessUnitReferenceChanged(current, scope))
        {
            var businessUnitFailure = await ValidateReferenceAsync(
                CampaignScopeReferenceSets.BusinessUnitSet, scope.BusinessUnitId!,
                CampaignReasonCodes.CampaignBusinessUnitUnknown, "business unit", cancellationToken);
            if (businessUnitFailure is not null)
            {
                return new Result(null, businessUnitFailure);
            }
        }

        if (scope.IsLegalEntity)
        {
            var verdict = await _legalEntities.ValidateAsync(scope.LegalEntityId!.Value, cancellationToken);
            if (verdict.DependencyUnavailable)
            {
                // 503, nothing written: we do not know, so we must not tell the author their input was wrong.
                return new Result(null, new CampaignScopeRules.Failure(
                    "The legal entity could not be verified because the master-data service did not answer. "
                    + "Nothing was saved — please try again.",
                    CampaignReasonCodes.CampaignLegalEntityValidationUnavailable,
                    503));
            }

            if (!verdict.IsReferenceable)
            {
                return new Result(null, new CampaignScopeRules.Failure(
                    "The legal entity does not exist, is not active, or may not be referenced.",
                    CampaignReasonCodes.CampaignLegalEntityNotReferenceable));
            }
        }

        return new Result(scope, null);
    }

    /// <summary>
    /// Did the author actually touch the business-unit reference? On create the answer is always yes. On update it is
    /// yes only when the normalised code differs from the stored one — which is what lets a pre-FU09 campaign carrying
    /// an ungoverned code keep being edited.
    /// </summary>
    private static bool BusinessUnitReferenceChanged(
        Domain.Entities.Campaign? current, CampaignScopeRules.NormalizedScope scope)
    {
        if (current is null)
        {
            return true;
        }

        var stored = current.EffectiveScopeType() == CampaignScopeTypes.BusinessUnit
            ? CampaignScopeRules.Trim(current.BusinessUnitId)
            : null;

        return !CampaignScopeRules.SameScopeRef(stored, scope.BusinessUnitId);
    }

    private async Task<CampaignScopeRules.Failure?> ValidateReferenceAsync(
        string setCode, string value, string unknownCode, string label, CancellationToken cancellationToken)
    {
        var result = await _references.ValidateAsync(setCode, value, cancellationToken);
        return result.Status switch
        {
            ReferenceValidationStatus.Valid => null,
            ReferenceValidationStatus.SetMissing => new CampaignScopeRules.Failure(
                $"The governed reference set '{setCode}' is not published yet, so a {label} cannot be validated. "
                + "An operator must publish it before campaigns can be scoped this way.",
                CampaignReasonCodes.CampaignReferenceSetUnpublished),
            _ => new CampaignScopeRules.Failure(
                $"'{value}' is not a published {label} value in '{setCode}'.",
                unknownCode)
        };
    }
}
