using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Knowledge.Concept.Relationship;

/// <summary>WP-CT-BE-A — conformance result vocabulary (derived, never enforced — D8).</summary>
public static class ConceptChainConformanceResults
{
    /// <summary>Both types are on the spine and, read in chain direction, the first comes before the second (skipping
    /// ahead included — WP-CT-BE-B).</summary>
    public const string Conforming = "conforming";

    /// <summary>Both types are on the spine but, read in chain direction, the pair runs backward.</summary>
    public const string Order = "order";

    /// <summary>At least one of the two types is not on the spine (see <see cref="ConceptChainConformanceClassification.MissingTypeIds"/>).</summary>
    public const string Out = "out";

    public static readonly IReadOnlyList<string> All = new[] { Conforming, Order, Out };
}

/// <summary>The classification of one relationship against one spine. <see cref="MissingTypeIds"/> is non-empty only
/// for <see cref="ConceptChainConformanceResults.Out"/>.</summary>
public sealed record ConceptChainConformanceClassification(
    string Result,
    IReadOnlyList<Guid> MissingTypeIds,
    bool IsReversed);

/// <summary>
/// WP-CT-BE-A — the SINGLE conformance classifier. Both the stored <c>IsTemplateConforming</c> flag (via
/// <see cref="ConceptRelationshipGraph.IsConforming"/>) and the template conformance-diagnostics read derive from this
/// pure function, so the editor's "non-conforming" tab and the persisted flag can never diverge.
/// <para>D2=(i): <c>addresses</c> / <c>evidences</c> are narrated against the chain direction ("component addresses the
/// need" while the chain reads need → component), so the (from, to) pair is swapped BEFORE the order check. Every
/// other type (<c>leads-to</c>, <c>requires</c>, <c>belongs-to</c>, <c>custom</c>) is read literally.</para>
/// <para>Diagnostics only — no traversal, no resolution, no enforcement (D8).</para>
/// </summary>
public static class ConceptChainConformance
{
    /// <summary>Relationship types whose narrative direction is the reverse of the chain direction.</summary>
    public static readonly IReadOnlyList<string> ReversedRelationshipTypes = new[]
    {
        ConceptRelationshipTypes.Addresses,
        ConceptRelationshipTypes.Evidences
    };

    public static bool IsReversed(string? relationshipType)
        => ReversedRelationshipTypes.Contains(ConceptRelationshipTypes.Normalize(relationshipType));

    /// <summary>Classifies the (fromType → toType) edge of the given relationship type against an ordered spine.</summary>
    public static ConceptChainConformanceClassification Classify(
        IReadOnlyList<Guid> orderedTypeIds, Guid fromTypeId, Guid toTypeId, string? relationshipType)
    {
        var reversed = IsReversed(relationshipType);
        var (first, second) = reversed ? (toTypeId, fromTypeId) : (fromTypeId, toTypeId);

        var missing = new[] { fromTypeId, toTypeId }
            .Distinct()
            .Where(id => !orderedTypeIds.Contains(id))
            .ToList();
        if (missing.Count > 0)
        {
            return new ConceptChainConformanceClassification(ConceptChainConformanceResults.Out, missing, reversed);
        }

        // WP-CT-BE-B (D2 relaxed) — forward order, not adjacency: a pair that skips ahead (A → C on A, B, C) conforms
        // because a step may be optional (MinSelection 0). Only a backward pair is an order problem. A type never
        // repeats on a spine, so first == second cannot be forward.
        var result = IndexOf(orderedTypeIds, first) < IndexOf(orderedTypeIds, second)
            ? ConceptChainConformanceResults.Conforming
            : ConceptChainConformanceResults.Order;
        return new ConceptChainConformanceClassification(result, Array.Empty<Guid>(), reversed);
    }

    private static int IndexOf(IReadOnlyList<Guid> list, Guid value)
    {
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] == value)
            {
                return i;
            }
        }

        return -1;
    }
}
