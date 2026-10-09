using System.Text.RegularExpressions;
using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Segmentation;
using Diten.CrmService.Application.Features.Segmentation.Catalog;
using Diten.CrmService.Application.Features.Segmentation.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.Segmentation.Queries;
using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Application.Tests.Territory;
using Diten.CrmService.Domain.Entities;
using Xunit;
using B = Diten.CrmService.Application.Tests.Segmentation.SegmentTestBuilders;

namespace Diten.CrmService.Application.Tests.Segmentation;

/// <summary>
/// WP-E2E-FIX-3 (E1-B2) — territory / link conditions narrow the candidate query (id pre-queries, <c>_id IN</c>) and the
/// in-memory evaluation stays the judge. On the PRODUCTION resolver, attribute reader, territory coverage reader
/// (<see cref="SegmentTerritoryCoverageReader"/>) and pre-filter (<see cref="SegmentCandidatePrefilter"/>); only the
/// stores are in-memory. Equivalence: with and without the pre-filter the SAME members, with fewer candidates. A fully
/// native rule past the ceiling is counted by the store in the preview; a partly pushable one keeps today's 422. String
/// criteria and the territory-model search are Turkish-insensitive.
/// </summary>
public sealed class SegmentCandidatePushdownTests
{
    private static readonly Guid Tenant = SegmentTestDoubles.TenantA;
    private static readonly DateTimeOffset At = SegmentTestDoubles.Now;

    private readonly FakeCandidateSource _candidates = new();
    private readonly FakeTerritoryModelRepo _models = new();
    private readonly FakeAccountTerritoryAssignmentRepo _assignments = new();
    private readonly Guid _model = Guid.NewGuid();
    private readonly Guid _n1 = Guid.NewGuid();
    private readonly Guid _n2 = Guid.NewGuid();
    private readonly Guid _a1 = Guid.NewGuid(), _a2 = Guid.NewGuid(), _a3 = Guid.NewGuid(), _a4 = Guid.NewGuid();
    private readonly Guid _c1 = Guid.NewGuid(), _c2 = Guid.NewGuid(), _c3 = Guid.NewGuid(),
        _c4 = Guid.NewGuid(), _c5 = Guid.NewGuid(), _c6 = Guid.NewGuid();

    public SegmentCandidatePushdownTests()
    {
        _models.Items.Add(new TerritoryModel
        {
            Id = _model, TenantId = Tenant, ModelCode = "TR", Status = "active", EffectiveFrom = SegmentTestDoubles.Past
        });
        Assign(_a1, _n1);
        Assign(_a2, _n2);
        Assign(_a3, _n1, ended: true); // not current: neither the pre-filter nor the evaluator counts it

        // C1 gastro @A1 (medical, primary, hospital) · C2 cardio @A1 · C3 gastro @A2 (pharmacy) · C4 gastro @A3 (ended
        // coverage, medical primary) · C5 gastro, no link · C6 family @A2 (nurse).
        _candidates.Candidates.AddRange(new[]
        {
            B.Contact(_c1, "gastroenterology"), B.Contact(_c2, "cardiology"), B.Contact(_c3, "gastroenterology"),
            B.Contact(_c4, "gastroenterology"), B.Contact(_c5, "gastroenterology"), B.Contact(_c6, "family-medicine")
        });
        _candidates.Links.AddRange(new[]
        {
            new SegmentLinkProjection(_c1, _a1, "medical", true, "hospital"),
            new SegmentLinkProjection(_c2, _a1, "medical", false, "hospital"),
            new SegmentLinkProjection(_c3, _a2, "medical", false, "pharmacy"),
            new SegmentLinkProjection(_c4, _a3, "medical", true, "hospital"),
            new SegmentLinkProjection(_c6, _a2, "nurse", false, "pharmacy")
        });
    }

    private void Assign(Guid account, Guid node, bool ended = false) => _assignments.Items.Add(new AccountTerritoryAssignment
    {
        Id = Guid.NewGuid(), TenantId = Tenant, AccountId = account, TerritoryModelId = _model, TerritoryNodeId = node,
        TerritoryNodeCode = "N", TerritoryNodeName = "N", AssignmentStatus = ended ? "ended" : "active",
        EffectiveFrom = SegmentTestDoubles.Past, EndedAt = ended ? SegmentTestDoubles.Past : null
    });

    private SegmentMembershipResolver Resolver(bool prefilter) => new(
        _candidates,
        new SegmentAttributeSourceReader(
            _candidates, new FakeConsentBulkReader(), new SegmentTerritoryCoverageReader(_assignments, _models),
            new FakeConceptAffinityReader()),
        new FakeTargetCustomerRepository(),
        prefilter ? new SegmentCandidatePrefilter(_candidates, _assignments, _models) : null);

    /// <summary>Resolves the same rule twice — without and with the pre-filter — and returns both member sets and
    /// candidate counts.</summary>
    private async Task<(Guid[] Plain, Guid[] Pushed, int PlainCandidates, int PushedCandidates)> Both(
        Segment segment)
    {
        var plain = await Resolver(prefilter: false).ResolveAsync(Tenant, segment, At, 1000, 0, false, default);
        var plainCandidates = _candidates.LastCandidateCount;
        var pushed = await Resolver(prefilter: true).ResolveAsync(Tenant, segment, At, 1000, 0, false, default);
        return (plain.Result!.Members.Select(m => m.SubjectId).ToArray(),
            pushed.Result!.Members.Select(m => m.SubjectId).ToArray(),
            plainCandidates, _candidates.LastCandidateCount);
    }

    private static Segment ContactRule(string groupOperator, params Func<Guid, SegmentCriteriaNodeInput>[] children)
    {
        var group = Guid.NewGuid();
        var nodes = new List<SegmentCriteriaNodeInput> { B.Group(groupOperator, group) };
        nodes.AddRange(children.Select((c, i) => c(group) with { SortOrder = i }));
        return B.Segment(Tenant, criteria: B.Criteria(nodes.ToArray()));
    }

    private static Func<Guid, SegmentCriteriaNodeInput> P(
        string code, string op, string type, string value, bool negate = false)
        => parent => B.Predicate(code, op, type, new[] { value }, parentNodeId: parent, negate: negate);

    // ── Acceptance 7 — equivalence: same members, fewer candidates ───────────────────────────────────────────────

    [Fact]
    public async Task Territory_node_block_is_pushed_and_the_members_are_identical()
    {
        var segment = ContactRule(SegmentGroupOperators.And,
            P(SegmentAttributeCatalog.TerritoryNode, SegmentOperators.In, SegmentValueTypes.Guid, _n1.ToString()),
            P(SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.Eq, SegmentValueTypes.String, "gastroenterology"));

        var r = await Both(segment);

        Assert.Equal(new[] { _c1 }, r.Pushed);
        Assert.Equal(r.Plain.OrderBy(x => x), r.Pushed.OrderBy(x => x));
        Assert.Equal(6, r.PlainCandidates);
        Assert.Equal(2, r.PushedCandidates); // C1 + C2 (linked to the currently covered A1); C2 is rejected in memory
    }

    [Fact]
    public async Task Link_blocks_role_and_primary_are_pushed_and_the_members_are_identical()
    {
        var segment = ContactRule(SegmentGroupOperators.And,
            P(SegmentAttributeCatalog.ContactAccountRole, SegmentOperators.Eq, SegmentValueTypes.String, "MEDICAL"),
            P(SegmentAttributeCatalog.ContactIsPrimary, SegmentOperators.Eq, SegmentValueTypes.Bool, "true"));

        var r = await Both(segment);

        Assert.Equal(new[] { _c1, _c4 }.OrderBy(x => x), r.Pushed.OrderBy(x => x));
        Assert.Equal(r.Plain.OrderBy(x => x), r.Pushed.OrderBy(x => x));
        Assert.True(r.PushedCandidates < r.PlainCandidates);
    }

    [Fact]
    public async Task An_OR_group_whose_branches_all_narrow_is_pushed_as_a_union()
    {
        var segment = ContactRule(SegmentGroupOperators.Or,
            P(SegmentAttributeCatalog.TerritoryNode, SegmentOperators.Eq, SegmentValueTypes.Guid, _n2.ToString()),
            P(SegmentAttributeCatalog.ContactAccountType, SegmentOperators.Eq, SegmentValueTypes.String, "pharmacy"));

        var r = await Both(segment);

        Assert.Equal(new[] { _c3, _c6 }.OrderBy(x => x), r.Pushed.OrderBy(x => x));
        Assert.Equal(r.Plain.OrderBy(x => x), r.Pushed.OrderBy(x => x));
        Assert.Equal(2, r.PushedCandidates);
    }

    [Fact]
    public async Task An_OR_group_with_a_branch_that_cannot_narrow_is_not_pushed_and_still_correct()
    {
        var segment = ContactRule(SegmentGroupOperators.Or,
            P(SegmentAttributeCatalog.TerritoryNode, SegmentOperators.Eq, SegmentValueTypes.Guid, _n2.ToString()),
            P(SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.Eq, SegmentValueTypes.String, "gastroenterology",
                negate: true));

        var r = await Both(segment);

        Assert.Equal(r.Plain.OrderBy(x => x), r.Pushed.OrderBy(x => x));
        Assert.Equal(r.PlainCandidates, r.PushedCandidates); // nothing narrowed: one wide branch makes the OR wide
        Assert.Empty(SegmentPushdownRules.PrefilterLeaves(segment.Criteria, segment.MatchMode, isContact: true));
    }

    [Fact]
    public async Task An_account_segment_territory_model_block_is_pushed_and_the_members_are_identical()
    {
        _candidates.Candidates.Clear();
        _candidates.Candidates.AddRange(new[] { B.Account(_a1), B.Account(_a2), B.Account(_a3), B.Account(_a4) });
        var group = Guid.NewGuid();
        var segment = B.Segment(Tenant, subjectType: SegmentSubjectTypes.Account, criteria: B.Criteria(
            B.Group(SegmentGroupOperators.And, group),
            B.Predicate(SegmentAttributeCatalog.TerritoryModel, SegmentOperators.Eq, SegmentValueTypes.Guid,
                new[] { _model.ToString() }, parentNodeId: group)));

        var r = await Both(segment);

        Assert.Equal(new[] { _a1, _a2 }.OrderBy(x => x), r.Pushed.OrderBy(x => x));
        Assert.Equal(r.Plain.OrderBy(x => x), r.Pushed.OrderBy(x => x));
        Assert.Equal(2, r.PushedCandidates);
        Assert.Equal(4, r.PlainCandidates);
    }

    [Fact]
    public async Task Without_a_current_model_a_territory_leaf_is_not_pushed_and_the_answer_is_unchanged()
    {
        _models.Items[0].Status = "draft";
        var segment = ContactRule(SegmentGroupOperators.And,
            P(SegmentAttributeCatalog.TerritoryNode, SegmentOperators.In, SegmentValueTypes.Guid, _n1.ToString()));

        var r = await Both(segment);

        Assert.Equal(r.Plain, r.Pushed);
        Assert.Equal(r.PlainCandidates, r.PushedCandidates);
    }

    [Fact]
    public void Negations_dates_and_has_coverage_false_never_narrow()
    {
        Assert.Equal(SegmentPushdownRules.LeafKind.None, SegmentPushdownRules.Classify(Node(
            SegmentAttributeCatalog.TerritoryNode, SegmentOperators.Ne, SegmentValueTypes.Guid, _n1.ToString()), true));
        Assert.Equal(SegmentPushdownRules.LeafKind.None, SegmentPushdownRules.Classify(Node(
            SegmentAttributeCatalog.TerritoryHasCoverage, SegmentOperators.Eq, SegmentValueTypes.Bool, "false"), true));
        Assert.Equal(SegmentPushdownRules.LeafKind.Prefilter, SegmentPushdownRules.Classify(Node(
            SegmentAttributeCatalog.TerritoryHasCoverage, SegmentOperators.Eq, SegmentValueTypes.Bool, "true"), true));
        Assert.Equal(SegmentPushdownRules.LeafKind.None, SegmentPushdownRules.Classify(Node(
            SegmentAttributeCatalog.ContactAccountRole, SegmentOperators.Eq, SegmentValueTypes.String, "medical", negate: true), true));
        Assert.Equal(SegmentPushdownRules.LeafKind.None, SegmentPushdownRules.Classify(Node(
            SegmentAttributeCatalog.ContactAccountRole, SegmentOperators.Eq, SegmentValueTypes.String, "medical"), isContact: false));
    }

    // ── Acceptance 8 — preview past the ceiling ──────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_fully_native_rule_over_the_ceiling_is_counted_by_the_store_instead_of_422()
    {
        _candidates.ForceCapExceeded = true;
        _candidates.FullyNativeCount = 56_052; // live: gastro + family + internal medicine, tenant 97c5
        var handler = new PreviewSegmentReachHandler(TenantCtx(), Resolver(prefilter: true));

        var response = await handler.Handle(new PreviewSegmentReachQuery(
            SegmentSubjectTypes.Contact, SegmentMatchModes.All,
            new[]
            {
                B.Predicate(SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.In, SegmentValueTypes.String,
                    new[] { "gastroenterology", "family-medicine", "internal-medicine" })
            }, At), default);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(56_052, response.Data!.TotalCount);
        Assert.True(response.Data.CountedByStore);
        Assert.Empty(response.Data.SampleMembers);
        var condition = Assert.Single(response.Data.ConditionCounts);
        Assert.Equal(56_052, condition.Count);
        Assert.False(condition.CapExceeded);
    }

    [Fact]
    public async Task A_partly_pushable_rule_over_the_ceiling_keeps_todays_422_and_never_asks_the_store_to_count()
    {
        _candidates.ForceCapExceeded = true;
        _candidates.FullyNativeCount = 56_052;
        var handler = new PreviewSegmentReachHandler(TenantCtx(), Resolver(prefilter: true));

        var response = await handler.Handle(new PreviewSegmentReachQuery(
            SegmentSubjectTypes.Contact, SegmentMatchModes.All,
            new[]
            {
                B.Predicate(SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.In, SegmentValueTypes.String,
                    new[] { "gastroenterology", "family-medicine", "internal-medicine" }),
                B.Predicate(SegmentAttributeCatalog.TerritoryNode, SegmentOperators.Eq, SegmentValueTypes.Guid,
                    new[] { _n1.ToString() }, sortOrder: 1)
            }, At), default);

        Assert.Equal(422, response.StatusCode);
        Assert.Equal(SegmentErrorCodes.CandidateSetTooLarge, response.Errors![0]);
        Assert.Equal(0, _candidates.CountCalls); // the resolver's fully-native gate stops before the store
    }

    [Fact]
    public async Task Membership_resolution_keeps_its_ceiling_even_for_a_fully_native_rule()
    {
        _candidates.ForceCapExceeded = true;
        _candidates.FullyNativeCount = 56_052;
        var segment = B.Segment(Tenant, criteria: B.Criteria(B.SpecialtyIs("family-medicine").ToArray()));

        var outcome = await Resolver(prefilter: true).ResolveAsync(Tenant, segment, At, 50, 0, false, default);

        Assert.True(outcome.CandidateCapExceeded);
        Assert.Null(outcome.Result);
    }

    [Fact]
    public void Fully_native_means_every_condition_is_a_positive_native_predicate()
    {
        var specialty = B.Criteria(B.SpecialtyIs("gastroenterology").ToArray());
        Assert.True(SegmentPushdownRules.IsFullyNative(specialty, isContact: true));

        var withTerritory = ContactRule(SegmentGroupOperators.And,
            P(SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.Eq, SegmentValueTypes.String, "gastroenterology"),
            P(SegmentAttributeCatalog.TerritoryNode, SegmentOperators.Eq, SegmentValueTypes.Guid, _n1.ToString()));
        Assert.False(SegmentPushdownRules.IsFullyNative(withTerritory.Criteria, isContact: true));

        var negated = ContactRule(SegmentGroupOperators.And,
            P(SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.Eq, SegmentValueTypes.String, "gastroenterology", negate: true));
        Assert.False(SegmentPushdownRules.IsFullyNative(negated.Criteria, isContact: true));
    }

    // ── Acceptance 9 — Turkish-insensitive string criteria + territory-model search ──────────────────────────────

    [Theory]
    [InlineData("İstanbul")]
    [InlineData("istanbul")]
    [InlineData("ISTANBUL")]
    [InlineData("Istanbul")]
    public void Segment_eq_and_contains_patterns_are_turkish_insensitive(string stored)
    {
        foreach (var term in new[] { "istanbul", "İstanbul", "ISTANBUL" })
        {
            Assert.Matches(Mongoish(SegmentPushdownRules.StringPattern(term, anchored: true, turkishInsensitive: true)), stored);
            Assert.Matches(Mongoish(SegmentPushdownRules.StringPattern(term[..3], anchored: false, turkishInsensitive: true)), stored);
        }

        // eq keeps ^…$ — it is still an equality, not a contains
        Assert.DoesNotMatch(Mongoish(SegmentPushdownRules.StringPattern("istanbul", anchored: true, turkishInsensitive: true)), stored + " Avrupa");
        // the value is literal text, never a regex
        Assert.DoesNotMatch(Mongoish(SegmentPushdownRules.StringPattern("ist.nbul", anchored: true, turkishInsensitive: true)), stored);
    }

    [Theory]
    [InlineData("İstanbul Bölge Modeli")]
    [InlineData("istanbul bölge modeli")]
    [InlineData("ISTANBUL BÖLGE MODELİ")]
    public void Territory_model_search_matches_every_turkish_i_spelling(string stored)
    {
        foreach (var term in new[] { "İstanbul", "istanbul", "ISTANBUL" })
        {
            Assert.Matches(Mongoish(TurkishInsensitivePattern.Build(term)), stored);
        }

        var source = File.ReadAllText(RepoFile(
            "services", "Diten.CrmService", "src", "Diten.CrmService.Persistence", "Repositories", "TerritoryModelRepository.cs"));
        Assert.Equal(2, Regex.Matches(source, @"TurkishInsensitivePattern\.Build\(").Count); // search + country scope
        Assert.DoesNotContain("new MongoDB.Bson.BsonRegularExpression(term, \"i\")) |", source.Replace("\r", string.Empty)
            .Split('\n').Where(l => l.Contains("var term = search.Trim()")).DefaultIfEmpty(string.Empty).First());
        var candidateSource = File.ReadAllText(RepoFile(
            "services", "Diten.CrmService", "src", "Diten.CrmService.Persistence", "Repositories", "SegmentCandidateSource.cs"));
        Assert.Contains("SegmentPushdownRules.StringPattern(", candidateSource);
        Assert.DoesNotContain("Regex.Escape(", candidateSource);
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The Mongo option <c>i</c> as .NET sees it (case-insensitive, culture-invariant).</summary>
    private static Regex Mongoish(string pattern) => new(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static SegmentCriteriaNode Node(string code, string op, string type, string value, bool negate = false)
        => B.Criteria(B.Predicate(code, op, type, new[] { value }, negate: negate))[0];

    private static TenantContext TenantCtx()
    {
        var t = new TenantContext();
        t.SetTenant(Tenant);
        return t;
    }

    private static string RepoFile(params string[] parts)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "services", "Diten.CrmService")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(new[] { dir?.FullName ?? throw new InvalidOperationException("repo root not found") }.Concat(parts).ToArray());
    }
}
