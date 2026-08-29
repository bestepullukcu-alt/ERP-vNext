using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>
/// MOD-0167 FU02 Phase-1 / Phase-1.5 read seam. Owned by this FU: it reads the Account / Contact / AccountContactLink /
/// AccountAttributeValue collections as a PROJECTION and therefore changes no MOD-0149 / MOD-0150 file, repository
/// signature or aggregate.
/// <para><b>Phase 1 is ONE Mongo query.</b> The implementation translates the criteria tree into a native filter that
/// is a deliberate OVER-APPROXIMATION: a non-native (J/D) node contributes "true", so the query can only ever return a
/// SUPERSET of the real answer and the in-memory evaluator then decides exactly. That is what makes the pushdown fast
/// and still correct.</para>
/// <para>Every method here is bulk by construction. A per-candidate read is the N+1 the scale contract forbids, and a
/// call-counter test pins that down.</para>
/// </summary>
public interface ISegmentCandidateSource
{
    /// <summary>Phase 1: single pushdown. Reads at most <paramref name="cap"/> + 1 rows so the ceiling breach is
    /// detected by the SAME query (no second count round-trip) and reported as 422.</summary>
    Task<SegmentCandidateLoad> LoadCandidatesAsync(
        Guid tenantId,
        string subjectType,
        IReadOnlyList<SegmentCriteriaNode> criteria,
        string matchMode,
        int cap,
        CancellationToken cancellationToken);

    /// <summary>Loads specific subjects: the single-subject is-member path, and the hybrid manual-include rows the
    /// pushdown did not return. ONE query.</summary>
    Task<IReadOnlyList<SegmentSubjectSnapshot>> LoadSubjectsByIdsAsync(
        Guid tenantId,
        string subjectType,
        IReadOnlyCollection<Guid> subjectIds,
        CancellationToken cancellationToken);

    /// <summary>Phase 1.5: active links for the whole candidate set, plus the linked account type. Bulk.</summary>
    Task<IReadOnlyList<SegmentLinkProjection>> LoadLinksAsync(
        Guid tenantId,
        string subjectType,
        IReadOnlyCollection<Guid> subjectIds,
        CancellationToken cancellationToken);

    /// <summary>Attribute values for the whole candidate set. Bulk.</summary>
    Task<IReadOnlyList<SegmentAccountAttributeProjection>> LoadAccountAttributesAsync(
        Guid tenantId,
        IReadOnlyCollection<Guid> accountIds,
        CancellationToken cancellationToken);
}
