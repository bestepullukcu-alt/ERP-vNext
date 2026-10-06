using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using Vfp = Diten.CrmService.Domain.Entities.VisitFrequencyPolicy;

namespace Diten.CrmService.Application.Features.VisitFrequencyPolicy.Resolve;

/// <summary>
/// MOD-0165 FU03 read-only resolve seam. This is the <b>single source of truth</b> for frequency resolution: it loads
/// the active candidate policies for the requested target (primary + caller-supplied context ids) and runs the
/// deterministic <see cref="VisitFrequencyResolveEngine"/>. Both the FU03 HTTP endpoint (via its query handler) and
/// in-process consumers (e.g. MOD-0151 FU09B route-candidate readiness) call THIS — no consumer re-implements or
/// copies the engine, and there is no HTTP self-call back through the Gateway. The resolver performs no writes.
/// </summary>
public interface IVisitFrequencyPolicyResolver
{
    Task<VisitFrequencyResolveResult> ResolveAsync(ResolveVisitFrequencyPolicyQuery request, CancellationToken cancellationToken);
}

public sealed class VisitFrequencyPolicyResolver : IVisitFrequencyPolicyResolver
{
    /// <summary>WP-FREQ-DET-P — upper bound on how many active contact segments a single resolve probes for membership.
    /// Beyond it derivation is capped (not aborted): the explicit request.SegmentId context still resolves. Membership
    /// is one bounded read per segment, so this keeps a contact resolve O(cap) even in a tenant with many segments.</summary>
    private const int MaxSegmentsToProbe = 200;

    private readonly ITenantContext _tenant;
    private readonly IVisitFrequencyPolicyRepository _repository;
    private readonly ISegmentRepository? _segments;
    private readonly ISegmentMembershipReader? _membership;

    public VisitFrequencyPolicyResolver(
        ITenantContext tenant,
        IVisitFrequencyPolicyRepository repository,
        ISegmentRepository? segments = null,
        ISegmentMembershipReader? membership = null)
    {
        _tenant = tenant;
        _repository = repository;
        _segments = segments;
        _membership = membership;
    }

    public async Task<VisitFrequencyResolveResult> ResolveAsync(
        ResolveVisitFrequencyPolicyQuery request, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;

        // No tenant → resolve against zero candidates → deterministic "unknown" (never a fabricated default).
        if (_tenant.TenantId is not { } tenantId)
        {
            return VisitFrequencyResolveEngine.Resolve(request, Array.Empty<Vfp>(), now);
        }

        var targetIds = new List<Guid> { request.TargetId };
        AddId(targetIds, request.SegmentId);
        AddId(targetIds, request.TerritoryNodeId);
        AddId(targetIds, request.CampaignId);
        AddId(targetIds, request.ConceptNodeId);
        AddId(targetIds, request.AudienceProfileId);

        // Segment context = explicit request.SegmentId (single, back-compat default) ∪ any derived contact memberships.
        var segmentContext = new HashSet<Guid>();
        if (request.SegmentId is { } explicitSegment && explicitSegment != Guid.Empty)
        {
            segmentContext.Add(explicitSegment);
        }

        // WP-FREQ-DET-P FAZ 1 — a contact target derives its ACTIVE segment memberships server-side so segment-scoped
        // policies apply automatically. Membership is read ONLY through the PII-safe reader (draft/ineffective segments
        // resolve to unknown and are never counted). Other target types are untouched (single-context, birebir).
        if (_segments is not null && _membership is not null
            && FrequencyTargetType.Normalize(request.TargetType) == FrequencyTargetType.Contact
            && request.TargetId != Guid.Empty)
        {
            var effectiveAt = request.EffectiveAt ?? now;
            foreach (var derivedSegmentId in await DeriveContactSegmentsAsync(
                         tenantId, request.TargetId, effectiveAt, cancellationToken))
            {
                if (segmentContext.Add(derivedSegmentId))
                {
                    targetIds.Add(derivedSegmentId);
                }
            }
        }

        var candidates = await _repository.ListActiveByTargetsAsync(tenantId, targetIds, cancellationToken);
        return VisitFrequencyResolveEngine.Resolve(request, candidates, now, segmentContext);
    }

    /// <summary>Active contact-subject segments this contact is a member of at <paramref name="effectiveAt"/>. Only
    /// <see cref="Segment.IsActive"/> contact segments are probed, capped at <see cref="MaxSegmentsToProbe"/>; each is a
    /// single bounded, PII-safe membership read whose <c>member</c> verdict adds the id (unknown/not-member is skipped,
    /// so a draft or ineffective segment naturally drops out).</summary>
    private async Task<IReadOnlyList<Guid>> DeriveContactSegmentsAsync(
        Guid tenantId, Guid contactId, DateTimeOffset effectiveAt, CancellationToken cancellationToken)
    {
        var all = await _segments!.ListAsync(tenantId, cancellationToken);
        var active = all
            .Where(s => s.IsActive()
                && string.Equals(s.SubjectType, SegmentSubjectTypes.Contact, StringComparison.Ordinal))
            .Take(MaxSegmentsToProbe)
            .ToList();

        var derived = new List<Guid>();
        foreach (var segment in active)
        {
            var verdict = await _membership!.IsMemberAsync(
                segment.Id, SegmentSubjectTypes.Contact, contactId, effectiveAt, cancellationToken);
            if (verdict.IsMember)
            {
                derived.Add(segment.Id);
            }
        }

        return derived;
    }

    private static void AddId(ICollection<Guid> ids, Guid? id)
    {
        if (id is { } value && value != Guid.Empty)
        {
            ids.Add(value);
        }
    }
}
