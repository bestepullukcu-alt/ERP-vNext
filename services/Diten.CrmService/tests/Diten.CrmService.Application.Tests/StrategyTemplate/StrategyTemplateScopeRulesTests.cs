using Diten.CrmService.Application.Features.StrategyTemplate.Rules;
using Diten.CrmService.Domain.Entities;
using Xunit;
using TemplateEntity = Diten.CrmService.Domain.Entities.StrategyTemplate;

namespace Diten.CrmService.Application.Tests.StrategyTemplate;

/// <summary>
/// WP-ST-SCOPE — the pure scope rules: normalisation, the single-reference invariant, the pre-scope derivation and
/// Apply. No I/O, so every case is a straight input → output assertion. A deliberate mirror of the campaign's scope
/// rules; existence checks live in the write validator, not here.
/// </summary>
public sealed class StrategyTemplateScopeRulesTests
{
    [Fact]
    public void Tenant_scope_with_no_reference_is_accepted()
    {
        var (scope, failure) = StrategyTemplateScopeRules.Normalize(
            StrategyTemplateScopeTypes.Tenant, null, null, null);

        Assert.Null(failure);
        Assert.NotNull(scope);
        Assert.Equal(StrategyTemplateScopeTypes.Tenant, scope!.ScopeType);
        Assert.Null(scope.CountryScope);
        Assert.Null(scope.LegalEntityId);
        Assert.Null(scope.BusinessUnitId);
        Assert.Null(scope.ScopeRef);
    }

    [Fact]
    public void Tenant_scope_with_a_reference_is_ambiguous_not_silently_cleared()
    {
        var (scope, failure) = StrategyTemplateScopeRules.Normalize(
            StrategyTemplateScopeTypes.Tenant, "TR", null, null);

        Assert.Null(scope);
        Assert.Equal(StrategyTemplateErrorCodes.ScopeAmbiguous, failure!.ReasonCode);
        Assert.Equal(400, failure.StatusCode);
    }

    [Fact]
    public void Country_scope_is_upper_cased_and_carried_as_the_ref()
    {
        var (scope, failure) = StrategyTemplateScopeRules.Normalize(
            StrategyTemplateScopeTypes.Country, "tr", null, null);

        Assert.Null(failure);
        Assert.Equal("TR", scope!.CountryScope);
        Assert.Equal("TR", scope.ScopeRef);
    }

    [Fact]
    public void Country_scope_rejects_a_non_alpha2_code()
    {
        var (scope, failure) = StrategyTemplateScopeRules.Normalize(
            StrategyTemplateScopeTypes.Country, "TUR", null, null);

        Assert.Null(scope);
        Assert.Equal(StrategyTemplateErrorCodes.ScopeCountryInvalid, failure!.ReasonCode);
    }

    [Fact]
    public void Country_scope_with_a_second_reference_is_ambiguous()
    {
        var (scope, failure) = StrategyTemplateScopeRules.Normalize(
            StrategyTemplateScopeTypes.Country, "TR", null, "rx");

        Assert.Null(scope);
        Assert.Equal(StrategyTemplateErrorCodes.ScopeAmbiguous, failure!.ReasonCode);
    }

    [Fact]
    public void Legal_entity_scope_carries_the_id_as_the_ref()
    {
        var id = Guid.NewGuid();
        var (scope, failure) = StrategyTemplateScopeRules.Normalize(
            StrategyTemplateScopeTypes.LegalEntity, null, id, null);

        Assert.Null(failure);
        Assert.Equal(id, scope!.LegalEntityId);
        Assert.Equal(id.ToString("D"), scope.ScopeRef);
    }

    [Fact]
    public void Business_unit_scope_carries_the_trimmed_code()
    {
        var (scope, failure) = StrategyTemplateScopeRules.Normalize(
            StrategyTemplateScopeTypes.BusinessUnit, null, null, " rx ");

        Assert.Null(failure);
        Assert.Equal("rx", scope!.BusinessUnitId);
        Assert.Equal("rx", scope.ScopeRef);
    }

    [Theory]
    [InlineData(StrategyTemplateScopeTypes.Country)]
    [InlineData(StrategyTemplateScopeTypes.LegalEntity)]
    [InlineData(StrategyTemplateScopeTypes.BusinessUnit)]
    public void A_named_level_without_its_reference_is_a_reference_required(string scopeType)
    {
        var (scope, failure) = StrategyTemplateScopeRules.Normalize(scopeType, null, null, null);

        Assert.Null(scope);
        Assert.Equal(StrategyTemplateErrorCodes.ScopeReferenceRequired, failure!.ReasonCode);
    }

    [Fact]
    public void A_present_but_unknown_scope_type_is_refused()
    {
        var (scope, failure) = StrategyTemplateScopeRules.Normalize("region", "TR", null, null);

        Assert.Null(scope);
        Assert.Equal(StrategyTemplateErrorCodes.ScopeTypeUnknown, failure!.ReasonCode);
    }

    [Fact]
    public void Absent_scope_type_with_nothing_derives_tenant()
    {
        var (scope, failure) = StrategyTemplateScopeRules.Normalize(null, null, null, null);

        Assert.Null(failure);
        Assert.Equal(StrategyTemplateScopeTypes.Tenant, scope!.ScopeType);
    }

    [Fact]
    public void Absent_scope_type_with_only_a_business_unit_derives_business_unit()
    {
        var (scope, failure) = StrategyTemplateScopeRules.Normalize(null, null, null, "rx");

        Assert.Null(failure);
        Assert.Equal(StrategyTemplateScopeTypes.BusinessUnit, scope!.ScopeType);
        Assert.Equal("rx", scope.BusinessUnitId);
    }

    [Fact]
    public void Absent_scope_type_with_a_country_cannot_be_derived_and_is_refused()
    {
        // A country level did not exist before scope, so nothing legacy can be meaning it — guessing would invent intent.
        var (scope, failure) = StrategyTemplateScopeRules.Normalize(null, "TR", null, null);

        Assert.Null(scope);
        Assert.Equal(StrategyTemplateErrorCodes.ScopeTypeUnknown, failure!.ReasonCode);
    }

    [Fact]
    public void Apply_writes_the_whole_address_onto_the_play()
    {
        var (scope, _) = StrategyTemplateScopeRules.Normalize(StrategyTemplateScopeTypes.Country, "de", null, null);
        var template = new TemplateEntity { BusinessUnitId = "stale" };

        StrategyTemplateScopeRules.Apply(template, scope!);

        Assert.Equal(StrategyTemplateScopeTypes.Country, template.ScopeType);
        Assert.Equal("DE", template.CountryScope);
        Assert.Null(template.LegalEntityId);
        Assert.Null(template.BusinessUnitId);
    }

    [Fact]
    public void A_pre_scope_row_carrying_a_business_unit_reads_as_business_unit_scope()
    {
        var template = new TemplateEntity { ScopeType = string.Empty, BusinessUnitId = "rx" };

        Assert.Equal(StrategyTemplateScopeTypes.BusinessUnit, template.EffectiveScopeType());
        Assert.Equal("rx", template.ScopeRef());
        Assert.True(template.HasConsistentScope());
    }

    [Fact]
    public void A_pre_scope_row_with_nothing_reads_as_tenant_scope()
    {
        var template = new TemplateEntity { ScopeType = string.Empty };

        Assert.Equal(StrategyTemplateScopeTypes.Tenant, template.EffectiveScopeType());
        Assert.Null(template.ScopeRef());
        Assert.True(template.HasConsistentScope());
    }
}
