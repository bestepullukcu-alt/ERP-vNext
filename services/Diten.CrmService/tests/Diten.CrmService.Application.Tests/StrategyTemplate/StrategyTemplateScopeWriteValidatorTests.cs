using Diten.CrmService.Application.Features.CyclePeriod.Services;
using Diten.CrmService.Application.Features.StrategyTemplate;
using Diten.CrmService.Application.Features.StrategyTemplate.Services;
using Diten.CrmService.Domain.Entities;
using Xunit;
using TemplateEntity = Diten.CrmService.Domain.Entities.StrategyTemplate;

namespace Diten.CrmService.Application.Tests.StrategyTemplate;

/// <summary>
/// WP-ST-SCOPE — the write path's scope gate: governed vocabulary (country + business unit) and the fail-closed MDM
/// legal-entity check. It reuses the campaign/cycle-period reference and legal-entity seams; these tests wire those seams
/// directly. The single-reference invariant itself is covered by the pure rules tests.
/// </summary>
public sealed class StrategyTemplateScopeWriteValidatorTests
{
    private readonly CampaignScopeTestDoubles.FakeReferenceValidator _references = new();
    private readonly CampaignScopeTestDoubles.FakeLegalEntityValidator _legalEntities = new();

    private StrategyTemplateScopeWriteValidator Gate() => new(_references, _legalEntities);

    [Fact]
    public async Task A_published_country_is_accepted()
    {
        _references.Publish(StrategyTemplateScopeReferenceSets.CountrySet, "TR", "DE");

        var result = await Gate().ValidateAsync(
            StrategyTemplateScopeTypes.Country, "TR", null, null, current: null, default);

        Assert.Null(result.Failure);
        Assert.Equal("TR", result.Scope!.CountryScope);
    }

    [Fact]
    public async Task An_unpublished_country_set_and_an_unknown_value_are_different_failures()
    {
        // Set never published -> operator's problem.
        var unpublished = await Gate().ValidateAsync(
            StrategyTemplateScopeTypes.Country, "TR", null, null, current: null, default);
        Assert.Equal(StrategyTemplateErrorCodes.ScopeReferenceSetUnpublished, unpublished.Failure!.ReasonCode);

        // Set published but value absent -> author's problem. A different code so each knows whose it is.
        _references.Publish(StrategyTemplateScopeReferenceSets.CountrySet, "DE");
        var unknown = await Gate().ValidateAsync(
            StrategyTemplateScopeTypes.Country, "TR", null, null, current: null, default);
        Assert.Equal(StrategyTemplateErrorCodes.ScopeCountryUnknown, unknown.Failure!.ReasonCode);

        Assert.NotEqual(unpublished.Failure.ReasonCode, unknown.Failure.ReasonCode);
    }

    [Fact]
    public async Task A_new_business_unit_is_validated_against_the_governed_set()
    {
        _references.Publish(StrategyTemplateScopeReferenceSets.BusinessUnitSet, "rx", "otc");

        var accepted = await Gate().ValidateAsync(
            StrategyTemplateScopeTypes.BusinessUnit, null, null, "rx", current: null, default);
        Assert.Null(accepted.Failure);

        var refused = await Gate().ValidateAsync(
            StrategyTemplateScopeTypes.BusinessUnit, null, null, "vaccines", current: null, default);
        Assert.Equal(StrategyTemplateErrorCodes.ScopeBusinessUnitUnknown, refused.Failure!.ReasonCode);
    }

    [Fact]
    public async Task An_unchanged_pre_scope_business_unit_is_not_re_validated_on_update()
    {
        // The play carries a code the governed set never had (it predates scope). Nothing is published.
        var current = new TemplateEntity { ScopeType = string.Empty, BusinessUnitId = "legacy-bu" };

        var result = await Gate().ValidateAsync(
            StrategyTemplateScopeTypes.BusinessUnit, null, null, "legacy-bu", current, default);

        // The reference did not change, so the governed check never ran — the play stays editable.
        Assert.Null(result.Failure);
        Assert.Equal(0, _references.Calls);
    }

    [Fact]
    public async Task A_changed_business_unit_on_update_IS_re_validated()
    {
        _references.Publish(StrategyTemplateScopeReferenceSets.BusinessUnitSet, "rx");
        var current = new TemplateEntity { ScopeType = string.Empty, BusinessUnitId = "legacy-bu" };

        var refused = await Gate().ValidateAsync(
            StrategyTemplateScopeTypes.BusinessUnit, null, null, "not-governed", current, default);

        Assert.Equal(StrategyTemplateErrorCodes.ScopeBusinessUnitUnknown, refused.Failure!.ReasonCode);
        Assert.Equal(1, _references.Calls);
    }

    [Fact]
    public async Task An_unreachable_legal_entity_check_is_a_503_with_no_verdict_on_the_input()
    {
        _legalEntities.Verdict = CyclePeriodLegalEntityValidation.Unavailable;

        var result = await Gate().ValidateAsync(
            StrategyTemplateScopeTypes.LegalEntity, null, Guid.NewGuid(), null, current: null, default);

        Assert.Equal(503, result.Failure!.StatusCode);
        Assert.Equal(StrategyTemplateErrorCodes.ScopeLegalEntityValidationUnavailable, result.Failure.ReasonCode);
    }

    [Fact]
    public async Task A_non_referenceable_legal_entity_is_a_400()
    {
        _legalEntities.Verdict = CyclePeriodLegalEntityValidation.NotReferenceable;

        var result = await Gate().ValidateAsync(
            StrategyTemplateScopeTypes.LegalEntity, null, Guid.NewGuid(), null, current: null, default);

        Assert.Equal(400, result.Failure!.StatusCode);
        Assert.Equal(StrategyTemplateErrorCodes.ScopeLegalEntityNotReferenceable, result.Failure.ReasonCode);
    }

    [Fact]
    public async Task A_referenceable_legal_entity_is_accepted()
    {
        _legalEntities.Verdict = CyclePeriodLegalEntityValidation.Valid;
        var id = Guid.NewGuid();

        var result = await Gate().ValidateAsync(
            StrategyTemplateScopeTypes.LegalEntity, null, id, null, current: null, default);

        Assert.Null(result.Failure);
        Assert.Equal(id, result.Scope!.LegalEntityId);
    }

    [Fact]
    public async Task A_tenant_scope_touches_no_governed_set()
    {
        var result = await Gate().ValidateAsync(
            StrategyTemplateScopeTypes.Tenant, null, null, null, current: null, default);

        Assert.Null(result.Failure);
        Assert.Equal(0, _references.Calls);
    }
}
