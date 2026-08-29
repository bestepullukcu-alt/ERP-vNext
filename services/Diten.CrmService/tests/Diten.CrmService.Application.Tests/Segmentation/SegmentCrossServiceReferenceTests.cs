using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Segmentation;
using Diten.CrmService.Application.Features.Segmentation.Catalog;
using Diten.CrmService.Application.Features.Segmentation.Commands;
using Diten.CrmService.Application.Features.Segmentation.Handlers.CommandHandlers;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.Segmentation;

/// <summary>
/// MOD-0167 FU02 — the D6 asymmetry, tested where it actually bites: a criterion VALUE that names an MDM master is
/// proven CROSS-PROCESS before anything is written.
/// <para>404 means the dependency answered and the rule is not authorable (400). Unreachable means we do not know,
/// which is a 503 with <b>nothing persisted</b> — never a partially accepted rule, and never an optimistic pass.
/// Contrast that with an in-service uncertainty, which eliminates a candidate with a reason and lets the resolution
/// complete: those tests live in the resolver and concept-affinity suites.</para>
/// </summary>
public sealed class SegmentCrossServiceReferenceTests
{
    private static readonly Guid Tenant = SegmentTestDoubles.TenantA;

    private readonly FakeSegmentRepository _segments = new();
    private readonly FakeProductReferenceValidator _references = new();

    private CreateSegmentHandler Create() => new(
        SegmentTestDoubles.Tenant(Tenant), new NullActorContext(), _segments, _references);

    private static CreateSegmentCommand AffinitySegment(string code, params string[] productIds)
        => new(code, "Affinity", SegmentTypes.Dynamic, SegmentSubjectTypes.Contact, SegmentMatchModes.All,
            SegmentTestDoubles.Past, null, null, null, null,
            new List<SegmentCriteriaNodeInput>
            {
                SegmentTestBuilders.Predicate(
                    SegmentAttributeCatalog.ConceptAffinity,
                    productIds.Length > 1 ? SegmentOperators.In : SegmentOperators.Eq,
                    SegmentValueTypes.Guid, productIds)
            });

    [Fact]
    public async Task An_unreachable_reference_master_is_a_503_and_nothing_is_persisted()
    {
        _references.Result = ISegmentProductReferenceValidator.Outcome.Unavailable;

        var response = await Create().Handle(
            AffinitySegment("aff-a", Guid.NewGuid().ToString()), default);

        Assert.Equal(503, response.StatusCode);
        Assert.Contains(SegmentErrorCodes.DependencyUnavailable, response.Errors!);

        // The proof runs BEFORE the insert, so an outage cannot leave a half-authored segment behind.
        Assert.Empty(_segments.Rows);
    }

    [Fact]
    public async Task A_reference_that_does_not_exist_is_a_400_and_not_a_503()
    {
        _references.Result = ISegmentProductReferenceValidator.Outcome.NotFound;

        var response = await Create().Handle(
            AffinitySegment("aff-b", Guid.NewGuid().ToString()), default);

        Assert.Equal(400, response.StatusCode);
        Assert.Contains(SegmentErrorCodes.CriteriaReferenceNotFound, response.Errors!);
        Assert.DoesNotContain(SegmentErrorCodes.DependencyUnavailable, response.Errors!);
        Assert.Empty(_segments.Rows);
    }

    [Fact]
    public async Task A_valid_reference_lets_the_segment_be_written()
    {
        _references.Result = ISegmentProductReferenceValidator.Outcome.Valid;

        var response = await Create().Handle(
            AffinitySegment("aff-c", Guid.NewGuid().ToString()), default);

        Assert.True(response.IsSuccessful);
        Assert.Single(_segments.Rows);
        Assert.Equal(SegmentAttributeCatalog.ReferenceKindGlobalProduct, Assert.Single(_references.Kinds));
    }

    [Fact]
    public async Task There_is_no_cache_so_the_same_id_twice_makes_two_calls()
    {
        var productId = Guid.NewGuid().ToString();

        await Create().Handle(AffinitySegment("aff-d", productId, productId), default);

        // Two values (even identical ones) mean two proofs: a cache here could authorise a rule against a reference
        // that no longer exists, which is the exact thing this validator is for.
        Assert.Equal(2, _references.Calls);
    }

    [Fact]
    public async Task A_criterion_with_no_cross_service_value_never_calls_the_dependency_at_all()
    {
        _references.Result = ISegmentProductReferenceValidator.Outcome.Unavailable;

        var response = await Create().Handle(
            new CreateSegmentCommand("native-only", "Native", SegmentTypes.Dynamic, SegmentSubjectTypes.Contact,
                SegmentMatchModes.All, SegmentTestDoubles.Past, null, null, null, null,
                SegmentTestBuilders.SpecialtyIs("cardiology")),
            default);

        Assert.True(response.IsSuccessful);
        Assert.Equal(0, _references.Calls);
    }

    [Fact]
    public async Task A_consent_scope_value_is_proven_against_the_right_master()
    {
        _references.Result = ISegmentProductReferenceValidator.Outcome.Valid;

        await Create().Handle(
            new CreateSegmentCommand("scope-a", "Scope", SegmentTypes.Dynamic, SegmentSubjectTypes.Contact,
                SegmentMatchModes.All, SegmentTestDoubles.Past, null, null, null, null,
                new List<SegmentCriteriaNodeInput>
                {
                    SegmentTestBuilders.Predicate(
                        SegmentAttributeCatalog.ConsentScopeBrand, SegmentOperators.Eq, SegmentValueTypes.Guid,
                        new[] { Guid.NewGuid().ToString() })
                }),
            default);

        Assert.Equal(SegmentAttributeCatalog.ReferenceKindBrand, Assert.Single(_references.Kinds));
    }
}
