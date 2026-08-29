using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.StrategyTemplate;
using Diten.CrmService.Application.Features.StrategyTemplate.Binding;
using Diten.CrmService.Application.Features.StrategyTemplate.Commands;
using Diten.CrmService.Application.Features.StrategyTemplate.Handlers.CommandHandlers;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.StrategyTemplate;

/// <summary>
/// MOD-0167 FU04 — the cross-service fail-closed contract. Two outcomes must never be confused: "the dependency says it
/// does not exist" (a 400 the author can fix) and "the dependency could not answer" (a 503 with nothing written).
/// Both are proven BEFORE persistence, and the repository counters are what proves it.
/// </summary>
public sealed class StrategyTemplateFailClosedTests
{
    private readonly FakeStrategyTemplateRepository _templates = new();
    private readonly FakeSegmentReadRepository _segments = new();
    private readonly FakeVisitFrequencyPolicyRepository _policies = new();
    private readonly FakeKnowledgePathRepository _paths = new();
    private readonly FakeContentEngagementJourneyRepository _journeys = new();
    private readonly FakeStrategyReferenceValidator _references = new();

    private StrategyTemplateBindingValidator Bindings() => new(_segments, _policies, _paths, _journeys);

    private CreateStrategyTemplateHandler Create() => new(
        StrategyTemplateTestDoubles.Tenant(StrategyTemplateTestDoubles.TenantA),
        new NullActorContext(), _templates, Bindings(), _references);

    private UpdateStrategyTemplateHandler Update() => new(
        StrategyTemplateTestDoubles.Tenant(StrategyTemplateTestDoubles.TenantA),
        new NullActorContext(), _templates, Bindings(), _references);

    private Guid Segment() => _segments.Add(StrategyTemplateTestDoubles.TenantA).Id;

    [Fact]
    public async Task An_unknown_global_product_is_a_400_and_nothing_is_inserted()
    {
        var product = Guid.NewGuid();
        _references.NotFound.Add(product);

        var response = await Create().Handle(
            StrategyTemplateTestBuilders.NewTemplate(
                Segment(), productLines: new[] { StrategyTemplateTestBuilders.ProductOnly(product) }),
            default);

        Assert.Equal(400, response.StatusCode);
        Assert.Contains(StrategyTemplateErrorCodes.ProductReferenceNotFound, response.Errors!);
        Assert.Equal(0, _templates.InsertCalls);
        Assert.Empty(_templates.Rows);
    }

    [Fact]
    public async Task An_unknown_sku_is_a_400_and_nothing_is_inserted()
    {
        var gsku = Guid.NewGuid();
        _references.NotFound.Add(gsku);

        var response = await Create().Handle(
            StrategyTemplateTestBuilders.NewTemplate(
                Segment(),
                productLines: new[]
                {
                    StrategyTemplateTestBuilders.SkuAllocated(Guid.NewGuid(), new[] { (gsku, 100m) })
                }),
            default);

        Assert.Equal(400, response.StatusCode);
        Assert.Contains(StrategyTemplateErrorCodes.SkuReferenceNotFound, response.Errors!);
        Assert.Equal(0, _templates.InsertCalls);
    }

    [Fact]
    public async Task An_unreachable_product_master_is_a_503_with_nothing_persisted()
    {
        _references.AllUnavailable = true;

        var response = await Create().Handle(
            StrategyTemplateTestBuilders.NewTemplate(
                Segment(), productLines: new[] { StrategyTemplateTestBuilders.ProductOnly(Guid.NewGuid()) }),
            default);

        Assert.Equal(503, response.StatusCode);
        Assert.Contains(StrategyTemplateErrorCodes.DependencyUnavailable, response.Errors!);
        Assert.Equal(0, _templates.InsertCalls);
        Assert.Empty(_templates.Rows);
    }

    [Fact]
    public async Task An_unreachable_master_on_UPDATE_leaves_the_stored_template_untouched()
    {
        var created = await Create().Handle(
            StrategyTemplateTestBuilders.NewTemplate(
                Segment(), productLines: new[] { StrategyTemplateTestBuilders.ProductOnly(Guid.NewGuid()) }),
            default);
        var id = created.Data;
        var before = _templates.Stored(id);
        var replacesBefore = _templates.ReplaceCalls;

        _references.AllUnavailable = true;
        var response = await Update().Handle(
            new UpdateStrategyTemplateCommand(
                id, before.TemplateName, before.EffectiveFrom, null, null, null, null,
                null, null,
                new[] { StrategyTemplateTestBuilders.ProductOnly(Guid.NewGuid()) },
                null, before.Version),
            default);

        Assert.Equal(503, response.StatusCode);
        Assert.Equal(replacesBefore, _templates.ReplaceCalls);
        Assert.Equal(
            before.ProductLines.Single().GlobalProductId,
            _templates.Stored(id).ProductLines.Single().GlobalProductId);
    }

    [Fact]
    public async Task Each_distinct_reference_is_proven_exactly_once_per_request()
    {
        var sharedGsku = Guid.NewGuid();
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();

        var response = await Create().Handle(
            StrategyTemplateTestBuilders.NewTemplate(
                Segment(),
                productLines: new[]
                {
                    StrategyTemplateTestBuilders.SkuAllocated(productA, new[] { (sharedGsku, 100m) }, sortOrder: 10),
                    StrategyTemplateTestBuilders.SkuAllocated(productB, new[] { (sharedGsku, 100m) }, sortOrder: 20)
                }),
            default);

        Assert.True(response.IsSuccessful);
        // Three distinct ids -> three calls, even though the SKU appears on both lines. A dedup, not a cache.
        Assert.Equal(3, _references.Calls.Count);
        Assert.Single(_references.Calls.Where(c => c.Id == sharedGsku));
    }

    [Fact]
    public async Task The_same_reference_is_proven_again_on_a_second_request()
    {
        var product = Guid.NewGuid();
        await Create().Handle(
            StrategyTemplateTestBuilders.NewTemplate(
                Segment(), "play-a", productLines: new[] { StrategyTemplateTestBuilders.ProductOnly(product) }),
            default);
        var afterFirst = _references.Calls.Count;

        await Create().Handle(
            StrategyTemplateTestBuilders.NewTemplate(
                Segment(), "play-b", productLines: new[] { StrategyTemplateTestBuilders.ProductOnly(product) }),
            default);

        // No cache: a reference that vanished between two writes must be caught by the second one.
        Assert.Equal(afterFirst + 1, _references.Calls.Count);
    }

    [Fact]
    public async Task The_reference_fanout_ceiling_is_a_422_before_any_call_is_made()
    {
        // 50 lines (the document ceiling) x one product + two SKUs each = 150 distinct references, well past the
        // fan-out ceiling of 100 while staying inside every document limit.
        var lines = Enumerable.Range(0, StrategyTemplateLimits.MaxProductLines)
            .Select(i => StrategyTemplateTestBuilders.SkuAllocated(
                Guid.NewGuid(),
                new[] { (Guid.NewGuid(), 50m), (Guid.NewGuid(), 50m) },
                sortOrder: i * 10))
            .ToList();

        var response = await Create().Handle(
            StrategyTemplateTestBuilders.NewTemplate(Segment(), productLines: lines), default);

        // The ceiling is checked before the loop, so an oversized write costs zero dependency calls.
        Assert.Equal(422, response.StatusCode);
        Assert.Contains(StrategyTemplateErrorCodes.ReferenceFanoutExceeded, response.Errors!);
        Assert.Empty(_references.Calls);
        Assert.Equal(0, _templates.InsertCalls);
    }

    [Fact]
    public async Task The_kinds_asked_of_MDM_are_only_global_product_and_gsku()
    {
        await Create().Handle(
            StrategyTemplateTestBuilders.NewTemplate(
                Segment(),
                productLines: new[]
                {
                    StrategyTemplateTestBuilders.SkuAllocated(Guid.NewGuid(), new[] { (Guid.NewGuid(), 100m) })
                }),
            default);

        Assert.All(_references.Calls, call => Assert.Contains(
            call.Kind,
            new[]
            {
                IStrategyTemplateProductReferenceValidator.ReferenceKind.GlobalProduct,
                IStrategyTemplateProductReferenceValidator.ReferenceKind.Gsku
            }));
        // No brand kind exists to ask for (D-BRAND).
        Assert.DoesNotContain(_references.Calls, call => call.Kind.Contains("brand", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_play_with_no_product_dimension_never_calls_the_dependency()
    {
        var response = await Create().Handle(
            StrategyTemplateTestBuilders.NewTemplate(Segment()), default);

        Assert.True(response.IsSuccessful);
        Assert.Empty(_references.Calls);
    }
}
