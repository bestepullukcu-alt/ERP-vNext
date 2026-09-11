using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Knowledge.Concept;

/// <summary>Aggregate ↔ DTO projection for MOD-0162 FU03. Reads never echo TenantId (server-resolved). Reference ids are
/// projected as-is; no master field is resolved or copied.</summary>
public static class ConceptGraphMapper
{
    public static ConceptTypeDto ToDto(ConceptType t) => new(
        t.Id, t.SubjectId, t.ConceptTypeCode, t.ConceptTypeName, t.Description, t.SortOrder,
        t.Color, t.IsGroup, t.IsList, t.ParentConceptTypeId, t.Status,
        t.CreatedAt, t.CreatedBy, t.UpdatedAt, t.UpdatedBy, t.ArchivedAt, t.ArchivedBy, t.IsArchived());

    public static ConceptNodeDto ToDto(ConceptNode n) => new(
        n.Id, n.SubjectId, n.ConceptTypeId, n.ConceptNodeCode, n.ConceptNodeName, n.Description, n.Status,
        n.EffectiveFrom, n.EffectiveTo, n.ExternalRefType, n.ExternalRefId, n.MetadataJson,
        n.CreatedAt, n.CreatedBy, n.UpdatedAt, n.UpdatedBy, n.ArchivedAt, n.ArchivedBy, n.IsArchived());

    public static ConceptRelationshipDto ToDto(ConceptRelationship r) => new(
        r.Id, r.SubjectId, r.FromConceptNodeId, r.ToConceptNodeId, r.RelationshipType, r.RelationshipCode,
        r.RelationshipName, r.Direction, r.Priority, r.IsTemplateConforming, r.Status, r.EffectiveFrom, r.EffectiveTo,
        r.CreatedAt, r.CreatedBy, r.UpdatedAt, r.UpdatedBy, r.ArchivedAt, r.ArchivedBy, r.IsArchived());

    public static ConceptChainTemplateDto ToDto(ConceptChainTemplate c) => new(
        c.Id, c.SubjectId, c.ChainCode, c.ChainName, c.Description, c.OrderedConceptTypes.ToList(),
        ToBranchDtos(c), c.Status,
        c.ChainVersion, c.EffectiveFrom, c.EffectiveTo,
        c.CreatedAt, c.CreatedBy, c.UpdatedAt, c.UpdatedBy, c.ArchivedAt, c.ArchivedBy, c.IsArchived());

    /// <summary>SCMM-10 (③) read-time migration: an explicit branch structure is projected as-is; a legacy flat template
    /// (no branches) is projected as a SINGLE branch derived from <c>OrderedConceptTypes</c> (each type → one
    /// single-select step), so every reader — new or old — sees a branched shape without any stored data changing.</summary>
    private static IReadOnlyList<ConceptChainBranchDto> ToBranchDtos(ConceptChainTemplate c)
    {
        if (c.Branches is { Count: > 0 })
        {
            return c.Branches.Select(b => new ConceptChainBranchDto(
                b.BranchCode, b.BranchName, b.SortOrder,
                b.Steps.Select(s => new ConceptChainStepDto(
                    s.ConceptTypeId, s.MinSelection, s.MaxSelection,
                    s.AllowedRoleRefs.ToList(), s.AudienceDimensionRefs.ToList())).ToList())).ToList();
        }

        if (c.OrderedConceptTypes.Count == 0)
        {
            return Array.Empty<ConceptChainBranchDto>();
        }

        var steps = c.OrderedConceptTypes
            .Select(id => new ConceptChainStepDto(id, 1, 1, Array.Empty<string>(), Array.Empty<string>()))
            .ToList();
        return new[] { new ConceptChainBranchDto("B1", null, 0, steps) };
    }

    public static KnowledgeContentConceptLinkDto ToDto(KnowledgeContentConceptLink l) => new(
        l.Id, l.KnowledgeContentId, l.ConceptNodeId, l.ConceptRelationshipId, l.LinkRole, l.SortOrder, l.Status,
        l.CreatedAt, l.CreatedBy, l.UpdatedAt, l.UpdatedBy, l.ArchivedAt, l.ArchivedBy, l.IsArchived());
}
