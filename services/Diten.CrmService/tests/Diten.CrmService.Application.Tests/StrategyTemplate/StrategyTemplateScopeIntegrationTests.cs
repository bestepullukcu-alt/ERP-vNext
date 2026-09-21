using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.CyclePeriod.Services;
using Diten.CrmService.Application.Features.StrategyTemplate;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Application.Features.StrategyTemplate.Commands;
using Diten.CrmService.Application.Features.StrategyTemplate.Handlers.CommandHandlers;
using Diten.CrmService.Application.Features.StrategyTemplate.Services;
using Diten.CrmService.Domain.Entities;
using Xunit;
using TemplateEntity = Diten.CrmService.Domain.Entities.StrategyTemplate;

namespace Diten.CrmService.Application.Tests.StrategyTemplate;

/// <summary>
/// WP-ST-SCOPE — create/update scope, end to end through the handlers. Proves the address is applied, the MDM
/// legal-entity check is fail-closed BEFORE persistence, and that scope is EDITABLE metadata: it may be corrected even on
/// a frozen (active) play, whose four binding lists cannot move.
/// </summary>
public sealed class StrategyTemplateScopeIntegrationTests
{
    private readonly FakeStrategyTemplateRepository _templates = new();
    private readonly FakeSegmentReadRepository _segments = new();
    private readonly FakeVisitFrequencyPolicyRepository _policies = new();
    private readonly FakeKnowledgePathRepository _paths = new();
    private readonly FakeContentEngagementJourneyRepository _journeys = new();
    private readonly FakeStrategyReferenceValidator _references = new();
    private readonly CampaignScopeTestDoubles.FakeReferenceValidator _refData = new();
    private readonly CampaignScopeTestDoubles.FakeLegalEntityValidator _legalEntities = new();

    private StrategyTemplateBindingValidator Bindings() => new(_segments, _policies, _paths, _journeys);
    private StrategyTemplateScopeWriteValidator Scope() => new(_refData, _legalEntities);

    private CreateStrategyTemplateHandler Create() => new(
        StrategyTemplateTestDoubles.Tenant(StrategyTemplateTestDoubles.TenantA),
        new NullActorContext(), _templates, Bindings(), _references, Scope());

    private UpdateStrategyTemplateHandler Update() => new(
        StrategyTemplateTestDoubles.Tenant(StrategyTemplateTestDoubles.TenantA),
        new NullActorContext(), _templates, Bindings(), _references, Scope());

    private Guid Segment() => _segments.Add(StrategyTemplateTestDoubles.TenantA).Id;

    [Fact]
    public async Task A_country_scoped_play_is_persisted_with_its_address()
    {
        _refData.Publish(StrategyTemplateScopeReferenceSets.CountrySet, "TR", "DE");

        var response = await Create().Handle(
            StrategyTemplateTestBuilders.NewTemplate(
                Segment(), scopeType: StrategyTemplateScopeTypes.Country, countryScope: "tr"),
            default);

        Assert.True(response.IsSuccessful);
        var stored = _templates.Stored(response.Data);
        Assert.Equal(StrategyTemplateScopeTypes.Country, stored.ScopeType);
        Assert.Equal("TR", stored.CountryScope);
        Assert.Equal("TR", stored.ScopeRef());
        Assert.Null(stored.BusinessUnitId);
    }

    [Fact]
    public async Task A_business_unit_scoped_play_validates_against_the_governed_set()
    {
        _refData.Publish(StrategyTemplateScopeReferenceSets.BusinessUnitSet, "rx", "otc");

        var response = await Create().Handle(
            StrategyTemplateTestBuilders.NewTemplate(
                Segment(), scopeType: StrategyTemplateScopeTypes.BusinessUnit, businessUnitId: "rx"),
            default);

        Assert.True(response.IsSuccessful);
        Assert.Equal(StrategyTemplateScopeTypes.BusinessUnit, _templates.Stored(response.Data).ScopeType);
    }

    [Fact]
    public async Task An_unreachable_legal_entity_check_on_create_is_a_503_with_nothing_persisted()
    {
        _legalEntities.Verdict = CyclePeriodLegalEntityValidation.Unavailable;

        var response = await Create().Handle(
            StrategyTemplateTestBuilders.NewTemplate(
                Segment(), scopeType: StrategyTemplateScopeTypes.LegalEntity, legalEntityId: Guid.NewGuid()),
            default);

        Assert.Equal(503, response.StatusCode);
        Assert.Contains(StrategyTemplateErrorCodes.ScopeLegalEntityValidationUnavailable, response.Errors!);
        Assert.Equal(0, _templates.InsertCalls);
        Assert.Empty(_templates.Rows);
    }

    [Fact]
    public async Task An_ambiguous_scope_on_create_is_a_400_with_nothing_persisted()
    {
        var response = await Create().Handle(
            StrategyTemplateTestBuilders.NewTemplate(
                Segment(), scopeType: StrategyTemplateScopeTypes.Country, countryScope: "TR",
                businessUnitId: "rx"),
            default);

        Assert.Equal(400, response.StatusCode);
        Assert.Contains(StrategyTemplateErrorCodes.ScopeAmbiguous, response.Errors!);
        Assert.Equal(0, _templates.InsertCalls);
    }

    [Fact]
    public async Task Scope_is_editable_on_a_FROZEN_active_play()
    {
        _refData.Publish(StrategyTemplateScopeReferenceSets.CountrySet, "TR");
        var id = SeedFrozenActivePlay();
        var stored = _templates.Stored(id);

        // Bindings are omitted (null = "leave alone"), so the freeze guard passes and only the address changes.
        var response = await Update().Handle(
            new UpdateStrategyTemplateCommand(
                id, stored.TemplateName, stored.EffectiveFrom, null, null, null, null,
                null, null, null, null, stored.Version,
                StrategyTemplateScopeTypes.Country, "TR", null),
            default);

        Assert.True(response.IsSuccessful);
        Assert.Equal(StrategyTemplateScopeTypes.Country, _templates.Stored(id).ScopeType);
        Assert.Equal("TR", _templates.Stored(id).CountryScope);
    }

    [Fact]
    public async Task A_binding_change_on_a_frozen_play_is_still_refused_while_scope_stays_editable()
    {
        var id = SeedFrozenActivePlay();
        var stored = _templates.Stored(id);

        var response = await Update().Handle(
            new UpdateStrategyTemplateCommand(
                id, stored.TemplateName, stored.EffectiveFrom, null, null, null, null,
                new[] { StrategyTemplateTestBuilders.Segment(Guid.NewGuid(), 20) }, null, null, null, stored.Version,
                null, null, null),
            default);

        Assert.Equal(409, response.StatusCode);
        Assert.Contains(StrategyTemplateErrorCodes.BindingsFrozen, response.Errors!);
    }

    [Fact]
    public async Task An_unchanged_pre_scope_business_unit_keeps_the_play_editable()
    {
        // Nothing is published, so any governed re-check would fail — but the reference did not change.
        var id = SeedDraftBusinessUnitPlay("legacy-bu");
        var stored = _templates.Stored(id);

        var response = await Update().Handle(
            new UpdateStrategyTemplateCommand(
                id, "Renamed", stored.EffectiveFrom, null, "legacy-bu", null, null,
                null, null, null, null, stored.Version,
                null, null, null),
            default);

        Assert.True(response.IsSuccessful);
        Assert.Equal("Renamed", _templates.Stored(id).TemplateName);
        Assert.Equal("legacy-bu", _templates.Stored(id).BusinessUnitId);
    }

    [Fact]
    public async Task A_changed_business_unit_on_update_is_validated()
    {
        _refData.Publish(StrategyTemplateScopeReferenceSets.BusinessUnitSet, "rx");
        var id = SeedDraftBusinessUnitPlay("legacy-bu");
        var stored = _templates.Stored(id);

        var response = await Update().Handle(
            new UpdateStrategyTemplateCommand(
                id, stored.TemplateName, stored.EffectiveFrom, null, "not-governed", null, null,
                null, null, null, null, stored.Version,
                null, null, null),
            default);

        Assert.Equal(400, response.StatusCode);
        Assert.Contains(StrategyTemplateErrorCodes.ScopeBusinessUnitUnknown, response.Errors!);
    }

    private Guid SeedFrozenActivePlay()
    {
        var id = Guid.NewGuid();
        _templates.Rows.Add(new TemplateEntity
        {
            Id = id,
            TenantId = StrategyTemplateTestDoubles.TenantA,
            TemplateCode = "frozen-play",
            TemplateName = "Frozen play",
            SubjectType = StrategyTemplateSubjectTypes.Contact,
            TemplateStatus = StrategyTemplateStatuses.Active,
            TemplateVersion = 1,
            VersionLineageId = id,
            SegmentBindings = new()
            {
                new StrategyTemplateSegmentBinding
                {
                    BindingId = Guid.NewGuid(),
                    SegmentId = Guid.NewGuid(),
                    BindingRole = StrategySegmentBindingRoles.Primary,
                    SortOrder = 10
                }
            },
            FrequencyIntent = new StrategyTemplateFrequencyIntent(),
            EffectiveFrom = StrategyTemplateTestDoubles.Past,
            BindingsFrozenAt = StrategyTemplateTestDoubles.Now,
            ActivatedAt = StrategyTemplateTestDoubles.Now,
            Version = 2,
            CreatedAt = StrategyTemplateTestDoubles.Past
        });
        return id;
    }

    private Guid SeedDraftBusinessUnitPlay(string businessUnitId)
    {
        var id = Guid.NewGuid();
        _templates.Rows.Add(new TemplateEntity
        {
            Id = id,
            TenantId = StrategyTemplateTestDoubles.TenantA,
            TemplateCode = "bu-play",
            TemplateName = "BU play",
            SubjectType = StrategyTemplateSubjectTypes.Contact,
            TemplateStatus = StrategyTemplateStatuses.Draft,
            TemplateVersion = 1,
            VersionLineageId = id,
            // Pre-scope: an opaque business-unit code and no ScopeType. EffectiveScopeType() reads this as business-unit.
            ScopeType = string.Empty,
            BusinessUnitId = businessUnitId,
            SegmentBindings = new()
            {
                new StrategyTemplateSegmentBinding
                {
                    BindingId = Guid.NewGuid(),
                    SegmentId = Guid.NewGuid(),
                    BindingRole = StrategySegmentBindingRoles.Primary,
                    SortOrder = 10
                }
            },
            FrequencyIntent = new StrategyTemplateFrequencyIntent(),
            EffectiveFrom = StrategyTemplateTestDoubles.Past,
            Version = 1,
            CreatedAt = StrategyTemplateTestDoubles.Past
        });
        return id;
    }
}
