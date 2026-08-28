using Diten.CrmService.Application.Features.Segmentation;
using Diten.CrmService.Application.Features.Segmentation.Catalog;
using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.Segmentation;

/// <summary>
/// MOD-0167 FU02 — <c>concept.affinity</c> (D-PRODUCT): "is this doctor a doctor who cares about product P?", derived
/// from the MOD-0162 FU03 concept graph without a single product field ever being written on a person.
/// <para>These tests hold the four properties the derivation lives or dies by: it is BOUNDED, it is READ-ONLY, an empty
/// graph is an empty ANSWER (never a 503, never everybody-matches), and it is LIVE.</para>
/// </summary>
public sealed class ConceptAffinityResolutionTests
{
    private static readonly Guid Tenant = SegmentTestDoubles.TenantA;
    private static readonly Guid ConceptSubject = Guid.NewGuid();

    private readonly FakeConceptNodeRepository _nodes = new();
    private readonly FakeConceptRelationshipRepository _edges = new();
    private readonly FakeSegmentRepository _segments = new();
    private readonly FakeTargetCustomerRepository _targets = new();
    private readonly FakeCandidateSource _candidates = new();
    private readonly FakeConsentBulkReader _consent = new();
    private readonly FakeTerritoryCoverageReader _territory = new();

    private ConceptAffinitySourceReader Reader() => new(_nodes, _edges);

    private SegmentMembershipResolver Resolver()
        => new(_candidates,
            new SegmentAttributeSourceReader(_candidates, _consent, _territory, Reader()),
            _targets);

    private Segment SeedAffinitySegment(string productId, int? maxDepth = null)
    {
        var parameters = new Dictionary<string, string>();
        if (maxDepth is { } depth)
        {
            parameters["maxDepth"] = depth.ToString();
        }

        var segment = SegmentTestBuilders.Segment(
            Tenant,
            criteria: SegmentTestBuilders.Criteria(SegmentTestBuilders.Predicate(
                SegmentAttributeCatalog.ConceptAffinity, SegmentOperators.Eq, SegmentValueTypes.Guid,
                new[] { productId }, parameters: parameters)));

        _segments.Rows.Add(segment);
        return segment;
    }

    /// <summary>product --addresses--> specialty("cardiology"). The minimal real chain.</summary>
    private (string ProductId, ConceptNode Product, ConceptNode Specialty) SeedChain(string specialtyCode = "cardiology")
    {
        var productId = Guid.NewGuid().ToString();
        var product = SegmentTestBuilders.ConceptNode(
            Tenant, ConceptSubject, ConceptExternalRefTypes.GlobalProduct, productId);
        var specialty = SegmentTestBuilders.ConceptNode(
            Tenant, ConceptSubject, ConceptExternalRefTypes.ReferenceDataValue, specialtyCode);

        _nodes.Rows.Add(product);
        _nodes.Rows.Add(specialty);
        _edges.Rows.Add(SegmentTestBuilders.Edge(
            Tenant, ConceptSubject, product.Id, specialty.Id, ConceptRelationshipTypes.Addresses));

        return (productId, product, specialty);
    }

    [Fact]
    public async Task Only_a_global_product_node_starts_the_traversal_and_only_a_reference_data_value_ends_it()
    {
        var (productId, product, _) = SeedChain();

        // A document node hanging off the same product must never enter the specialty set.
        var document = SegmentTestBuilders.ConceptNode(
            Tenant, ConceptSubject, ConceptExternalRefTypes.Document, "sop-1");
        _nodes.Rows.Add(document);
        _edges.Rows.Add(SegmentTestBuilders.Edge(
            Tenant, ConceptSubject, product.Id, document.Id, ConceptRelationshipTypes.Addresses));

        var result = await Reader().ResolveSpecialtiesAsync(
            Tenant, productId, 1, null, SegmentTestDoubles.Now, default);

        Assert.True(result.ProductNodeFound);
        Assert.Equal(new[] { "cardiology" }, result.SpecialtyCodes);
    }

    [Fact]
    public async Task A_product_with_no_node_yields_an_empty_set_and_its_own_reason_never_a_503()
    {
        var segment = SeedAffinitySegment(Guid.NewGuid().ToString());
        _candidates.Candidates.Add(SegmentTestBuilders.Contact(Guid.NewGuid(), specialty: "cardiology"));

        var outcome = await Resolver().ResolveAsync(
            Tenant, segment, SegmentTestDoubles.Now, 100, 0, includeExcluded: true, default);

        // A completed 200-shaped result: no exception, no dependency failure, and nobody admitted by default.
        Assert.NotNull(outcome.Result);
        Assert.Empty(outcome.Result!.Members);
        Assert.Contains(
            SegmentReasonCodes.ConceptProductNodeMissing,
            outcome.Result.Excluded.Single().ReasonCodes);
    }

    [Fact]
    public async Task A_product_node_with_no_reachable_specialty_gets_a_different_reason_than_a_missing_product()
    {
        var productId = Guid.NewGuid().ToString();
        _nodes.Rows.Add(SegmentTestBuilders.ConceptNode(
            Tenant, ConceptSubject, ConceptExternalRefTypes.GlobalProduct, productId));

        var segment = SeedAffinitySegment(productId);
        _candidates.Candidates.Add(SegmentTestBuilders.Contact(Guid.NewGuid(), specialty: "cardiology"));

        var outcome = await Resolver().ResolveAsync(
            Tenant, segment, SegmentTestDoubles.Now, 100, 0, includeExcluded: true, default);

        Assert.Contains(
            SegmentReasonCodes.ConceptAffinityNoSpecialtyReached,
            outcome.Result!.Excluded.Single().ReasonCodes);
    }

    [Fact]
    public async Task Depth_defaults_to_one_and_two_reaches_the_second_layer_while_a_third_is_never_walked()
    {
        var productId = Guid.NewGuid().ToString();
        var product = SegmentTestBuilders.ConceptNode(
            Tenant, ConceptSubject, ConceptExternalRefTypes.GlobalProduct, productId);
        var first = SegmentTestBuilders.ConceptNode(
            Tenant, ConceptSubject, ConceptExternalRefTypes.ReferenceDataValue, "layer-1");
        var second = SegmentTestBuilders.ConceptNode(
            Tenant, ConceptSubject, ConceptExternalRefTypes.ReferenceDataValue, "layer-2");
        var third = SegmentTestBuilders.ConceptNode(
            Tenant, ConceptSubject, ConceptExternalRefTypes.ReferenceDataValue, "layer-3");

        _nodes.Rows.AddRange(new[] { product, first, second, third });
        _edges.Rows.Add(SegmentTestBuilders.Edge(
            Tenant, ConceptSubject, product.Id, first.Id, ConceptRelationshipTypes.Addresses));
        _edges.Rows.Add(SegmentTestBuilders.Edge(
            Tenant, ConceptSubject, first.Id, second.Id, ConceptRelationshipTypes.BelongsTo));
        _edges.Rows.Add(SegmentTestBuilders.Edge(
            Tenant, ConceptSubject, second.Id, third.Id, ConceptRelationshipTypes.BelongsTo));

        var atDefault = await Reader().ResolveSpecialtiesAsync(
            Tenant, productId, SegmentLimits.DefaultConceptAffinityDepth, null, SegmentTestDoubles.Now, default);
        Assert.Equal(new[] { "layer-1" }, atDefault.SpecialtyCodes);

        var atTwo = await Reader().ResolveSpecialtiesAsync(
            Tenant, productId, 2, null, SegmentTestDoubles.Now, default);
        Assert.Equal(new[] { "layer-1", "layer-2" }, atTwo.SpecialtyCodes.OrderBy(x => x).ToArray());

        // There is no transitive closure: even asked for more, the walk stops at the ceiling.
        var clamped = await Reader().ResolveSpecialtiesAsync(
            Tenant, productId, 9, null, SegmentTestDoubles.Now, default);
        Assert.DoesNotContain("layer-3", clamped.SpecialtyCodes);
    }

    [Fact]
    public async Task Only_addresses_and_belongs_to_edges_are_followed()
    {
        var productId = Guid.NewGuid().ToString();
        var product = SegmentTestBuilders.ConceptNode(
            Tenant, ConceptSubject, ConceptExternalRefTypes.GlobalProduct, productId);
        _nodes.Rows.Add(product);

        var followed = new[] { ConceptRelationshipTypes.Addresses, ConceptRelationshipTypes.BelongsTo };
        var ignored = new[]
        {
            ConceptRelationshipTypes.LeadsTo, ConceptRelationshipTypes.Requires,
            ConceptRelationshipTypes.Evidences, ConceptRelationshipTypes.Custom
        };

        foreach (var type in followed.Concat(ignored))
        {
            var target = SegmentTestBuilders.ConceptNode(
                Tenant, ConceptSubject, ConceptExternalRefTypes.ReferenceDataValue, type);
            _nodes.Rows.Add(target);
            _edges.Rows.Add(SegmentTestBuilders.Edge(Tenant, ConceptSubject, product.Id, target.Id, type));
        }

        var result = await Reader().ResolveSpecialtiesAsync(
            Tenant, productId, 1, null, SegmentTestDoubles.Now, default);

        Assert.Equal(followed.OrderBy(x => x), result.SpecialtyCodes.OrderBy(x => x));
        Assert.All(ignored, type => Assert.DoesNotContain(type, result.SpecialtyCodes));
    }

    [Fact]
    public async Task A_bidirectional_edge_is_followed_as_declared_and_a_reverse_edge_is_never_derived()
    {
        var productId = Guid.NewGuid().ToString();
        var product = SegmentTestBuilders.ConceptNode(
            Tenant, ConceptSubject, ConceptExternalRefTypes.GlobalProduct, productId);
        var declared = SegmentTestBuilders.ConceptNode(
            Tenant, ConceptSubject, ConceptExternalRefTypes.ReferenceDataValue, "declared");
        var inbound = SegmentTestBuilders.ConceptNode(
            Tenant, ConceptSubject, ConceptExternalRefTypes.ReferenceDataValue, "inbound-only");

        _nodes.Rows.AddRange(new[] { product, declared, inbound });
        _edges.Rows.Add(SegmentTestBuilders.Edge(
            Tenant, ConceptSubject, product.Id, declared.Id, ConceptRelationshipTypes.Addresses,
            direction: ConceptDirections.Bidirectional));
        // Points AT the product. Walking it backwards would be deriving a reverse edge - which never happens.
        _edges.Rows.Add(SegmentTestBuilders.Edge(
            Tenant, ConceptSubject, inbound.Id, product.Id, ConceptRelationshipTypes.Addresses,
            direction: ConceptDirections.Bidirectional));

        var result = await Reader().ResolveSpecialtiesAsync(
            Tenant, productId, 2, null, SegmentTestDoubles.Now, default);

        Assert.Contains("declared", result.SpecialtyCodes);
        Assert.DoesNotContain("inbound-only", result.SpecialtyCodes);
    }

    [Fact]
    public async Task Archived_inactive_and_out_of_window_nodes_and_edges_never_enter_the_set()
    {
        var productId = Guid.NewGuid().ToString();
        var product = SegmentTestBuilders.ConceptNode(
            Tenant, ConceptSubject, ConceptExternalRefTypes.GlobalProduct, productId);
        _nodes.Rows.Add(product);

        var inactiveNode = SegmentTestBuilders.ConceptNode(
            Tenant, ConceptSubject, ConceptExternalRefTypes.ReferenceDataValue, "inactive-node",
            status: ConceptStatuses.Inactive);
        var expiredNode = SegmentTestBuilders.ConceptNode(
            Tenant, ConceptSubject, ConceptExternalRefTypes.ReferenceDataValue, "expired-node",
            effectiveTo: SegmentTestDoubles.Past);
        var behindInactiveEdge = SegmentTestBuilders.ConceptNode(
            Tenant, ConceptSubject, ConceptExternalRefTypes.ReferenceDataValue, "behind-inactive-edge");
        var behindExpiredEdge = SegmentTestBuilders.ConceptNode(
            Tenant, ConceptSubject, ConceptExternalRefTypes.ReferenceDataValue, "behind-expired-edge");

        _nodes.Rows.AddRange(new[] { inactiveNode, expiredNode, behindInactiveEdge, behindExpiredEdge });
        _edges.Rows.Add(SegmentTestBuilders.Edge(
            Tenant, ConceptSubject, product.Id, inactiveNode.Id, ConceptRelationshipTypes.Addresses));
        _edges.Rows.Add(SegmentTestBuilders.Edge(
            Tenant, ConceptSubject, product.Id, expiredNode.Id, ConceptRelationshipTypes.Addresses));
        _edges.Rows.Add(SegmentTestBuilders.Edge(
            Tenant, ConceptSubject, product.Id, behindInactiveEdge.Id, ConceptRelationshipTypes.Addresses,
            status: ConceptStatuses.Inactive));
        _edges.Rows.Add(SegmentTestBuilders.Edge(
            Tenant, ConceptSubject, product.Id, behindExpiredEdge.Id, ConceptRelationshipTypes.Addresses,
            effectiveTo: SegmentTestDoubles.Past));

        var result = await Reader().ResolveSpecialtiesAsync(
            Tenant, productId, 2, null, SegmentTestDoubles.Now, default);

        Assert.Empty(result.SpecialtyCodes);
        Assert.True(result.ProductNodeFound);
    }

    [Fact]
    public async Task Five_hundred_candidates_cost_ONE_node_read_and_ONE_edge_read()
    {
        var (productId, _, _) = SeedChain();
        var segment = SeedAffinitySegment(productId);

        for (var i = 0; i < 500; i++)
        {
            _candidates.Candidates.Add(SegmentTestBuilders.Contact(
                Guid.NewGuid(), specialty: i % 2 == 0 ? "cardiology" : "oncology"));
        }

        await Resolver().ResolveAsync(
            Tenant, segment, SegmentTestDoubles.Now, 1000, 0, includeExcluded: true, default);

        Assert.Equal(1, _nodes.ListCalls);
        Assert.Equal(1, _edges.ListCalls);
    }

    [Fact]
    public async Task Resolving_writes_nothing_into_the_concept_graph()
    {
        var (productId, _, _) = SeedChain();
        var segment = SeedAffinitySegment(productId);
        _candidates.Candidates.Add(SegmentTestBuilders.Contact(Guid.NewGuid(), specialty: "cardiology"));

        var nodesBefore = _nodes.Rows.Count;
        var edgesBefore = _edges.Rows.Count;

        await Resolver().ResolveAsync(
            Tenant, segment, SegmentTestDoubles.Now, 1000, 0, includeExcluded: true, default);

        Assert.Equal(0, _nodes.WriteCalls);
        Assert.Equal(0, _edges.WriteCalls);
        Assert.Equal(nodesBefore, _nodes.Rows.Count);
        Assert.Equal(edgesBefore, _edges.Rows.Count);
    }

    [Fact]
    public async Task A_candidate_with_no_specialty_is_eliminated_and_a_blank_specialty_never_counts_as_a_match()
    {
        var (productId, _, _) = SeedChain();
        var segment = SeedAffinitySegment(productId);
        _candidates.Candidates.Add(SegmentTestBuilders.Contact(Guid.NewGuid(), specialty: null));
        _candidates.Candidates.Add(SegmentTestBuilders.Contact(Guid.NewGuid(), specialty: "   "));

        var outcome = await Resolver().ResolveAsync(
            Tenant, segment, SegmentTestDoubles.Now, 1000, 0, includeExcluded: true, default);

        Assert.Empty(outcome.Result!.Members);
        Assert.All(outcome.Result.Excluded, e =>
            Assert.Contains(SegmentReasonCodes.ConceptSubjectSpecialtyMissing, e.ReasonCodes));
    }

    [Fact]
    public async Task A_specialty_outside_the_reachable_set_is_an_ordinary_negative_with_its_own_reason()
    {
        var (productId, _, _) = SeedChain();
        var segment = SeedAffinitySegment(productId);
        var matching = Guid.NewGuid();
        _candidates.Candidates.Add(SegmentTestBuilders.Contact(matching, specialty: "cardiology"));
        _candidates.Candidates.Add(SegmentTestBuilders.Contact(Guid.NewGuid(), specialty: "dermatology"));

        var outcome = await Resolver().ResolveAsync(
            Tenant, segment, SegmentTestDoubles.Now, 1000, 0, includeExcluded: true, default);

        Assert.Equal(matching, Assert.Single(outcome.Result!.Members).SubjectId);
        Assert.Contains(
            SegmentReasonCodes.ConceptAffinityNotMatched,
            outcome.Result.Excluded.Single().ReasonCodes);
    }

    [Fact]
    public async Task The_derivation_is_live_so_a_new_edge_changes_what_the_SAME_frozen_version_returns()
    {
        var (productId, product, _) = SeedChain();
        var segment = SeedAffinitySegment(productId);
        Assert.NotNull(segment.CriteriaFrozenAt);

        var oncologist = Guid.NewGuid();
        _candidates.Candidates.Add(SegmentTestBuilders.Contact(oncologist, specialty: "oncology"));

        var before = await Resolver().ResolveAsync(
            Tenant, segment, SegmentTestDoubles.Now, 1000, 0, includeExcluded: false, default);
        Assert.Empty(before.Result!.Members);

        var oncology = SegmentTestBuilders.ConceptNode(
            Tenant, ConceptSubject, ConceptExternalRefTypes.ReferenceDataValue, "oncology");
        _nodes.Rows.Add(oncology);
        _edges.Rows.Add(SegmentTestBuilders.Edge(
            Tenant, ConceptSubject, product.Id, oncology.Id, ConceptRelationshipTypes.Addresses));

        // A NEW resolver: activation froze the criteria TREE, not the answer of the derivation.
        var after = await Resolver().ResolveAsync(
            Tenant, segment, SegmentTestDoubles.Now, 1000, 0, includeExcluded: false, default);

        Assert.Equal(oncologist, Assert.Single(after.Result!.Members).SubjectId);
        Assert.Equal(segment.SegmentVersion, after.Result.SegmentVersion);
    }

    [Fact]
    public async Task A_graph_belonging_to_another_tenant_is_invisible()
    {
        var (productId, _, _) = SeedChain();

        var result = await Reader().ResolveSpecialtiesAsync(
            SegmentTestDoubles.TenantB, productId, 2, null, SegmentTestDoubles.Now, default);

        Assert.False(result.ProductNodeFound);
        Assert.Empty(result.SpecialtyCodes);
    }
}
