using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Domain.Repositories;

/// <summary>MOD-0162 FU03 concept-type master. Tenant scoped, soft-delete aware. No delete method: closing a type is the
/// soft archive lifecycle. Code is unique within (tenant, subject) among non-archived rows.</summary>
public interface IConceptTypeRepository
{
    Task<ConceptType?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConceptType>> ListAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConceptType>> ListBySubjectAsync(Guid tenantId, Guid subjectId, CancellationToken cancellationToken);
    Task<ConceptType?> GetActiveByCodeAsync(Guid tenantId, Guid subjectId, string conceptTypeCode, CancellationToken cancellationToken);
    Task InsertAsync(ConceptType entity, CancellationToken cancellationToken);
    Task UpdateAsync(ConceptType entity, CancellationToken cancellationToken);
}

/// <summary>MOD-0162 FU03 concept-node master. Code is unique within (subject, type) among non-archived rows.</summary>
public interface IConceptNodeRepository
{
    Task<ConceptNode?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConceptNode>> ListAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConceptNode>> ListBySubjectAsync(Guid tenantId, Guid subjectId, CancellationToken cancellationToken);
    Task<ConceptNode?> GetActiveByCodeAsync(Guid tenantId, Guid subjectId, Guid conceptTypeId, string conceptNodeCode, CancellationToken cancellationToken);
    Task InsertAsync(ConceptNode entity, CancellationToken cancellationToken);
    Task UpdateAsync(ConceptNode entity, CancellationToken cancellationToken);
}

/// <summary>MOD-0162 FU03 concept-relationship (directed edge). Listed per subject for cycle detection, conformance and
/// the read-only graph view.</summary>
public interface IConceptRelationshipRepository
{
    Task<ConceptRelationship?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConceptRelationship>> ListAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConceptRelationship>> ListBySubjectAsync(Guid tenantId, Guid subjectId, CancellationToken cancellationToken);
    Task InsertAsync(ConceptRelationship entity, CancellationToken cancellationToken);
    Task UpdateAsync(ConceptRelationship entity, CancellationToken cancellationToken);
}

/// <summary>MOD-0162 FU03 concept-chain template (versioned). Listed per subject and per chain code (the published
/// overlap guard and template-conformance computation both need same-code / same-subject rows).</summary>
public interface IConceptChainTemplateRepository
{
    Task<ConceptChainTemplate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConceptChainTemplate>> ListAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConceptChainTemplate>> ListBySubjectAsync(Guid tenantId, Guid subjectId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ConceptChainTemplate>> ListByCodeAsync(Guid tenantId, Guid subjectId, string chainCode, CancellationToken cancellationToken);
    Task InsertAsync(ConceptChainTemplate entity, CancellationToken cancellationToken);
    Task UpdateAsync(ConceptChainTemplate entity, CancellationToken cancellationToken);
}

/// <summary>MOD-0162 FU03 content ↔ concept link (many-to-many). Listed by content and by node for the graph views and
/// the duplicate-link guard.</summary>
public interface IKnowledgeContentConceptLinkRepository
{
    Task<KnowledgeContentConceptLink?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<KnowledgeContentConceptLink>> ListAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyList<KnowledgeContentConceptLink>> ListByContentAsync(Guid tenantId, Guid knowledgeContentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<KnowledgeContentConceptLink>> ListByNodeAsync(Guid tenantId, Guid conceptNodeId, CancellationToken cancellationToken);
    Task InsertAsync(KnowledgeContentConceptLink entity, CancellationToken cancellationToken);
    Task UpdateAsync(KnowledgeContentConceptLink entity, CancellationToken cancellationToken);
}
