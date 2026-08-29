using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Segmentation;
using Diten.CrmService.Application.Features.Segmentation.Catalog;
using Diten.CrmService.Application.Features.Segmentation.Handlers.QueryHandlers;
using Diten.CrmService.Application.Features.Segmentation.Queries;
using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Domain.Entities;
using Xunit;

namespace Diten.CrmService.Application.Tests.Segmentation;

/// <summary>
/// MOD-0167 FU02 — the membership engine. These are the tests that hold the FU together: determinism, the
/// static / dynamic / hybrid formula, the fail-closed asymmetry, the N+1 ban and the "resolve persists nothing" rule.
/// </summary>
public sealed class SegmentMembershipResolverTests
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

    private Segment SeedSegment(
        string type = SegmentTypes.Dynamic,
        List<SegmentCriteriaNode>? criteria = null,
        DateTimeOffset? effectiveFrom = null,
        DateTimeOffset? effectiveTo = null,
        string status = SegmentStatuses.Active,
        string matchMode = SegmentMatchModes.All)
    {
        var segment = SegmentTestBuilders.Segment(
            Tenant, type: type,
            criteria: criteria ?? SegmentTestBuilders.Criteria(
                SegmentTestBuilders.Predicate(
                    SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.Eq, SegmentValueTypes.String,
                    new[] { "cardiology" })),
            effectiveFrom: effectiveFrom, effectiveTo: effectiveTo, status: status, matchMode: matchMode);

        if (type == SegmentTypes.Static)
        {
            segment.Criteria = new List<SegmentCriteriaNode>();
        }

        _segments.Rows.Add(segment);
        return segment;
    }

    [Fact]
    public async Task Three_consecutive_resolutions_return_the_same_members_in_the_same_order_with_the_same_reasons()
    {
        var segment = SeedSegment();
        for (var i = 0; i < 40; i++)
        {
            _candidates.Candidates.Add(SegmentTestBuilders.Contact(
                Guid.NewGuid(), specialty: i % 2 == 0 ? "cardiology" : "oncology"));
        }

        var runs = new List<string>();
        for (var run = 0; run < 3; run++)
        {
            var outcome = await Resolver().ResolveAsync(
                Tenant, segment, SegmentTestDoubles.Now, 1000, 0, includeExcluded: true, default);

            runs.Add(string.Join("|", outcome.Result!.Members
                .Select(m => $"{m.SubjectId}:{string.Join(",", m.ReasonCodes)}")));
        }

        Assert.Equal(runs[0], runs[1]);
        Assert.Equal(runs[1], runs[2]);
    }

    [Fact]
    public async Task Members_are_ordered_by_subject_id_and_by_nothing_else()
    {
        var segment = SeedSegment();
        for (var i = 0; i < 25; i++)
        {
            _candidates.Candidates.Add(SegmentTestBuilders.Contact(Guid.NewGuid(), specialty: "cardiology"));
        }

        var outcome = await Resolver().ResolveAsync(
            Tenant, segment, SegmentTestDoubles.Now, 1000, 0, includeExcluded: false, default);

        var ids = outcome.Result!.Members.Select(m => m.SubjectId).ToList();
        Assert.Equal(ids.OrderBy(id => id).ToList(), ids);
    }

    [Fact]
    public async Task Accepted_plus_eliminated_always_equals_the_candidate_count_and_no_one_drops_out_silently()
    {
        var segment = SeedSegment();
        for (var i = 0; i < 30; i++)
        {
            _candidates.Candidates.Add(SegmentTestBuilders.Contact(
                Guid.NewGuid(), specialty: i % 3 == 0 ? "cardiology" : "oncology"));
        }

        var outcome = await Resolver().ResolveAsync(
            Tenant, segment, SegmentTestDoubles.Now, 1000, 0, includeExcluded: true, default);

        var result = outcome.Result!;
        Assert.Equal(result.CandidateCount, result.MatchedCount + result.ExcludedCount);
        Assert.Equal(30, result.CandidateCount);
        Assert.All(result.Excluded, e => Assert.NotEmpty(e.ReasonCodes));
    }

    [Fact]
    public async Task A_static_segment_never_invokes_the_criteria_engine_at_all()
    {
        var segment = SeedSegment(SegmentTypes.Static);
        var member = Guid.NewGuid();
        _targets.Rows.Add(SegmentTestBuilders.Manual(
            Tenant, segment.Id, member, SegmentMembershipModes.ManualInclude));

        var outcome = await Resolver().ResolveAsync(
            Tenant, segment, SegmentTestDoubles.Now, 1000, 0, includeExcluded: true, default);

        Assert.Equal(0, _candidates.LoadCandidatesCalls);
        Assert.Equal(member, Assert.Single(outcome.Result!.Members).SubjectId);
    }

    [Fact]
    public async Task Hybrid_adds_a_manual_include_and_a_manual_exclude_definitively_removes_a_rule_match()
    {
        var segment = SeedSegment(SegmentTypes.Hybrid);
        var matching = Guid.NewGuid();
        var excluded = Guid.NewGuid();
        var invited = Guid.NewGuid();

        _candidates.Candidates.Add(SegmentTestBuilders.Contact(matching, specialty: "cardiology"));
        _candidates.Candidates.Add(SegmentTestBuilders.Contact(excluded, specialty: "cardiology"));

        _targets.Rows.Add(SegmentTestBuilders.Manual(
            Tenant, segment.Id, excluded, SegmentMembershipModes.ManualExclude));
        _targets.Rows.Add(SegmentTestBuilders.Manual(
            Tenant, segment.Id, invited, SegmentMembershipModes.ManualInclude));

        var outcome = await Resolver().ResolveAsync(
            Tenant, segment, SegmentTestDoubles.Now, 1000, 0, includeExcluded: true, default);

        var members = outcome.Result!.Members.Select(m => m.SubjectId).ToList();
        Assert.Contains(matching, members);
        Assert.Contains(invited, members);
        Assert.DoesNotContain(excluded, members);

        var removed = outcome.Result.Excluded.Single(e => e.SubjectId == excluded);
        Assert.Contains(SegmentReasonCodes.ManualExclude, removed.ReasonCodes);
        Assert.Contains(
            SegmentReasonCodes.ManualInclude,
            outcome.Result.Members.Single(m => m.SubjectId == invited).ReasonCodes);
    }

    [Fact]
    public async Task Outside_its_effective_window_a_segment_answers_with_an_empty_explained_result_not_a_404()
    {
        var segment = SeedSegment(effectiveFrom: SegmentTestDoubles.Future);

        var outcome = await Resolver().ResolveAsync(
            Tenant, segment, SegmentTestDoubles.Now, 1000, 0, includeExcluded: true, default);

        Assert.False(outcome.CandidateCapExceeded);
        Assert.Empty(outcome.Result!.Members);
        Assert.False(outcome.Result.SegmentEffective);
        Assert.Contains(SegmentReasonCodes.OutsideEffectiveWindow, outcome.Result.ReasonCodes);
        Assert.Equal(0, _candidates.LoadCandidatesCalls);
    }

    [Fact]
    public async Task A_draft_segment_resolves_to_an_empty_result_explaining_that_it_is_not_active()
    {
        var segment = SeedSegment(status: SegmentStatuses.Draft);

        var outcome = await Resolver().ResolveAsync(
            Tenant, segment, SegmentTestDoubles.Now, 1000, 0, includeExcluded: true, default);

        Assert.Contains(SegmentReasonCodes.SegmentNotActive, outcome.Result!.ReasonCodes);
    }

    [Fact]
    public async Task Consent_unknown_eliminates_the_candidate_and_is_never_treated_as_allowed()
    {
        var criteria = SegmentTestBuilders.Criteria(SegmentTestBuilders.Predicate(
            SegmentAttributeCatalog.ConsentEligibility, SegmentOperators.Eq, SegmentValueTypes.String,
            new[] { "allowed" },
            parameters: new Dictionary<string, string>
            {
                ["channel"] = ConsentChannel.Email, ["purpose"] = ConsentPurpose.Marketing
            }));

        var segment = SeedSegment(criteria: criteria);
        var subject = Guid.NewGuid();
        _candidates.Candidates.Add(SegmentTestBuilders.Contact(subject, specialty: "cardiology"));

        // No consent row at all: the MOD-0164 engine answers unknown, which is NOT allowed.
        var outcome = await Resolver().ResolveAsync(
            Tenant, segment, SegmentTestDoubles.Now, 1000, 0, includeExcluded: true, default);

        Assert.Empty(outcome.Result!.Members);
        Assert.Contains(
            SegmentReasonCodes.ConsentUnknown,
            outcome.Result.Excluded.Single().ReasonCodes);
    }

    [Fact]
    public async Task Consent_granted_admits_the_candidate_through_the_MOD_0164_engine()
    {
        var criteria = SegmentTestBuilders.Criteria(SegmentTestBuilders.Predicate(
            SegmentAttributeCatalog.ConsentEligibility, SegmentOperators.Eq, SegmentValueTypes.String,
            new[] { "allowed" },
            parameters: new Dictionary<string, string>
            {
                ["channel"] = ConsentChannel.Email, ["purpose"] = ConsentPurpose.Marketing
            }));

        var segment = SeedSegment(criteria: criteria);
        var subject = Guid.NewGuid();
        _candidates.Candidates.Add(SegmentTestBuilders.Contact(subject, specialty: "cardiology"));
        _consent.Consents.Add(new ConsentRecord
        {
            TenantId = Tenant,
            SubjectType = SegmentSubjectTypes.Contact,
            SubjectId = subject,
            Channel = ConsentChannel.Email,
            Purpose = ConsentPurpose.Marketing,
            ConsentStatus = ConsentStatuses.Granted,
            LegalBasis = ConsentLegalBasis.ExplicitConsent,
            Source = ConsentSource.SubjectDeclared,
            EffectiveFrom = SegmentTestDoubles.Past
        });

        var outcome = await Resolver().ResolveAsync(
            Tenant, segment, SegmentTestDoubles.Now, 1000, 0, includeExcluded: true, default);

        Assert.Equal(subject, Assert.Single(outcome.Result!.Members).SubjectId);
    }

    [Fact]
    public async Task No_valid_territory_model_eliminates_the_candidate_but_the_resolution_still_completes()
    {
        var criteria = SegmentTestBuilders.Criteria(SegmentTestBuilders.Predicate(
            SegmentAttributeCatalog.TerritoryHasCoverage, SegmentOperators.Eq, SegmentValueTypes.Bool,
            new[] { "true" }));

        var segment = SeedSegment(criteria: criteria);
        _candidates.Candidates.Add(SegmentTestBuilders.Contact(Guid.NewGuid(), specialty: "cardiology"));
        _territory.CoverageAvailable = false;

        var outcome = await Resolver().ResolveAsync(
            Tenant, segment, SegmentTestDoubles.Now, 1000, 0, includeExcluded: true, default);

        // In-service degradation is an ANSWER, not a dependency failure: no exception, no 503, a completed result.
        Assert.NotNull(outcome.Result);
        Assert.Empty(outcome.Result!.Members);
        Assert.Contains(
            SegmentReasonCodes.TerritoryCoverageUnavailable,
            outcome.Result.Excluded.Single().ReasonCodes);
    }

    [Fact]
    public async Task Every_derived_source_is_read_exactly_once_no_matter_how_many_candidates_there_are()
    {
        var groupId = Guid.NewGuid();
        var criteria = SegmentTestBuilders.Criteria(
            SegmentTestBuilders.Group(SegmentGroupOperators.And, groupId),
            SegmentTestBuilders.Predicate(
                SegmentAttributeCatalog.ConsentEligibility, SegmentOperators.Eq, SegmentValueTypes.String,
                new[] { "allowed" }, parentNodeId: groupId, sortOrder: 0,
                parameters: new Dictionary<string, string>
                {
                    ["channel"] = ConsentChannel.Email, ["purpose"] = ConsentPurpose.Marketing
                }),
            SegmentTestBuilders.Predicate(
                SegmentAttributeCatalog.TerritoryHasCoverage, SegmentOperators.Eq, SegmentValueTypes.Bool,
                new[] { "true" }, parentNodeId: groupId, sortOrder: 1),
            SegmentTestBuilders.Predicate(
                SegmentAttributeCatalog.ContactAccountRole, SegmentOperators.Eq, SegmentValueTypes.String,
                new[] { "primary" }, parentNodeId: groupId, sortOrder: 2));

        var segment = SeedSegment(criteria: criteria);
        for (var i = 0; i < 500; i++)
        {
            _candidates.Candidates.Add(SegmentTestBuilders.Contact(Guid.NewGuid(), specialty: "cardiology"));
        }

        await Resolver().ResolveAsync(
            Tenant, segment, SegmentTestDoubles.Now, 1000, 0, includeExcluded: true, default);

        Assert.Equal(1, _candidates.LoadCandidatesCalls);
        Assert.Equal(1, _candidates.LoadLinksCalls);
        Assert.True(_consent.Calls <= 2, $"consent reader called {_consent.Calls} times for 500 candidates");
        Assert.True(_territory.Calls <= 2, $"territory reader called {_territory.Calls} times for 500 candidates");
    }

    [Fact]
    public async Task Every_reported_subject_carries_a_display_name_and_it_costs_no_extra_read()
    {
        var segment = SeedSegment();
        var matching = Guid.NewGuid();
        var missing = Guid.NewGuid();
        _candidates.Candidates.Add(SegmentTestBuilders.Contact(
            matching, specialty: "cardiology", displayName: "Dr Ada Lovelace"));
        _candidates.Candidates.Add(SegmentTestBuilders.Contact(
            missing, specialty: "oncology", displayName: "Dr Alan Turing"));

        var outcome = await Resolver().ResolveAsync(
            Tenant, segment, SegmentTestDoubles.Now, 1000, 0, includeExcluded: true, default);

        Assert.Equal("Dr Ada Lovelace", Assert.Single(outcome.Result!.Members).SubjectDisplayName);
        // An ELIMINATED candidate is named too: it was already scanned, so hiding its name would only make the
        // explanation harder to read.
        Assert.Equal("Dr Alan Turing", Assert.Single(outcome.Result.Excluded).SubjectDisplayName);

        // The label rides on the candidate projection. One pushdown, and no per-subject lookup was added for it.
        Assert.Equal(1, _candidates.LoadCandidatesCalls);
        Assert.Equal(0, _candidates.LoadSubjectsCalls);
    }

    [Fact]
    public async Task A_manual_row_outside_the_candidate_set_is_named_from_the_row_itself()
    {
        var segment = SeedSegment(SegmentTypes.Hybrid);
        var invited = Guid.NewGuid();
        _targets.Rows.Add(SegmentTestBuilders.Manual(
            Tenant, segment.Id, invited, SegmentMembershipModes.ManualInclude, displayName: "Board pick"));

        var outcome = await Resolver().ResolveAsync(
            Tenant, segment, SegmentTestDoubles.Now, 1000, 0, includeExcluded: true, default);

        var member = Assert.Single(outcome.Result!.Members);
        Assert.Equal(invited, member.SubjectId);
        Assert.Equal("Board pick", member.SubjectDisplayName);
        // Still no lookup: the manual row already stored the label.
        Assert.Equal(0, _candidates.LoadSubjectsCalls);
    }

    [Fact]
    public async Task Is_member_reports_the_display_name_from_the_snapshot_it_already_loaded()
    {
        var segment = SeedSegment();
        var member = Guid.NewGuid();
        _candidates.Candidates.Add(SegmentTestBuilders.Contact(
            member, specialty: "cardiology", displayName: "Dr Grace Hopper"));

        var verdict = await Resolver().EvaluateAsync(
            Tenant, segment, SegmentSubjectTypes.Contact, member, SegmentTestDoubles.Now, default);

        Assert.Equal(SegmentMembershipVerdicts.Member, verdict.Verdict);
        Assert.Equal("Dr Grace Hopper", verdict.SubjectDisplayName);
        Assert.Equal(1, _candidates.LoadSubjectsCalls);
    }

    [Fact]
    public async Task Past_the_candidate_ceiling_nothing_is_returned_and_the_endpoint_answers_422()
    {
        var segment = SeedSegment();
        _candidates.ForceCapExceeded = true;

        var outcome = await Resolver().ResolveAsync(
            Tenant, segment, SegmentTestDoubles.Now, 1000, 0, includeExcluded: true, default);

        Assert.True(outcome.CandidateCapExceeded);
        Assert.Null(outcome.Result);

        var response = await new ResolveSegmentMembershipHandler(
                SegmentTestDoubles.Tenant(Tenant), _segments, Resolver())
            .Handle(new ResolveSegmentMembershipQuery(segment.Id, SegmentTestDoubles.Now, 100, 0, true), default);

        Assert.Equal(422, response.StatusCode);
        Assert.Contains(SegmentErrorCodes.CandidateSetTooLarge, response.Errors!);
        Assert.Null(response.Data);
    }

    [Fact]
    public async Task Resolving_changes_no_stored_document_whatsoever()
    {
        var segment = SeedSegment(SegmentTypes.Hybrid);
        _candidates.Candidates.Add(SegmentTestBuilders.Contact(Guid.NewGuid(), specialty: "cardiology"));
        _targets.Rows.Add(SegmentTestBuilders.Manual(
            Tenant, segment.Id, Guid.NewGuid(), SegmentMembershipModes.ManualInclude));

        var segmentsBefore = _segments.Rows.Select(s => (s.Id, s.Version, s.UpdatedAt)).ToList();
        var targetsBefore = _targets.Rows.Select(t => (t.Id, t.Version, t.UpdatedAt)).ToList();

        await Resolver().ResolveAsync(
            Tenant, segment, SegmentTestDoubles.Now, 1000, 0, includeExcluded: true, default);

        Assert.Equal(segmentsBefore, _segments.Rows.Select(s => (s.Id, s.Version, s.UpdatedAt)).ToList());
        Assert.Equal(targetsBefore, _targets.Rows.Select(t => (t.Id, t.Version, t.UpdatedAt)).ToList());
    }

    [Fact]
    public async Task A_superseded_version_still_resolves_and_says_so()
    {
        var segment = SeedSegment();
        segment.SupersededBySegmentId = Guid.NewGuid();
        _candidates.Candidates.Add(SegmentTestBuilders.Contact(Guid.NewGuid(), specialty: "cardiology"));

        var outcome = await Resolver().ResolveAsync(
            Tenant, segment, SegmentTestDoubles.Now, 1000, 0, includeExcluded: false, default);

        Assert.True(outcome.Result!.Superseded);
        Assert.Single(outcome.Result.Members);
    }

    [Fact]
    public async Task Is_member_answers_member_not_member_and_unknown_and_unknown_is_never_member()
    {
        var segment = SeedSegment();
        var member = Guid.NewGuid();
        var stranger = Guid.NewGuid();
        _candidates.Candidates.Add(SegmentTestBuilders.Contact(member, specialty: "cardiology"));
        _candidates.Candidates.Add(SegmentTestBuilders.Contact(stranger, specialty: "oncology"));

        var resolver = Resolver();

        var yes = await resolver.EvaluateAsync(
            Tenant, segment, SegmentSubjectTypes.Contact, member, SegmentTestDoubles.Now, default);
        Assert.Equal(SegmentMembershipVerdicts.Member, yes.Verdict);

        var no = await resolver.EvaluateAsync(
            Tenant, segment, SegmentSubjectTypes.Contact, stranger, SegmentTestDoubles.Now, default);
        Assert.Equal(SegmentMembershipVerdicts.NotMember, no.Verdict);

        // A subject that is not visible in this tenant is unknown - and unknown is never member.
        var invisible = await resolver.EvaluateAsync(
            Tenant, segment, SegmentSubjectTypes.Contact, Guid.NewGuid(), SegmentTestDoubles.Now, default);
        Assert.Equal(SegmentMembershipVerdicts.Unknown, invisible.Verdict);
        Assert.NotEqual(SegmentMembershipVerdicts.Member, invisible.Verdict);
    }

    [Fact]
    public async Task Match_mode_any_is_an_or_over_the_root_children()
    {
        var criteria = SegmentTestBuilders.Criteria(
            SegmentTestBuilders.Predicate(
                SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.Eq, SegmentValueTypes.String,
                new[] { "cardiology" }, sortOrder: 0),
            SegmentTestBuilders.Predicate(
                SegmentAttributeCatalog.ContactSpecialty, SegmentOperators.Eq, SegmentValueTypes.String,
                new[] { "oncology" }, sortOrder: 1));

        var segment = SeedSegment(criteria: criteria, matchMode: SegmentMatchModes.Any);
        _candidates.Candidates.Add(SegmentTestBuilders.Contact(Guid.NewGuid(), specialty: "oncology"));

        var outcome = await Resolver().ResolveAsync(
            Tenant, segment, SegmentTestDoubles.Now, 1000, 0, includeExcluded: false, default);

        Assert.Single(outcome.Result!.Members);
    }

    [Fact]
    public async Task The_read_only_seam_reports_the_same_verdict_as_the_endpoint_and_writes_nothing()
    {
        var segment = SeedSegment();
        var member = Guid.NewGuid();
        _candidates.Candidates.Add(SegmentTestBuilders.Contact(member, specialty: "cardiology"));

        var reader = new SegmentMembershipReader(
            SegmentTestDoubles.Tenant(Tenant), _segments, Resolver());

        var verdict = await reader.IsMemberAsync(
            segment.Id, SegmentSubjectTypes.Contact, member, SegmentTestDoubles.Now, default);

        Assert.True(verdict.IsMember);
        Assert.False(verdict.IsUnknown);

        var mismatched = await reader.IsMemberAsync(
            segment.Id, SegmentSubjectTypes.Account, member, SegmentTestDoubles.Now, default);
        Assert.True(mismatched.IsUnknown);
        Assert.False(mismatched.IsMember);
    }
}
