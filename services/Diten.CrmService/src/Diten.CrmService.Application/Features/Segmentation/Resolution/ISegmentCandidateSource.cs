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

    /// <summary>WP-E2E-FIX-3 (E1-B2) — Phase 1 with the pre-filtered leaves (<see cref="SegmentCandidatePrefilter"/>):
    /// NodeId → the subject ids that can satisfy that leaf, pushed as <c>_id IN</c>. Ignoring them (the default) is still
    /// correct — the answer is merely a wider superset — so a source that cannot push them stays valid.</summary>
    Task<SegmentCandidateLoad> LoadCandidatesAsync(
        Guid tenantId,
        string subjectType,
        IReadOnlyList<SegmentCriteriaNode> criteria,
        string matchMode,
        int cap,
        IReadOnlyDictionary<Guid, IReadOnlyCollection<Guid>> prefiltered,
        CancellationToken cancellationToken)
        => LoadCandidatesAsync(tenantId, subjectType, criteria, matchMode, cap, cancellationToken);

    /// <summary>WP-E2E-FIX-3 — the contacts with a (not deleted) link to one of <paramref name="accountIds"/>; null when
    /// more than <paramref name="max"/> (or when the source cannot answer — then nothing is narrowed).</summary>
    Task<IReadOnlyCollection<Guid>?> ListContactIdsLinkedToAccountsAsync(
        Guid tenantId, IReadOnlyCollection<Guid> accountIds, int max, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyCollection<Guid>?>(null);

    /// <summary>WP-E2E-FIX-3 — the contacts with a (not deleted) link satisfying a <c>contact.account-role /
    /// is-primary / account-type</c> leaf (a superset: link status is not filtered); null when more than
    /// <paramref name="max"/> or when the source cannot answer.</summary>
    Task<IReadOnlyCollection<Guid>?> ListContactIdsByLinkAsync(
        Guid tenantId, SegmentCriteriaNode leaf, int max, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyCollection<Guid>?>(null);

    /// <summary>WP-E2E-FIX-3 — the store's own count of a FULLY NATIVE rule (<see cref="SegmentPushdownRules.IsFullyNative"/>):
    /// there the native filter is the rule itself, so the reach preview can answer past the candidate ceiling. Null when
    /// the rule is not fully native or the source cannot count.</summary>
    Task<long?> CountFullyNativeAsync(
        Guid tenantId,
        string subjectType,
        IReadOnlyList<SegmentCriteriaNode> criteria,
        string matchMode,
        CancellationToken cancellationToken)
        => Task.FromResult<long?>(null);
}
