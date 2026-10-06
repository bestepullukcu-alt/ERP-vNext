using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Segmentation;
using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Queries;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Resolve;
using Diten.CrmService.Application.Tests.Segmentation;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Xunit;
using Vfp = Diten.CrmService.Domain.Entities.VisitFrequencyPolicy;

namespace Diten.CrmService.Application.Tests;

/// <summary>
/// WP-FREQ-DET-P (MOD-0165-FU03, FAZ 1) — resolving a <c>contact</c> target derives that contact's ACTIVE segment
/// memberships server-side and applies the segment-scoped policies automatically. Pins down: a member of an active
/// segment resolves that segment's policy; a non-member stays unknown; a draft (inactive) segment is never probed and
/// never auto-applied; two memberships produce two candidates with a deterministic winner; membership is read ONLY
/// through the PII-safe <see cref="ISegmentMembershipReader"/>; and — the backward-compat contract — a resolver built
/// without the derivation seams (or resolving a non-contact target) behaves exactly as the single-segment engine did.
/// </summary>
public sealed class VisitFrequencyPolicyContactSegmentResolveTests
{
    private static readonly Guid TenantA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly DateTimeOffset Jan1 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Jun1 = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    private static TenantContext Tenant(Guid id)
    {
        var ctx = new TenantContext();
        ctx.SetTenant(id);
        return ctx;
    }

    // ---------------- FAZ 1 — contact → active segment derivation ----------------

    [Fact]
    public async Task Contact_Member_Of_Active_Segment_Resolves_Its_Segment_Policy()
    {
        var contact = Guid.NewGuid();
        var segId = Guid.NewGuid();

        var segments = new FakeSegmentRepository();
        segments.Rows.Add(ActiveContactSegment(segId, "SEG-A"));
        // A second active segment the contact is NOT a member of, to prove the filter drops non-members.
        var otherSeg = Guid.NewGuid();
        segments.Rows.Add(ActiveContactSegment(otherSeg, "SEG-B"));

        var membership = new FakeMembership();
        membership.MemberSegments.Add(segId);

        var vfp = new FakeVfpRepo();
        vfp.Items.Add(SegmentPolicy("SEG-POL", segId, priority: 200));
        vfp.Items.Add(SegmentPolicy("OTHER-POL", otherSeg, priority: 100)); // stronger, but contact is not a member

        var resolver = new VisitFrequencyPolicyResolver(Tenant(TenantA), vfp, segments, membership);
        var r = await resolver.ResolveAsync(Query(contact), default);

        Assert.Equal(FrequencyStatus.Resolved, r.FrequencyStatus);
        Assert.Equal("SEG-POL", r.SelectedPolicyCode);
        Assert.Equal(2, r.RequiredVisitCount);
        // Only the segment the contact actually belongs to was applied — the stronger non-member policy never entered.
        Assert.DoesNotContain(r.CandidatePolicies, c => c.PolicyCode == "OTHER-POL");
    }

    [Fact]
    public async Task Contact_Not_Member_Of_Any_Segment_Is_Unknown()
    {
        var contact = Guid.NewGuid();
        var segId = Guid.NewGuid();

        var segments = new FakeSegmentRepository();
        segments.Rows.Add(ActiveContactSegment(segId, "SEG-A"));

        var membership = new FakeMembership(); // no members configured → NotMember for everything

        var vfp = new FakeVfpRepo();
        vfp.Items.Add(SegmentPolicy("SEG-POL", segId, priority: 200));

        var resolver = new VisitFrequencyPolicyResolver(Tenant(TenantA), vfp, segments, membership);
        var r = await resolver.ResolveAsync(Query(contact), default);

        Assert.Equal(FrequencyStatus.Unknown, r.FrequencyStatus);
        Assert.Null(r.SelectedFrequencyPolicyId);
        Assert.Contains(FrequencyReasonCodes.NoMatchingPolicy, r.ReasonCodes);
    }

    [Fact]
    public async Task Draft_Segment_Is_Never_Probed_And_Never_Auto_Applied()
    {
        var contact = Guid.NewGuid();
        var draftSeg = Guid.NewGuid();

        var segments = new FakeSegmentRepository();
        segments.Rows.Add(DraftContactSegment(draftSeg, "SEG-DRAFT"));

        // Even if the contact WOULD be a member, a draft (inactive) segment must never be applied.
        var membership = new FakeMembership();
        membership.MemberSegments.Add(draftSeg);

        var vfp = new FakeVfpRepo();
        vfp.Items.Add(SegmentPolicy("DRAFT-POL", draftSeg, priority: 200));

        var resolver = new VisitFrequencyPolicyResolver(Tenant(TenantA), vfp, segments, membership);
        var r = await resolver.ResolveAsync(Query(contact), default);

        Assert.Equal(FrequencyStatus.Unknown, r.FrequencyStatus);
        // The inactive segment is filtered out at the resolver, so the PII-safe reader is never even asked about it.
        Assert.DoesNotContain(draftSeg, membership.Probed);
    }

    [Fact]
    public async Task Contact_In_Two_Active_Segments_Yields_Two_Candidates_With_Deterministic_Winner()
    {
        var contact = Guid.NewGuid();
        var seg1 = Guid.NewGuid();
        var seg2 = Guid.NewGuid();

        var segments = new FakeSegmentRepository();
        segments.Rows.Add(ActiveContactSegment(seg1, "SEG-1"));
        segments.Rows.Add(ActiveContactSegment(seg2, "SEG-2"));

        var membership = new FakeMembership();
        membership.MemberSegments.Add(seg1);
        membership.MemberSegments.Add(seg2);

        var vfp = new FakeVfpRepo();
        vfp.Items.Add(SegmentPolicy("SEG-1-POL", seg1, priority: 100)); // lower priority wins
        vfp.Items.Add(SegmentPolicy("SEG-2-POL", seg2, priority: 300));

        var resolver = new VisitFrequencyPolicyResolver(Tenant(TenantA), vfp, segments, membership);
        var r = await resolver.ResolveAsync(Query(contact), default);

        Assert.Equal(FrequencyStatus.Resolved, r.FrequencyStatus);
        Assert.Equal("SEG-1-POL", r.SelectedPolicyCode);
        Assert.Contains(FrequencyReasonCodes.PolicySelectedByPriority, r.ReasonCodes);
        // Both memberships are visible as candidates (winner + runner-up), not just the winner.
        Assert.Equal(2, r.CandidatePolicies.Count);
        Assert.Contains(r.CandidatePolicies, c => c.PolicyCode == "SEG-2-POL" && !c.Selected);
    }

    // ---------------- Backward-compat (single-segment context stays birebir) ----------------

    [Fact]
    public async Task Resolver_Without_Derivation_Seams_Does_Not_Derive_Contact_Segments()
    {
        var contact = Guid.NewGuid();
        var segId = Guid.NewGuid();

        // The membership WOULD say member, but a resolver built with the legacy (tenant, repo) ctor has no reader,
        // so derivation is disabled and only explicit context ids are honoured — exactly the pre-WP behaviour.
        var vfp = new FakeVfpRepo();
        vfp.Items.Add(SegmentPolicy("SEG-POL", segId, priority: 200));

        var resolver = new VisitFrequencyPolicyResolver(Tenant(TenantA), vfp); // 2-arg legacy ctor
        var r = await resolver.ResolveAsync(Query(contact), default);

        Assert.Equal(FrequencyStatus.Unknown, r.FrequencyStatus);
    }

    [Fact]
    public async Task Explicit_SegmentId_Context_Still_Resolves_A_Segment_Policy_As_Before()
    {
        var segId = Guid.NewGuid();

        var vfp = new FakeVfpRepo();
        vfp.Items.Add(SegmentPolicy("SEG-POL", segId, priority: 200));

        // Non-contact resolve carrying an explicit SegmentId — the single-element context default. No derivation runs.
        var resolver = new VisitFrequencyPolicyResolver(
            Tenant(TenantA), vfp, new FakeSegmentRepository(), new FakeMembership());
        var r = await resolver.ResolveAsync(
            new ResolveVisitFrequencyPolicyQuery(FrequencyTargetType.Segment, segId, Jun1, SegmentId: segId), default);

        Assert.Equal(FrequencyStatus.Resolved, r.FrequencyStatus);
        Assert.Equal("SEG-POL", r.SelectedPolicyCode);
    }

    // ---------------- Builders / fakes ----------------

    private static ResolveVisitFrequencyPolicyQuery Query(Guid contactId)
        => new(FrequencyTargetType.Contact, contactId, Jun1);

    private static Segment ActiveContactSegment(Guid id, string code) => new()
    {
        Id = id,
        TenantId = TenantA,
        SegmentCode = code,
        SegmentName = code,
        SubjectType = SegmentSubjectTypes.Contact,
        SegmentStatus = SegmentStatuses.Active,
        EffectiveFrom = Jan1
    };

    private static Segment DraftContactSegment(Guid id, string code) => new()
    {
        Id = id,
        TenantId = TenantA,
        SegmentCode = code,
        SegmentName = code,
        SubjectType = SegmentSubjectTypes.Contact,
        SegmentStatus = SegmentStatuses.Draft,
        EffectiveFrom = Jan1
    };

    private static Vfp SegmentPolicy(string code, Guid segmentId, int priority) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = TenantA,
        PolicyCode = code,
        PolicyName = "Policy " + code,
        TargetType = FrequencyTargetType.Segment,
        TargetId = segmentId,
        SegmentId = segmentId,
        FrequencyType = FrequencyType.Monthly,
        RequiredVisitCount = 2,
        PeriodType = FrequencyPeriodType.Month,
        EffectiveFrom = Jan1,
        Priority = priority,
        Source = FrequencySource.Segmentation,
        Status = FrequencyPolicyStatus.Active
    };

    /// <summary>Membership reader double. <see cref="MemberSegments"/> resolve to <c>member</c>, everything else to
    /// <c>not-member</c>; <see cref="Probed"/> records exactly which segment ids the resolver asked about, so "a draft
    /// segment is never probed" is provable rather than asserted indirectly.</summary>
    private sealed class FakeMembership : ISegmentMembershipReader
    {
        public HashSet<Guid> MemberSegments { get; } = new();
        public List<Guid> Probed { get; } = new();

        public Task<SegmentMembershipVerdict> IsMemberAsync(
            Guid segmentId, string subjectType, Guid subjectId, DateTimeOffset effectiveAt, CancellationToken ct)
        {
            Probed.Add(segmentId);
            var verdict = MemberSegments.Contains(segmentId)
                ? SegmentMembershipVerdicts.Member
                : SegmentMembershipVerdicts.NotMember;
            return Task.FromResult(new SegmentMembershipVerdict(
                segmentId, 1, subjectType, subjectId, verdict, Array.Empty<string>(), effectiveAt));
        }

        public Task<SegmentResolutionResult> ResolveAsync(
            Guid segmentId, DateTimeOffset effectiveAt, int limit, int offset, CancellationToken ct)
            => throw new NotSupportedException("Resolve is not used by the frequency resolver.");
    }

    private sealed class FakeVfpRepo : IVisitFrequencyPolicyRepository
    {
        public List<Vfp> Items { get; } = new();

        public Task<Vfp?> GetByIdAsync(Guid t, Guid id, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(p => p.TenantId == t && p.Id == id && !p.IsDeleted));

        public Task<IReadOnlyList<Vfp>> ListAsync(Guid t, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Vfp>)Items.Where(p => p.TenantId == t && !p.IsDeleted).ToList());

        public Task<Vfp?> GetActiveByCodeAsync(Guid t, string code, CancellationToken ct)
            => Task.FromResult(Items.FirstOrDefault(p =>
                p.TenantId == t && !p.IsDeleted && p.PolicyCode == code && p.Status != FrequencyPolicyStatus.Archived));

        public Task<IReadOnlyList<Vfp>> ListActiveByTargetsAsync(
            Guid t, IReadOnlyCollection<Guid> targetIds, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<Vfp>)Items.Where(p =>
                p.TenantId == t && !p.IsDeleted && p.Status == FrequencyPolicyStatus.Active
                && targetIds.Contains(p.TargetId)).ToList());

        public Task InsertAsync(Vfp policy, CancellationToken ct) { Items.Add(policy); return Task.CompletedTask; }

        public Task UpdateAsync(Vfp policy, CancellationToken ct) => Task.CompletedTask;
    }
}
