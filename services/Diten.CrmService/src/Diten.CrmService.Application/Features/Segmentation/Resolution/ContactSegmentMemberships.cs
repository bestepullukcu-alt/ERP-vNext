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
}
