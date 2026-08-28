namespace Diten.CrmService.Application.Features.Knowledge.Concept;

/// <summary>MOD-0162 FU03 read model for a concept type. TenantId is never echoed (server-resolved).</summary>
public sealed record ConceptTypeDto(
    Guid ConceptTypeId,
    Guid SubjectId,
    string ConceptTypeCode,
    string ConceptTypeName,
    string? Description,
    int SortOrder,
    string Status,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    bool IsArchived);

public sealed record ConceptTypeListDto(IReadOnlyList<ConceptTypeDto> Items, int Total);

/// <summary>MOD-0162 FU03 read model for a concept node. ExternalRef is provenance only — no master field is resolved.</summary>
public sealed record ConceptNodeDto(
    Guid ConceptNodeId,
    Guid SubjectId,
    Guid ConceptTypeId,
    string ConceptNodeCode,
    string ConceptNodeName,
    string? Description,
    string Status,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    string? ExternalRefType,
    string? ExternalRefId,
    string? MetadataJson,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    bool IsArchived);

public sealed record ConceptNodeListDto(IReadOnlyList<ConceptNodeDto> Items, int Total);

/// <summary>MOD-0162 FU03 read model for a directed relationship. <c>IsTemplateConforming</c> is derived and visible —
/// a non-conforming edge is never silently dropped.</summary>
public sealed record ConceptRelationshipDto(
    Guid ConceptRelationshipId,
    Guid SubjectId,
    Guid FromConceptNodeId,
    Guid ToConceptNodeId,
    string RelationshipType,
    string RelationshipCode,
    string RelationshipName,
    string Direction,
    int Priority,
    bool IsTemplateConforming,
    string Status,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    bool IsArchived);

public sealed record ConceptRelationshipListDto(IReadOnlyList<ConceptRelationshipDto> Items, int Total);

/// <summary>MOD-0162 FU03 read model for a chain template. <c>OrderedConceptTypes</c> is the frozen (once published)
/// sequence of ConceptType ids. <c>ChainVersion</c> is the business version (not the concurrency token).</summary>
public sealed record ConceptChainTemplateDto(
    Guid ConceptChainTemplateId,
    Guid SubjectId,
    string ChainCode,
    string ChainName,
    string? Description,
    IReadOnlyList<Guid> OrderedConceptTypes,
    string Status,
    string ChainVersion,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    bool IsArchived);

public sealed record ConceptChainTemplateListDto(IReadOnlyList<ConceptChainTemplateDto> Items, int Total);

/// <summary>MOD-0162 FU03 read model for a content ↔ concept link.</summary>
public sealed record KnowledgeContentConceptLinkDto(
    Guid LinkId,
    Guid KnowledgeContentId,
    Guid ConceptNodeId,
    Guid? ConceptRelationshipId,
    string LinkRole,
    int SortOrder,
    string Status,
    DateTimeOffset CreatedAt,
    string? CreatedBy,
    DateTimeOffset? UpdatedAt,
    string? UpdatedBy,
    DateTimeOffset? ArchivedAt,
    string? ArchivedBy,
    bool IsArchived);

public sealed record KnowledgeContentConceptLinkListDto(IReadOnlyList<KnowledgeContentConceptLinkDto> Items, int Total);

/// <summary>MOD-0162 FU03 read-only graph view — node list + edge list + template list for a subject (or a subset for
/// the by-node / by-content projections). This is an ADJACENCY read, NOT an engine: no multi-hop traversal, no
/// best-path, no scoring, no recommendation. Empty when there is no data — no default is invented.</summary>
public sealed record ConceptGraphDto(
    Guid SubjectId,
    IReadOnlyList<ConceptNodeDto> Nodes,
    IReadOnlyList<ConceptRelationshipDto> Edges,
    IReadOnlyList<ConceptChainTemplateDto> Templates);
