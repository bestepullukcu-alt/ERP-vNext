namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>
/// MOD-0167 FU02 Phase-2 ConceptGraph seam (D-PRODUCT). READ-ONLY consumption of MOD-0162 FU03: it uses the EXISTING
/// repository surface (list + in-memory filter, exactly the way the FU03 graph handlers already work), so
/// <c>IConceptGraphRepositories</c> is not widened, no graph aggregate / endpoint / repository method is added, and
/// nothing in the graph is ever written.
/// <para>The traversal is bounded — default depth 1, ceiling 2, no transitive closure — and follows only OUTBOUND
/// <c>addresses</c> / <c>belongs-to</c> edges. It is deliberately LIVE: activating a segment freezes the criteria tree
/// (which question is asked), never the answer of a derivation.</para>
/// </summary>
public interface ISegmentConceptAffinityReader
{
    /// <summary>Derives the reachable specialty set for one global-product id. Called ONCE per distinct
    /// (product, depth, concept-subject) triple in a resolution, never once per candidate.</summary>
    Task<SegmentConceptAffinityResult> ResolveSpecialtiesAsync(
        Guid tenantId,
        string globalProductId,
        int maxDepth,
        Guid? conceptSubjectId,
        DateTimeOffset effectiveAt,
        CancellationToken cancellationToken);
}
