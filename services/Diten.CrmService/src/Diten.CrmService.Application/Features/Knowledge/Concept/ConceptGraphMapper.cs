using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Knowledge.Concept;

/// <summary>Aggregate ↔ DTO projection for MOD-0162 FU03. Reads never echo TenantId (server-resolved). Reference ids are
/// projected as-is; no master field is resolved or copied.</summary>
public static class ConceptGraphMapper
{
    public static ConceptTypeDto ToDto(ConceptType t) => new(
        t.Id, t.SubjectId, t.ConceptTypeCode, t.ConceptTypeName, t.Description, t.SortOrder, t.Status,
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
        c.Id, c.SubjectId, c.ChainCode, c.ChainName, c.Description, c.OrderedConceptTypes.ToList(), c.Status,
        c.ChainVersion, c.EffectiveFrom, c.EffectiveTo,
        c.CreatedAt, c.CreatedBy, c.UpdatedAt, c.UpdatedBy, c.ArchivedAt, c.ArchivedBy, c.IsArchived());

    public static KnowledgeContentConceptLinkDto ToDto(KnowledgeContentConceptLink l) => new(
        l.Id, l.KnowledgeContentId, l.ConceptNodeId, l.ConceptRelationshipId, l.LinkRole, l.SortOrder, l.Status,
        l.CreatedAt, l.CreatedBy, l.UpdatedAt, l.UpdatedBy, l.ArchivedAt, l.ArchivedBy, l.IsArchived());
}
