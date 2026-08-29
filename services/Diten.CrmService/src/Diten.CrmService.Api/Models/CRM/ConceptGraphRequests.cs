namespace Diten.CrmService.Api.Models.CRM;

// MOD-0162 FU03 request models. TenantId is NEVER part of any request body — it is server-resolved from the JWT claim.
// Route ids (conceptTypeId / conceptNodeId / relationshipId / templateId / linkId) come from the path, never the body.

public sealed record CreateConceptTypeRequest(
    Guid SubjectId,
    string ConceptTypeCode,
    string ConceptTypeName,
    string? Description = null,
    int SortOrder = 0,
    string? Status = null);

public sealed record UpdateConceptTypeRequest(
    string ConceptTypeName,
    string? Description = null,
    int SortOrder = 0,
    string? Status = null);

public sealed record CreateConceptNodeRequest(
    Guid SubjectId,
    Guid ConceptTypeId,
    string ConceptNodeCode,
    string ConceptNodeName,
    DateTimeOffset EffectiveFrom,
    string? Description = null,
    string? Status = null,
    DateTimeOffset? EffectiveTo = null,
    string? ExternalRefType = null,
    string? ExternalRefId = null,
    string? MetadataJson = null);

public sealed record UpdateConceptNodeRequest(
    string ConceptNodeName,
    DateTimeOffset EffectiveFrom,
    string? Description = null,
    string? Status = null,
    DateTimeOffset? EffectiveTo = null,
    string? ExternalRefType = null,
    string? ExternalRefId = null,
    string? MetadataJson = null);

public sealed record CreateConceptRelationshipRequest(
    Guid SubjectId,
    Guid FromConceptNodeId,
    Guid ToConceptNodeId,
    string RelationshipType,
    string RelationshipCode,
    string RelationshipName,
    DateTimeOffset EffectiveFrom,
    string? Direction = null,
    int Priority = 0,
    string? Status = null,
    DateTimeOffset? EffectiveTo = null);

public sealed record UpdateConceptRelationshipRequest(
    string RelationshipName,
    DateTimeOffset EffectiveFrom,
    string? Direction = null,
    int Priority = 0,
    string? Status = null,
    DateTimeOffset? EffectiveTo = null);

public sealed record CreateConceptChainTemplateRequest(
    Guid SubjectId,
    string ChainCode,
    string ChainName,
    IReadOnlyList<Guid> OrderedConceptTypes,
    DateTimeOffset EffectiveFrom,
    string? Description = null,
    string? Status = null,
    string? ChainVersion = null,
    DateTimeOffset? EffectiveTo = null);

public sealed record UpdateConceptChainTemplateRequest(
    string ChainName,
    IReadOnlyList<Guid> OrderedConceptTypes,
    DateTimeOffset EffectiveFrom,
    string? Description = null,
    string? Status = null,
    string? ChainVersion = null,
    DateTimeOffset? EffectiveTo = null);

public sealed record CreateContentConceptLinkRequest(
    Guid KnowledgeContentId,
    Guid ConceptNodeId,
    Guid? ConceptRelationshipId = null,
    string? LinkRole = null,
    int SortOrder = 0);
