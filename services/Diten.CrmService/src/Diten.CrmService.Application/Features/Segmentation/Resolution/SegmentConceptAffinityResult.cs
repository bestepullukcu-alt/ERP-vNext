namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>
/// The outcome of ONE bounded <c>concept.affinity</c> derivation: the specialty codes reachable from a global-product
/// node. <see cref="ProductNodeFound"/> separates "the graph knows nothing about this product" from "the graph knows
/// the product but no specialty is reachable", so the two get DIFFERENT reason codes — and neither is ever a 503.
/// <para>The set is derived ONCE per resolution and applied to every candidate: its cost depends on the size of the
/// concept graph, never on the number of candidates.</para>
/// </summary>
public sealed record SegmentConceptAffinityResult(
    bool ProductNodeFound,
    IReadOnlyCollection<string> SpecialtyCodes)
{
    public static readonly SegmentConceptAffinityResult NoProductNode = new(false, Array.Empty<string>());
}
