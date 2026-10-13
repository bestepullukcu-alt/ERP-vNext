using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>
/// WP-FREQ-DET-P, shared by WP-VP-2 (B-3) — the ACTIVE contact segments a doctor is a member of at an instant. ONE
/// implementation for every consumer (frequency resolution, play derivation), so "which segments is this doctor in" can
/// never be answered two ways. Only <see cref="Segment.IsActive"/> contact-subject segments are probed, capped at
/// <see cref="MaxSegmentsToProbe"/>; each is a single bounded, PII-safe membership read whose <c>member</c> verdict adds
/// the id (unknown / not-member is skipped, so a draft or ineffective segment naturally drops out). Reads only.
/// </summary>
public static class ContactSegmentMemberships
{
    /// <summary>Upper bound on how many active contact segments one derivation probes. Beyond it derivation is capped
    /// (not aborted), which keeps a doctor's derivation O(cap) even in a tenant with many segments.</summary>
    public const int MaxSegmentsToProbe = 200;

    public static async Task<IReadOnlyList<Guid>> DeriveAsync(
        ISegmentRepository segments,
        ISegmentMembershipReader membership,
        Guid tenantId,
        Guid contactId,
        DateTimeOffset effectiveAt,
        CancellationToken cancellationToken)
    {
        if (contactId == Guid.Empty)
        {
            return Array.Empty<Guid>();
        }

        var all = await segments.ListAsync(tenantId, cancellationToken);
        var active = all
            .Where(s => s.IsActive()
                && string.Equals(s.SubjectType, SegmentSubjectTypes.Contact, StringComparison.Ordinal))
            .Take(MaxSegmentsToProbe)
            .ToList();

        var derived = new List<Guid>();
        foreach (var segment in active)
        {
            var verdict = await membership.IsMemberAsync(
                segment.Id, SegmentSubjectTypes.Contact, contactId, effectiveAt, cancellationToken);
            if (verdict.IsMember)
            {
                derived.Add(segment.Id);
            }
        }

        return derived;
    }

    /// <summary>
    /// WP-VP-3D — the same derivation for MANY doctors at once: the same active contact segments (same filter, same
    /// <see cref="MaxSegmentsToProbe"/> cap), each asked once for the whole contact set through
    /// <see cref="SegmentMembershipResolver.EvaluateManyAsync"/> (the single-subject decision code). Cost: one segment
    /// list read + a fixed number of reads PER SEGMENT — never one per doctor. A <c>member</c> verdict adds the id;
    /// unknown / not-member are skipped, exactly as in <see cref="DeriveAsync"/>. Reads only.
    /// </summary>
    public static async Task<ContactSegmentSet> DeriveManyAsync(
        ISegmentRepository segments,
        SegmentMembershipResolver resolver,
        Guid tenantId,
        IReadOnlyCollection<Guid> contactIds,
        DateTimeOffset effectiveAt,
        CancellationToken cancellationToken)
    {
        var ids = contactIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
        {
            return ContactSegmentSet.Empty;
        }

        var all = await segments.ListAsync(tenantId, cancellationToken);
        var active = all
            .Where(s => s.IsActive()
                && string.Equals(s.SubjectType, SegmentSubjectTypes.Contact, StringComparison.Ordinal))
            .Take(MaxSegmentsToProbe)
            .ToList();

        var byContact = ids.ToDictionary(id => id, _ => new List<Guid>());
        var names = new Dictionary<Guid, string>();
        foreach (var segment in active)
        {
            var verdicts = await resolver.EvaluateManyAsync(
                tenantId, segment, segment.SubjectType, ids, effectiveAt, cancellationToken);
            foreach (var (contactId, verdict) in verdicts)
            {
                if (string.Equals(verdict.Verdict, SegmentMembershipVerdicts.Member, StringComparison.Ordinal)
                    && byContact.TryGetValue(contactId, out var list))
                {
                    list.Add(segment.Id);
                    names[segment.Id] = string.IsNullOrWhiteSpace(segment.SegmentName) ? segment.SegmentCode : segment.SegmentName;
                }
            }
        }

        return new ContactSegmentSet(
            byContact.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<Guid>)kv.Value),
            names);
    }
}
