using Diten.CrmService.Application.Features.Segmentation;
using Diten.CrmService.Application.Features.Segmentation.Catalog;
using Diten.CrmService.Application.Features.Segmentation.Contract;
using Diten.CrmService.Application.Features.Segmentation.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.Segmentation.Queries;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.Segmentation;

/// <summary>
/// MOD-0167 FU02 — the attribute catalog is CLOSED and it is published exactly as it is enforced. If these two ever
/// diverge, a UI can offer a rule the runtime refuses, which is the failure a declared catalog exists to prevent.
/// </summary>
public sealed class SegmentAttributeCatalogTests
{
    private static GetSegmentAttributeCatalogHandler Handler()
        => new(SegmentTestDoubles.Tenant(SegmentTestDoubles.TenantA));

    [Fact]
    public void An_undeclared_code_is_not_found_and_the_catalog_is_not_extensible_at_runtime()
    {
        Assert.Null(SegmentAttributeCatalog.Find("contact.favourite-colour"));
        Assert.False(SegmentAttributeCatalog.IsDeclared("segment.of.segment"));
        Assert.False(SegmentAttributeCatalog.IsDeclared(null));
        Assert.False(SegmentAttributeCatalog.IsDeclared("  "));
    }

    [Fact]
    public void Every_declared_attribute_carries_a_class_a_value_type_and_at_least_one_operator()
    {
        Assert.All(SegmentAttributeCatalog.All, a =>
        {
            Assert.Contains(a.AttributeClass, new[]
            {
                SegmentAttributeCatalog.ClassNative,
                SegmentAttributeCatalog.ClassJoin,
                SegmentAttributeCatalog.ClassDerived
            });
            Assert.True(SegmentValueTypes.IsValid(a.ValueType));
            Assert.NotEmpty(a.Operators);
            Assert.All(a.Operators, op => Assert.True(SegmentOperators.IsValid(op)));
            Assert.NotEmpty(a.AllowedSubjectTypes);
            Assert.All(a.AllowedSubjectTypes, s => Assert.True(SegmentSubjectTypes.IsValid(s)));
        });
    }

    [Fact]
    public async Task The_published_catalog_matches_the_enforced_one_attribute_for_attribute()
    {
        var response = await Handler().Handle(new GetSegmentAttributeCatalogQuery(), default);
        var published = response.Data!;

        Assert.Equal(SegmentAttributeCatalog.All.Count, published.Attributes.Count);

        foreach (var declared in SegmentAttributeCatalog.All)
        {
            var item = published.Attributes.Single(a => a.AttributeCode == declared.AttributeCode);
            Assert.Equal(declared.AttributeClass, item.Class);
            Assert.Equal(declared.DeclaredClass, item.DeclaredClass);
            Assert.Equal(declared.ValueType, item.ValueType);
            Assert.Equal(declared.Operators, item.Operators);
            Assert.Equal(declared.RequiredParameters, item.RequiredParameters);
            Assert.Equal(declared.OptionalParameters, item.OptionalParameters);
            Assert.Equal(declared.AllowedSubjectTypes, item.SubjectTypes);
            Assert.Equal(declared.RequiresCrossServiceValueValidation, item.RequiresCrossServiceValueValidation);
            Assert.Equal(declared.ValueSource.Kind, item.ValueSource.Kind);
            Assert.Equal(declared.ValueSource.ReferenceSetCode, item.ValueSource.ReferenceSetCode);
            Assert.Equal(declared.ValueSource.AllowedValues, item.ValueSource.AllowedValues);
            Assert.Equal(declared.ValueSource.EntityKind, item.ValueSource.EntityKind);
        }
    }

    [Fact]
    public async Task Concept_affinity_is_declared_derived_in_service_and_not_cross_service()
    {
        var affinity = SegmentAttributeCatalog.Find(SegmentAttributeCatalog.ConceptAffinity)!;

        // The class decides the fail-closed behaviour: D means an empty graph is an empty ANSWER, not a 503.
        Assert.Equal(SegmentAttributeCatalog.ClassDerived, affinity.AttributeClass);
        Assert.NotEqual(SegmentAttributeCatalog.ClassCrossService, affinity.AttributeClass);

        // Only the VALUE is proven cross-service, which the +X marker states out loud.
        Assert.True(affinity.RequiresCrossServiceValueValidation);
        Assert.Equal("D+X", affinity.DeclaredClass);
        Assert.Equal(SegmentAttributeCatalog.ReferenceKindGlobalProduct, affinity.CrossServiceReferenceKind);

        // It is a contact question: a specialty belongs to a person.
        Assert.Equal(new[] { SegmentSubjectTypes.Contact }, affinity.AllowedSubjectTypes);

        var published = (await Handler().Handle(new GetSegmentAttributeCatalogQuery(), default)).Data!;
        var item = published.Attributes.Single(a => a.AttributeCode == SegmentAttributeCatalog.ConceptAffinity);
        Assert.Equal(SegmentAttributeCatalog.ClassDerived, item.Class);
    }

    [Fact]
    public void Attributes_that_belong_to_another_module_are_deliberately_absent()
    {
        var codes = SegmentAttributeCatalog.All.Select(a => a.AttributeCode).ToList();

        Assert.DoesNotContain(codes, c => c.StartsWith("visit.", StringComparison.Ordinal));
        Assert.DoesNotContain(codes, c => c.StartsWith("frequency.", StringComparison.Ordinal));
        Assert.DoesNotContain(codes, c => c.StartsWith("campaign.", StringComparison.Ordinal));
        Assert.DoesNotContain(codes, c => c.StartsWith("journey.", StringComparison.Ordinal));
        Assert.DoesNotContain(codes, c => c.StartsWith("rep.", StringComparison.Ordinal));
        Assert.DoesNotContain(codes, c => c.StartsWith("person.", StringComparison.Ordinal));
        Assert.DoesNotContain(codes, c => c.StartsWith("score.", StringComparison.Ordinal));
        Assert.DoesNotContain(codes, c => c.StartsWith("icp.", StringComparison.Ordinal));
        Assert.DoesNotContain(codes, c => c.StartsWith("segment.", StringComparison.Ordinal));
    }

    [Fact]
    public void The_only_place_tier_can_live_is_an_account_attribute_key_and_it_is_not_invented_as_a_field()
    {
        var codes = SegmentAttributeCatalog.All.Select(a => a.AttributeCode).ToList();
        Assert.DoesNotContain("account.tier", codes);
        Assert.DoesNotContain("contact.tier", codes);

        var attribute = SegmentAttributeCatalog.Find(SegmentAttributeCatalog.AccountAttribute)!;
        Assert.Contains(SegmentAttributeCatalog.ParameterAttributeCode, attribute.RequiredParameters);
    }

    [Fact]
    public async Task The_contract_advertises_the_capabilities_this_FU_does_not_have_as_false()
    {
        var contract = (await new GetSegmentContractHandler(
                SegmentTestDoubles.Tenant(SegmentTestDoubles.TenantA))
            .Handle(new GetSegmentContractQuery(), default)).Data!;

        var flags = contract.Features;
        Assert.True(flags.SupportsRealTimeMembershipResolution);
        Assert.True(flags.SupportsProductAffinityAttributes);
        Assert.True(flags.SupportsConceptGraphDerivedAttributes);

        Assert.False(flags.SupportsMaterializedMembership);
        Assert.False(flags.SupportsMembershipRefreshJob);
        Assert.False(flags.SupportsMembershipHistory);
        Assert.False(flags.SupportsSegmentOfSegment);
        Assert.False(flags.SupportsIcpScoring);
        Assert.False(flags.SupportsSegmentUsageLog);
        Assert.False(flags.SupportsStrategyTemplate);
        Assert.False(flags.SupportsSubjectList);
        Assert.False(flags.SupportsUcln);
        Assert.False(flags.SupportsCampaignTargetGeneration);
        Assert.False(flags.SupportsFrequencyPolicyWrite);
        Assert.False(flags.SupportsConceptGraphAuthoring);
        Assert.False(flags.SupportsConceptGraphTraversalEngine);

        Assert.False(contract.Limits.MembershipIsPersisted);
        Assert.True(contract.Limits.CriteriaAreEmbeddedInSegmentDocument);
        Assert.Equal(SegmentLimits.MaxCandidateSet, contract.Limits.MaxCandidateSet);
    }

    [Fact]
    public void The_permission_definition_file_touches_no_storage_at_all()
    {
        var members = typeof(SegmentPermissions)
            .GetFields()
            .Select(f => f.FieldType.FullName ?? string.Empty)
            .ToList();

        Assert.DoesNotContain(members, t => t.Contains("Repository", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(members, t => t.Contains("Mongo", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(6, SegmentPermissions.All.Count);
        Assert.All(SegmentPermissions.All, key => Assert.True(key.Split('.').Length >= 3));
    }
}
