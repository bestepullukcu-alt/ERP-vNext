using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Application.Features.CyclePeriod.Services;
using Diten.CrmService.Application.Features.StrategyTemplate.Rules;
using Diten.CrmService.Domain.Entities;
using TemplateEntity = Diten.CrmService.Domain.Entities.StrategyTemplate;

namespace Diten.CrmService.Application.Features.StrategyTemplate.Services;

/// <summary>
/// MOD-0167 FU04 (WP-ST-SCOPE) — the play write path's scope gate, in ONE place so create and update can never drift
/// apart.
///
/// <para>It runs the same order the campaign gate runs and stops at the first refusal:</para>
/// <list type="number">
/// <item><description>normalise + the single-reference invariant (pure — <see cref="StrategyTemplateScopeRules"/>);</description></item>
/// <item><description>governed vocabulary: the country against <c>COUNTRY_CODES</c>, the business unit against the same
/// published <c>business-unit</c> set MOD-0151 Territory uses. An unpublished SET and an unknown VALUE are reported as
/// different failures, because one is fixed by an operator and the other by retyping;</description></item>
/// <item><description>the MDM legal entity, fail-closed.</description></item>
/// </list>
///
/// <para><b>Everything here happens BEFORE any insert or replace.</b> A dependency outage must never be able to leave a
/// half-authored play behind.</para>
///
/// <para><b>The business-unit vocabulary check is conditional, and that is the point.</b> A play's scope is EDITABLE, so
/// this validator runs on every update — but before WP-ST-SCOPE the business unit was an opaque context string, and
/// existing plays may carry codes the governed set never had. Validating those on every write would make such a play
/// permanently uneditable: an author could not even fix a typo in its description. So the check runs only when the
/// reference actually CHANGES. Whoever touches the code has to make it valid; whoever does not, is not punished for
/// someone else's data.</para>
///
/// <para><b>It reuses the cycle period's MDM validator rather than cloning it.</b> That seam is read-only and answers
/// exactly one question — "may this legal entity be referenced?" — which is an MDM question, not a cycle-period one. A
/// second copy would mean two HTTP clients and two behaviours the day MDM is slow. The scope RULES are mirrored because
/// they may diverge; an outbound dependency window is not meaning.</para>
/// </summary>
public sealed class StrategyTemplateScopeWriteValidator
{
    private readonly IReferenceDataValidator _references;
    private readonly ICyclePeriodLegalEntityValidator _legalEntities;

    public StrategyTemplateScopeWriteValidator(
        IReferenceDataValidator references,
        ICyclePeriodLegalEntityValidator legalEntities)
    {
        _references = references;
        _legalEntities = legalEntities;
    }

    /// <summary>The accepted scope, or the failure the handler answers with.</summary>
    public sealed record Result(StrategyTemplateScopeRules.NormalizedScope? Scope, StrategyTemplateScopeRules.Failure? Failure);

    /// <param name="current">
    /// The play being updated, or <c>null</c> on create. Used ONLY to decide whether the business-unit reference
    /// changed — never to widen what is accepted.
    /// </param>
    public async Task<Result> ValidateAsync(
        string? scopeType,
        string? countryScope,
        Guid? legalEntityId,
        string? businessUnitId,
        TemplateEntity? current,
        CancellationToken cancellationToken)
    {
        var (scope, failure) = StrategyTemplateScopeRules.Normalize(
            scopeType, countryScope, legalEntityId, businessUnitId);
        if (failure is not null || scope is null)
        {
            return new Result(null, failure);
        }

        if (scope.IsCountry)
        {
            var countryFailure = await ValidateReferenceAsync(
                StrategyTemplateScopeReferenceSets.CountrySet, scope.CountryScope!,
                StrategyTemplateErrorCodes.ScopeCountryUnknown, "country", cancellationToken);
            if (countryFailure is not null)
            {
                return new Result(null, countryFailure);
            }
        }

        if (scope.IsBusinessUnit && BusinessUnitReferenceChanged(current, scope))
        {
            var businessUnitFailure = await ValidateReferenceAsync(
                StrategyTemplateScopeReferenceSets.BusinessUnitSet, scope.BusinessUnitId!,
                StrategyTemplateErrorCodes.ScopeBusinessUnitUnknown, "business unit", cancellationToken);
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
                return new Result(null, new StrategyTemplateScopeRules.Failure(
                    "The legal entity could not be verified because the master-data service did not answer. "
                    + "Nothing was saved — please try again.",
                    StrategyTemplateErrorCodes.ScopeLegalEntityValidationUnavailable,
                    503));
            }

            if (!verdict.IsReferenceable)
            {
                return new Result(null, new StrategyTemplateScopeRules.Failure(
                    "The legal entity does not exist, is not active, or may not be referenced.",
                    StrategyTemplateErrorCodes.ScopeLegalEntityNotReferenceable));
            }
        }

        return new Result(scope, null);
    }

    /// <summary>
    /// Did the author actually touch the business-unit reference? On create the answer is always yes. On update it is yes
    /// only when the normalised code differs from the stored one — which is what lets a pre-scope play carrying an
    /// ungoverned code keep being edited.
    /// </summary>
    private static bool BusinessUnitReferenceChanged(
        TemplateEntity? current, StrategyTemplateScopeRules.NormalizedScope scope)
    {
        if (current is null)
        {
            return true;
        }

        var stored = current.EffectiveScopeType() == StrategyTemplateScopeTypes.BusinessUnit
            ? StrategyTemplateScopeRules.Trim(current.BusinessUnitId)
            : null;

        return !StrategyTemplateScopeRules.SameScopeRef(stored, scope.BusinessUnitId);
    }

    private async Task<StrategyTemplateScopeRules.Failure?> ValidateReferenceAsync(
        string setCode, string value, string unknownCode, string label, CancellationToken cancellationToken)
    {
        var result = await _references.ValidateAsync(setCode, value, cancellationToken);
        return result.Status switch
        {
            ReferenceValidationStatus.Valid => null,
            ReferenceValidationStatus.SetMissing => new StrategyTemplateScopeRules.Failure(
                $"The governed reference set '{setCode}' is not published yet, so a {label} cannot be validated. "
                + "An operator must publish it before plays can be scoped this way.",
                StrategyTemplateErrorCodes.ScopeReferenceSetUnpublished),
            _ => new StrategyTemplateScopeRules.Failure(
                $"'{value}' is not a published {label} value in '{setCode}'.",
                unknownCode)
        };
    }
}
