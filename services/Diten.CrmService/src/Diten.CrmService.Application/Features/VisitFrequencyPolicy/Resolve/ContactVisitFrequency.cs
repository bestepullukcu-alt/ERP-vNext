using Diten.CrmService.Application.Features.Segmentation.Resolution;
using Diten.CrmService.Application.Features.VisitFrequencyPolicy.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.VisitFrequencyPolicy.Resolve;

/// <summary>
/// WP-VP-3D — a DOCTOR's visit frequency for many doctors at once, with the SAME rule as the single-doctor path the
/// planning engine uses today (<see cref="Diten.CrmService.Application.Features.VisitPlanning.FrequencyExtendPlanner"/> →
/// <see cref="IVisitFrequencyPolicyResolver"/> with <c>TargetType = contact</c>, the doctor's id, the instant, and no
/// segment / campaign / territory context): the candidate policies are the doctor's own plus its ACTIVE segments'
/// (WP-FREQ-DET-P), and <see cref="VisitFrequencyResolveEngine"/> decides. Nothing is re-implemented — only the reads are
/// batched: ONE policy read for the whole set (the engine keeps only each doctor's own target pairs, so a shared
/// candidate list is exact), and the segment memberships come in already derived (<see cref="ContactSegmentSet"/>).
/// <para>This is the shared function WP-VP-3A should bind the engine to (one rule for preview, period plan and the
/// status reads). Reads only.</para>
/// </summary>
public static class ContactVisitFrequency
{
    public static async Task<IReadOnlyDictionary<Guid, VisitFrequencyResolveResult>> ResolveManyAsync(
        IVisitFrequencyPolicyRepository policies,
        Guid tenantId,
        IReadOnlyCollection<Guid> contactIds,
        ContactSegmentSet segments,
        DateTimeOffset effectiveAt,
        CancellationToken cancellationToken)
    {
        var ids = contactIds.Where(id => id != Guid.Empty).Distinct().ToList();
        var result = new Dictionary<Guid, VisitFrequencyResolveResult>();
        if (ids.Count == 0)
        {
            return result;
        }

        var targetIds = ids.Concat(ids.SelectMany(segments.For)).Distinct().ToList();
        var candidates = await policies.ListActiveByTargetsAsync(tenantId, targetIds, cancellationToken);

        foreach (var contactId in ids)
        {
            result[contactId] = Resolve(contactId, segments.For(contactId), candidates, effectiveAt);
        }

        return result;
    }

    /// <summary>One doctor's verdict from an already-read candidate list — the request shape of the single path.</summary>
    public static VisitFrequencyResolveResult Resolve(
        Guid contactId,
        IReadOnlyCollection<Guid> activeSegmentIds,
        IReadOnlyCollection<Domain.Entities.VisitFrequencyPolicy> candidates,
        DateTimeOffset effectiveAt)
        => VisitFrequencyResolveEngine.Resolve(
            new ResolveVisitFrequencyPolicyQuery(
                TargetType: FrequencyTargetType.Contact,
                TargetId: contactId,
                EffectiveAt: effectiveAt,
                IncludeDiagnostics: false),
            candidates,
            effectiveAt,
            activeSegmentIds.ToHashSet());
}
