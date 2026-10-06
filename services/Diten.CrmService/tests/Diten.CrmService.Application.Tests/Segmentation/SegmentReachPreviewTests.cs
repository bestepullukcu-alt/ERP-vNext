using Diten.CrmService.Application.Features.Segmentation;
using Diten.CrmService.Application.Features.Segmentation.Catalog;
using Diten.CrmService.Application.Features.Segmentation.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.Segmentation.Queries;
using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.Segmentation;

/// <summary>
/// MOD-0167 FU02 — the DRAFT-rule reach preview (WP-SEG-C). These tests hold the promise the reach rail is built on: a
/// draft's total is the same number the saved rule would resolve to, every predicate gets its own "reach alone" count,
/// the sample is bounded, the write-path structural rules still reject a bad draft, and nothing is persisted.
/// </summary>
public sealed class SegmentReachPreviewTests
{
    private static readonly Guid Tenant = SegmentTestDoubles.TenantA;

    private readonly FakeSegmentRepository _segments = new();
    private readonly FakeTargetCustomerRepository _targets = new();
    private readonly FakeCandidateSource _candidates = new();
    private readonly FakeConsentBulkReader _consent = new();
    private readonly FakeTerritoryCoverageReader _territory = new();
    private readonly FakeConceptAffinityReader _affinity = new();

    private SegmentMembershipResolver Resolver() => new(
        _candidates,
        new SegmentAttributeSourceReader(_candidates, _consent, _territory, _affinity),
        _targets);

    private PreviewSegmentReachHandler Handler() =>
        new(SegmentTestDoubles.Tenant(Tenant), Resolver());

    private static List<SegmentCriteriaNodeInput> SpecialtyIs(string value)
        => new()
        {
            SegmentTestBuilders.Predicate(
                SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.Eq, SegmentValueTypes.String,
                new[] { value })
        };

    [Fact]
    public async Task Draft_preview_total_equals_the_matched_count_of_the_same_rule_saved_and_resolved()
    {
        var inputs = SpecialtyIs("cardiology");

        // The same rule, saved as an active/effective dynamic segment.
        var segment = SegmentTestBuilders.Segment(
            Tenant, criteria: SegmentMapper.ToCriteria(inputs), status: SegmentStatuses.Active);
        _segments.Rows.Add(segment);

        for (var i = 0; i < 40; i++)
        {
            _candidates.Candidates.Add(SegmentTestBuilders.Contact(
                Guid.NewGuid(), specialty: i % 2 == 0 ? "cardiology" : "oncology"));
        }

        var resolved = await Resolver().ResolveAsync(
            Tenant, segment, SegmentTestDoubles.Now, 1000, 0, includeExcluded: false, default);

        var preview = await Handler().Handle(
            new PreviewSegmentReachQuery(
                SegmentSubjectTypes.Contact, SegmentMatchModes.All, inputs, SegmentTestDoubles.Now),
            default);

        Assert.Equal(200, preview.StatusCode);
        Assert.Equal(resolved.Result!.MatchedCount, preview.Data!.TotalCount);
        Assert.Equal(20, preview.Data.TotalCount);
    }

    [Fact]
    public async Task Every_predicate_gets_its_own_reach_alone_count_and_the_or_total_is_their_union()
    {
        var inputs = new List<SegmentCriteriaNodeInput>
        {
            SegmentTestBuilders.Predicate(
                SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.Eq, SegmentValueTypes.String,
                new[] { "cardiology" }, sortOrder: 0),
            SegmentTestBuilders.Predicate(
                SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.Eq, SegmentValueTypes.String,
                new[] { "oncology" }, sortOrder: 1)
        };

        AddContacts("cardiology", 10);
        AddContacts("oncology", 6);
        AddContacts("nephrology", 4);

        var preview = await Handler().Handle(
            new PreviewSegmentReachQuery(
                SegmentSubjectTypes.Contact, SegmentMatchModes.Any, inputs, SegmentTestDoubles.Now),
            default);

        Assert.Equal(200, preview.StatusCode);
        var conditions = preview.Data!.ConditionCounts;
        Assert.Equal(2, conditions.Count);
        Assert.Equal(10, conditions[0].Count);
        Assert.Equal(6, conditions[1].Count);
        // matchMode=any (OR) over the two, the union of the disjoint specialties.
        Assert.Equal(16, preview.Data.TotalCount);
        Assert.All(conditions, c => Assert.False(c.CapExceeded));
    }

    [Fact]
    public async Task Condition_counts_echo_the_callers_node_ids_so_the_rail_can_line_them_up()
    {
        var nodeA = Guid.NewGuid();
        var nodeB = Guid.NewGuid();
        var inputs = new List<SegmentCriteriaNodeInput>
        {
            SegmentTestBuilders.Predicate(
                SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.Eq, SegmentValueTypes.String,
                new[] { "cardiology" }, nodeId: nodeA, sortOrder: 0),
            SegmentTestBuilders.Predicate(
                SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.Eq, SegmentValueTypes.String,
                new[] { "oncology" }, nodeId: nodeB, sortOrder: 1)
        };

        AddContacts("cardiology", 3);

        var preview = await Handler().Handle(
            new PreviewSegmentReachQuery(
                SegmentSubjectTypes.Contact, SegmentMatchModes.Any, inputs, SegmentTestDoubles.Now),
            default);

        var ids = preview.Data!.ConditionCounts.Select(c => c.NodeId).ToList();
        Assert.Contains(nodeA, ids);
        Assert.Contains(nodeB, ids);
    }

    [Fact]
    public async Task The_member_sample_is_bounded_to_the_sample_limit_when_the_reach_is_larger()
    {
        for (var i = 0; i < 80; i++)
        {
            _candidates.Candidates.Add(SegmentTestBuilders.Contact(Guid.NewGuid(), specialty: "cardiology"));
        }

        var preview = await Handler().Handle(
            new PreviewSegmentReachQuery(
                SegmentSubjectTypes.Contact, SegmentMatchModes.All, SpecialtyIs("cardiology"),
                SegmentTestDoubles.Now),
            default);

        Assert.Equal(80, preview.Data!.TotalCount);
        Assert.Equal(preview.Data.SampleLimit, preview.Data.SampleMembers.Count);
        Assert.True(preview.Data.SampleMembers.Count < preview.Data.TotalCount);
    }

    [Fact]
    public async Task Sampled_members_carry_the_display_name_the_resolver_projects_at_no_extra_read()
    {
        // A reach smaller than the sample limit, so every member is in the sample and the named one is guaranteed there.
        _candidates.Candidates.Add(SegmentTestBuilders.Contact(
            Guid.NewGuid(), specialty: "cardiology", displayName: "Dr Ada Lovelace"));
        AddContacts("cardiology", 4);

        var preview = await Handler().Handle(
            new PreviewSegmentReachQuery(
                SegmentSubjectTypes.Contact, SegmentMatchModes.All, SpecialtyIs("cardiology"),
                SegmentTestDoubles.Now),
            default);

        Assert.Equal(5, preview.Data!.TotalCount);
        Assert.Equal(5, preview.Data.SampleMembers.Count);
        Assert.Contains(preview.Data.SampleMembers, m => m.DisplayName == "Dr Ada Lovelace");
        // The label rides on the candidate projection: one pushdown, no per-subject lookup was added.
        Assert.Equal(0, _candidates.LoadSubjectsCalls);
    }

    [Fact]
    public async Task An_empty_rule_is_a_400_before_any_resolve_runs()
    {
        var preview = await Handler().Handle(
            new PreviewSegmentReachQuery(
                SegmentSubjectTypes.Contact, SegmentMatchModes.All,
                new List<SegmentCriteriaNodeInput>(), SegmentTestDoubles.Now),
            default);

        Assert.Equal(400, preview.StatusCode);
        Assert.Null(preview.Data);
        Assert.Equal(0, _candidates.LoadCandidatesCalls);
    }

    [Fact]
    public async Task An_unknown_attribute_is_a_400_with_the_catalog_error_code()
    {
        var inputs = new List<SegmentCriteriaNodeInput>
        {
            SegmentTestBuilders.Predicate(
                "contact.does-not-exist", SegmentOperators.Eq, SegmentValueTypes.String, new[] { "x" })
        };

        var preview = await Handler().Handle(
            new PreviewSegmentReachQuery(
                SegmentSubjectTypes.Contact, SegmentMatchModes.All, inputs, SegmentTestDoubles.Now),
            default);

        Assert.Equal(400, preview.StatusCode);
        Assert.Contains(SegmentErrorCodes.AttributeUnknown, preview.Errors!);
        Assert.Equal(0, _candidates.LoadCandidatesCalls);
    }

    [Fact]
    public async Task An_invalid_subject_type_is_a_400()
    {
        var preview = await Handler().Handle(
            new PreviewSegmentReachQuery(
                "planet", SegmentMatchModes.All, SpecialtyIs("cardiology"), SegmentTestDoubles.Now),
            default);

        Assert.Equal(400, preview.StatusCode);
        Assert.Null(preview.Data);
    }

    [Fact]
    public async Task A_rule_past_the_candidate_ceiling_answers_422_and_no_partial_number()
    {
        _candidates.ForceCapExceeded = true;

        var preview = await Handler().Handle(
            new PreviewSegmentReachQuery(
                SegmentSubjectTypes.Contact, SegmentMatchModes.All, SpecialtyIs("cardiology"),
                SegmentTestDoubles.Now),
            default);

        Assert.Equal(422, preview.StatusCode);
        Assert.Contains(SegmentErrorCodes.CandidateSetTooLarge, preview.Errors!);
        Assert.Null(preview.Data);
    }

    [Fact]
    public async Task Previewing_persists_nothing_and_is_repeatable()
    {
        AddContacts("cardiology", 5);
        var segmentsBefore = _segments.Rows.Count;
        var targetsBefore = _targets.Rows.Count;

        var query = new PreviewSegmentReachQuery(
            SegmentSubjectTypes.Contact, SegmentMatchModes.All, SpecialtyIs("cardiology"), SegmentTestDoubles.Now);

        var first = await Handler().Handle(query, default);
        var second = await Handler().Handle(query, default);

        Assert.Equal(first.Data!.TotalCount, second.Data!.TotalCount);
        Assert.Equal(5, first.Data.TotalCount);
        // No collection grew: a preview is a report, not a write.
        Assert.Equal(segmentsBefore, _segments.Rows.Count);
        Assert.Equal(targetsBefore, _targets.Rows.Count);
    }

    private void AddContacts(string specialty, int count)
    {
        for (var i = 0; i < count; i++)
        {
            _candidates.Candidates.Add(SegmentTestBuilders.Contact(Guid.NewGuid(), specialty: specialty));
        }
    }
}
