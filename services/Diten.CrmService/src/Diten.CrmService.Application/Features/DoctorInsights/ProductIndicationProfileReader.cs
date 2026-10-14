using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.DoctorInsights;

/// <summary>One indication (× optional patient profile) row the report asks patients for.</summary>
public sealed record IndicationProfileRowDto(Guid IndicationNodeId, string IndicationName, Guid? ProfileNodeId, string? ProfileName);

/// <summary>The rows of a product; <see cref="Reason"/> says why the list is empty (<c>no_concept_graph</c> | <c>no_indications</c>).</summary>
public sealed record ProductIndicationProfilesDto(Guid ProductId, IReadOnlyList<IndicationProfileRowDto> Items, string? Reason);

/// <summary>
/// WP-VW-W4 (W4-BE-c, §2.2, K-W6) — a product's indication × patient profile rows, read from the Concept Graph
/// (KnowledgeConcepts): the product node (<c>ExternalRefType = global-product</c>, <c>ExternalRefId</c> = the Global
/// Product id) –<c>addresses</c>→ an indication node (type <see cref="IDoctorInsightsSettings.IndicationConceptTypeCode"/>)
/// –<c>leads-to</c>→ a profile node (type <see cref="IDoctorInsightsSettings.ProfileConceptTypeCode"/>).
/// <list type="bullet">
/// <item>only ACTIVE, not archived, effective nodes and relationships; a <c>bidirectional</c> relationship counts both ways;</item>
/// <item>an indication without a profile is one row with no profile;</item>
/// <item>no type mapping or no product node ⇒ empty + <c>no_concept_graph</c> (the report then asks only the total);
/// a product node without indications ⇒ empty + <c>no_indications</c>.</item>
/// </list>
/// Tenant scoped through the repositories; reads only.
/// </summary>
public sealed class ProductIndicationProfileReader
{
    private readonly IConceptTypeRepository _types;
    private readonly IConceptNodeRepository _nodes;
    private readonly IConceptRelationshipRepository _relationships;
    private readonly IDoctorInsightsSettings _settings;
    private readonly TimeProvider _clock;

    public ProductIndicationProfileReader(IConceptTypeRepository types, IConceptNodeRepository nodes,
        IConceptRelationshipRepository relationships, IDoctorInsightsSettings? settings = null, TimeProvider? clock = null)
    {
        _types = types;
        _nodes = nodes;
        _relationships = relationships;
        _settings = settings ?? new DefaultDoctorInsightsSettings();
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<ProductIndicationProfilesDto> ReadAsync(Guid tenantId, Guid productId, CancellationToken ct)
    {
        var types = (await _types.ListAsync(tenantId, ct)).Where(t => t.TenantId == tenantId && Live(t.Status, t.IsArchived())).ToList();
        var indicationTypes = TypeIds(types, _settings.IndicationConceptTypeCode);
        var profileTypes = TypeIds(types, _settings.ProfileConceptTypeCode);
        if (indicationTypes.Count == 0) return Empty(productId, DoctorInsightsErrors.NoConceptGraph);

        var now = _clock.GetUtcNow();
        var nodes = (await _nodes.ListAsync(tenantId, ct))
            .Where(n => n.TenantId == tenantId && Live(n.Status, n.IsArchived()) && n.IsEffectiveAt(now))
            .ToDictionary(n => n.Id);
        var products = nodes.Values
            .Where(n => string.Equals(n.ExternalRefType, ConceptExternalRefTypes.GlobalProduct, StringComparison.OrdinalIgnoreCase)
                        && Guid.TryParse(n.ExternalRefId, out var g) && g == productId)
            .Select(n => n.Id)
            .ToHashSet();
        if (products.Count == 0) return Empty(productId, DoctorInsightsErrors.NoConceptGraph);

        var edges = (await _relationships.ListAsync(tenantId, ct))
            .Where(r => r.TenantId == tenantId && r.IsActive()
                        && r.EffectiveFrom <= now && (r.EffectiveTo is null || now <= r.EffectiveTo))
            .ToList();

        var indications = Targets(edges, ConceptRelationshipTypes.Addresses, products)
            .Where(e => nodes.TryGetValue(e.Node, out var n) && indicationTypes.Contains(n.ConceptTypeId))
            .GroupBy(e => e.Node).Select(g => g.OrderBy(e => e.Priority).First())
            .OrderBy(e => e.Priority).ThenBy(e => nodes[e.Node].ConceptNodeName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        if (indications.Count == 0) return Empty(productId, DoctorInsightsErrors.NoIndications);

        var rows = new List<IndicationProfileRowDto>();
        foreach (var indication in indications)
        {
            var node = nodes[indication.Node];
            var profiles = Targets(edges, ConceptRelationshipTypes.LeadsTo, new HashSet<Guid> { indication.Node })
                .Where(e => nodes.TryGetValue(e.Node, out var p) && profileTypes.Contains(p.ConceptTypeId))
                .GroupBy(e => e.Node).Select(g => g.OrderBy(e => e.Priority).First())
                .OrderBy(e => e.Priority).ThenBy(e => nodes[e.Node].ConceptNodeName, StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            if (profiles.Count == 0)
            {
                rows.Add(new IndicationProfileRowDto(node.Id, node.ConceptNodeName, null, null));
                continue;
            }

            rows.AddRange(profiles.Select(p => new IndicationProfileRowDto(node.Id, node.ConceptNodeName, p.Node, nodes[p.Node].ConceptNodeName)));
        }

        return new ProductIndicationProfilesDto(productId, rows, null);
    }

    private static ProductIndicationProfilesDto Empty(Guid productId, string reason) => new(productId, Array.Empty<IndicationProfileRowDto>(), reason);

    private static bool Live(string? status, bool archived)
        => !archived && string.Equals(status, ConceptStatuses.Active, StringComparison.OrdinalIgnoreCase);

    private static HashSet<Guid> TypeIds(IEnumerable<ConceptType> types, string code)
        => types.Where(t => string.Equals(t.ConceptTypeCode, code, StringComparison.OrdinalIgnoreCase)).Select(t => t.Id).ToHashSet();

    /// <summary>The nodes reached from <paramref name="from"/> over <paramref name="type"/> (both ways when bidirectional).</summary>
    private static IEnumerable<(Guid Node, int Priority)> Targets(IEnumerable<ConceptRelationship> edges, string type, IReadOnlySet<Guid> from)
    {
        foreach (var e in edges.Where(e => string.Equals(e.RelationshipType, type, StringComparison.OrdinalIgnoreCase)))
        {
            if (from.Contains(e.FromConceptNodeId)) yield return (e.ToConceptNodeId, e.Priority);
            else if (string.Equals(e.Direction, ConceptDirections.Bidirectional, StringComparison.OrdinalIgnoreCase) && from.Contains(e.ToConceptNodeId))
                yield return (e.FromConceptNodeId, e.Priority);
        }
    }
}
