using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>
/// MOD-0167 FU02 — <c>concept.affinity</c> derivation (D-PRODUCT). Answers "is this doctor a doctor who cares about
/// product P?" without any product field ever being written on a person: the interest edge already exists in the
/// MOD-0162 FU03 concept graph, and this reader derives from it.
/// <code>
/// value: global-product P
///   1. concept nodes  -> ExternalRefType = global-product AND ExternalRefId = P   (active + effective)
///   2. outbound edges -> RelationshipType in { addresses, belongs-to }            (active + effective)
///                        bounded: default depth 1, ceiling 2, no transitive closure
///   3. reached nodes  -> ExternalRefType = reference-data-value
///   4. specialty set  -> their ExternalRefId values                               = S
///   5. match          -> candidate contact.specialty in S
/// </code>
/// <para><b>Read-only, structurally.</b> It consumes the EXISTING FU03 repository surface (list + in-memory filter, the
/// same way the FU03 graph handlers work): <c>IConceptGraphRepositories</c> is not widened, no graph aggregate,
/// endpoint or repository method is added, and there is no write path here at all. The list read is memoised for the
/// lifetime of the (request-scoped) instance, so a resolution performs ONE node read and ONE relationship read no
/// matter how many candidates or how many affinity predicates it evaluates — the N+1 ban is structural.</para>
/// <para><b>An empty graph is an empty ANSWER, never a 503.</b> The graph is in this service, so uncertainty here is
/// in-service: a missing product node eliminates every candidate with <c>concept_product_node_missing</c> and the
/// resolution completes with 200 and an empty member set. No candidate is ever admitted by default.</para>
/// <para><b>Deliberately live.</b> Activating a segment freezes the criteria TREE (which question is asked), not the
/// answer of a derivation. Add an edge to the graph and the same segment version answers differently — exactly as it
/// does when a contact specialty changes. The determinism contract is stated over UNCHANGED source data.</para>
/// </summary>
public sealed class ConceptAffinitySourceReader : ISegmentConceptAffinityReader
{
    private readonly IConceptNodeRepository _nodes;
    private readonly IConceptRelationshipRepository _relationships;

    private readonly Dictionary<string, IReadOnlyList<ConceptNode>> _nodeCache = new(StringComparer.Ordinal);
    private readonly Dictionary<string, IReadOnlyList<ConceptRelationship>> _relationshipCache = new(StringComparer.Ordinal);

    public ConceptAffinitySourceReader(IConceptNodeRepository nodes, IConceptRelationshipRepository relationships)
    {
        _nodes = nodes;
        _relationships = relationships;
    }

    public async Task<SegmentConceptAffinityResult> ResolveSpecialtiesAsync(
        Guid tenantId,
        string globalProductId,
        int maxDepth,
        Guid? conceptSubjectId,
        DateTimeOffset effectiveAt,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(globalProductId))
        {
            return SegmentConceptAffinityResult.NoProductNode;
        }

        var depth = Math.Clamp(maxDepth, 1, SegmentLimits.MaxConceptAffinityDepth);

        var allNodes = await LoadNodesAsync(tenantId, conceptSubjectId, cancellationToken);
        var liveNodes = allNodes.Where(n => IsLive(n, effectiveAt)).ToList();

        var productNodes = liveNodes
            .Where(n => string.Equals(
                            ConceptExternalRefTypes.Normalize(n.ExternalRefType),
                            ConceptExternalRefTypes.GlobalProduct, StringComparison.Ordinal)
                        && ExternalRefMatches(n.ExternalRefId, globalProductId))
            .Select(n => n.Id)
            .ToHashSet();

        if (productNodes.Count == 0)
        {
            // The graph knows nothing about this product. Empty set + its own reason code; never a dependency failure
            // and never "everybody matches".
            return SegmentConceptAffinityResult.NoProductNode;
        }

        var allEdges = await LoadRelationshipsAsync(tenantId, conceptSubjectId, cancellationToken);
        var followable = allEdges
            .Where(e => e.IsActive()
                        && IsEdgeEffective(e, effectiveAt)
                        && ConceptAffinityRelationshipTypes.IsFollowed(e.RelationshipType))
            .ToList();

        // Outbound only. A `bidirectional` edge is an explicit declaration and is followed exactly as authored; a
        // reverse edge is NEVER derived (the FU03 rule, kept verbatim).
        var outbound = followable
            .GroupBy(e => e.FromConceptNodeId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ToConceptNodeId).ToList());

        var byId = liveNodes.ToDictionary(n => n.Id);
        var reached = new HashSet<Guid>();
        var frontier = productNodes;

        for (var level = 0; level < depth && frontier.Count > 0; level++)
        {
            var next = new HashSet<Guid>();
            foreach (var from in frontier)
            {
                if (!outbound.TryGetValue(from, out var targets))
                {
                    continue;
                }

                foreach (var to in targets)
                {
                    // A target that is archived / inactive / outside the window is simply not part of the live graph.
                    if (!byId.ContainsKey(to) || productNodes.Contains(to) || !reached.Add(to))
                    {
                        continue;
                    }

                    next.Add(to);
                }
            }

            frontier = next;
        }

        var specialties = reached
            .Select(id => byId[id])
            .Where(n => string.Equals(
                ConceptExternalRefTypes.Normalize(n.ExternalRefType),
                ConceptExternalRefTypes.ReferenceDataValue, StringComparison.Ordinal))
            .Select(n => n.ExternalRefId!)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new SegmentConceptAffinityResult(true, specialties);
    }

    /// <summary>The FU03 edge entity exposes no effective-window helper; the window is applied here rather than by
    /// adding a member to a MOD-0162 aggregate this FU is only allowed to read.</summary>
    private static bool IsEdgeEffective(ConceptRelationship edge, DateTimeOffset at)
        => edge.EffectiveFrom <= at && (edge.EffectiveTo is null || at <= edge.EffectiveTo);

    private static bool IsLive(ConceptNode node, DateTimeOffset at)
        => !node.IsArchived()
           && string.Equals(ConceptStatuses.Normalize(node.Status), ConceptStatuses.Active, StringComparison.Ordinal)
           && node.IsEffectiveAt(at);

    /// <summary>Product/specialty ids are opaque strings on the graph; a Guid-shaped value is compared as a Guid so
    /// casing and brace formatting never cause a silent miss.</summary>
    private static bool ExternalRefMatches(string? externalRefId, string expected)
    {
        if (string.IsNullOrWhiteSpace(externalRefId))
        {
            return false;
        }

        if (Guid.TryParse(externalRefId, out var left) && Guid.TryParse(expected, out var right))
        {
            return left == right;
        }

        return string.Equals(externalRefId.Trim(), expected.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private async Task<IReadOnlyList<ConceptNode>> LoadNodesAsync(
        Guid tenantId, Guid? conceptSubjectId, CancellationToken cancellationToken)
    {
        var key = CacheKey(tenantId, conceptSubjectId);
        if (_nodeCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var loaded = conceptSubjectId is { } subjectId && subjectId != Guid.Empty
            ? await _nodes.ListBySubjectAsync(tenantId, subjectId, cancellationToken)
            : await _nodes.ListAsync(tenantId, cancellationToken);

        _nodeCache[key] = loaded;
        return loaded;
    }

    private async Task<IReadOnlyList<ConceptRelationship>> LoadRelationshipsAsync(
        Guid tenantId, Guid? conceptSubjectId, CancellationToken cancellationToken)
    {
        var key = CacheKey(tenantId, conceptSubjectId);
        if (_relationshipCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var loaded = conceptSubjectId is { } subjectId && subjectId != Guid.Empty
            ? await _relationships.ListBySubjectAsync(tenantId, subjectId, cancellationToken)
            : await _relationships.ListAsync(tenantId, cancellationToken);

        _relationshipCache[key] = loaded;
        return loaded;
    }

    private static string CacheKey(Guid tenantId, Guid? conceptSubjectId)
        => $"{tenantId:D}|{conceptSubjectId?.ToString("D") ?? "*"}";
}
