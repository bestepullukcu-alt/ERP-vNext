using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// MOD-0162 FU03 concept-graph persistence. Same rules as the FU02 knowledge repositories: tenant scoped, soft-delete
/// aware, no delete method (closing is the soft archive lifecycle). EffectiveFrom / EffectiveTo / ArchivedAt
/// (DateTimeOffset → BSON array) are never sorted server-side nor used as index keys; ordering happens in memory. The
/// duplicate-code guards exclude archived rows with an <c>ArchivedAt == null</c> equality filter (never <c>$ne</c>,
/// which crash-loops partial indexes). Every Guid FK takes the string-Guid class-map convention (see Persistence DI) so
/// filters never silently return nothing.
/// </summary>
public sealed class ConceptTypeRepository : IConceptTypeRepository
{
    public const string CollectionName = "concept_types";

    private readonly IMongoCollection<ConceptType> _collection;

    public ConceptTypeRepository(IMongoDatabase database)
        => _collection = database.GetCollection<ConceptType>(CollectionName);

    private static FilterDefinition<ConceptType> Tenant(Guid tenantId)
        => Builders<ConceptType>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<ConceptType?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId) & Builders<ConceptType>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ConceptType>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken);
        return rows.OrderBy(x => x.SortOrder).ThenByDescending(x => x.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<ConceptType>> ListBySubjectAsync(
        Guid tenantId, Guid subjectId, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId) & Builders<ConceptType>.Filter.Eq(x => x.SubjectId, subjectId))
            .ToListAsync(cancellationToken);
        return rows.OrderBy(x => x.SortOrder).ThenByDescending(x => x.CreatedAt).ToList();
    }

    public async Task<ConceptType?> GetActiveByCodeAsync(
        Guid tenantId, Guid subjectId, string conceptTypeCode, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId)
                & Builders<ConceptType>.Filter.Eq(x => x.SubjectId, subjectId)
                & Builders<ConceptType>.Filter.Eq(x => x.ConceptTypeCode, conceptTypeCode)
                & Builders<ConceptType>.Filter.Eq(x => x.ArchivedAt, null))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task InsertAsync(ConceptType entity, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task UpdateAsync(ConceptType entity, CancellationToken cancellationToken)
        => await _collection.ReplaceOneAsync(
            Builders<ConceptType>.Filter.Where(x => x.Id == entity.Id && x.TenantId == entity.TenantId),
            entity, cancellationToken: cancellationToken);
}

/// <summary>MOD-0162 FU03 concept-node persistence.</summary>
public sealed class ConceptNodeRepository : IConceptNodeRepository
{
    public const string CollectionName = "concept_nodes";

    private readonly IMongoCollection<ConceptNode> _collection;

    public ConceptNodeRepository(IMongoDatabase database)
        => _collection = database.GetCollection<ConceptNode>(CollectionName);

    private static FilterDefinition<ConceptNode> Tenant(Guid tenantId)
        => Builders<ConceptNode>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<ConceptNode?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId) & Builders<ConceptNode>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ConceptNode>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken);
        return rows.OrderBy(x => x.ConceptNodeName).ThenByDescending(x => x.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<ConceptNode>> ListBySubjectAsync(
        Guid tenantId, Guid subjectId, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId) & Builders<ConceptNode>.Filter.Eq(x => x.SubjectId, subjectId))
            .ToListAsync(cancellationToken);
        return rows.OrderBy(x => x.ConceptNodeName).ThenByDescending(x => x.CreatedAt).ToList();
    }

    public async Task<ConceptNode?> GetActiveByCodeAsync(
        Guid tenantId, Guid subjectId, Guid conceptTypeId, string conceptNodeCode, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId)
                & Builders<ConceptNode>.Filter.Eq(x => x.SubjectId, subjectId)
                & Builders<ConceptNode>.Filter.Eq(x => x.ConceptTypeId, conceptTypeId)
                & Builders<ConceptNode>.Filter.Eq(x => x.ConceptNodeCode, conceptNodeCode)
                & Builders<ConceptNode>.Filter.Eq(x => x.ArchivedAt, null))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task InsertAsync(ConceptNode entity, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task UpdateAsync(ConceptNode entity, CancellationToken cancellationToken)
        => await _collection.ReplaceOneAsync(
            Builders<ConceptNode>.Filter.Where(x => x.Id == entity.Id && x.TenantId == entity.TenantId),
            entity, cancellationToken: cancellationToken);
}

/// <summary>MOD-0162 FU03 concept-relationship persistence.</summary>
public sealed class ConceptRelationshipRepository : IConceptRelationshipRepository
{
    public const string CollectionName = "concept_relationships";

    private readonly IMongoCollection<ConceptRelationship> _collection;

    public ConceptRelationshipRepository(IMongoDatabase database)
        => _collection = database.GetCollection<ConceptRelationship>(CollectionName);

    private static FilterDefinition<ConceptRelationship> Tenant(Guid tenantId)
        => Builders<ConceptRelationship>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<ConceptRelationship?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId) & Builders<ConceptRelationship>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ConceptRelationship>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken);
        return rows.OrderBy(x => x.Priority).ThenBy(x => x.RelationshipCode).ToList();
    }

    public async Task<IReadOnlyList<ConceptRelationship>> ListBySubjectAsync(
        Guid tenantId, Guid subjectId, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId) & Builders<ConceptRelationship>.Filter.Eq(x => x.SubjectId, subjectId))
            .ToListAsync(cancellationToken);
        return rows.OrderBy(x => x.Priority).ThenBy(x => x.RelationshipCode).ToList();
    }

    public async Task InsertAsync(ConceptRelationship entity, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task UpdateAsync(ConceptRelationship entity, CancellationToken cancellationToken)
        => await _collection.ReplaceOneAsync(
            Builders<ConceptRelationship>.Filter.Where(x => x.Id == entity.Id && x.TenantId == entity.TenantId),
            entity, cancellationToken: cancellationToken);
}

/// <summary>MOD-0162 FU03 concept-chain-template persistence.</summary>
public sealed class ConceptChainTemplateRepository : IConceptChainTemplateRepository
{
    public const string CollectionName = "concept_chain_templates";

    private readonly IMongoCollection<ConceptChainTemplate> _collection;

    public ConceptChainTemplateRepository(IMongoDatabase database)
        => _collection = database.GetCollection<ConceptChainTemplate>(CollectionName);

    private static FilterDefinition<ConceptChainTemplate> Tenant(Guid tenantId)
        => Builders<ConceptChainTemplate>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<ConceptChainTemplate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId) & Builders<ConceptChainTemplate>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ConceptChainTemplate>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken);
        return rows.OrderBy(x => x.ChainCode).ThenByDescending(x => x.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<ConceptChainTemplate>> ListBySubjectAsync(
        Guid tenantId, Guid subjectId, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId) & Builders<ConceptChainTemplate>.Filter.Eq(x => x.SubjectId, subjectId))
            .ToListAsync(cancellationToken);
        return rows.OrderBy(x => x.ChainCode).ThenByDescending(x => x.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<ConceptChainTemplate>> ListByCodeAsync(
        Guid tenantId, Guid subjectId, string chainCode, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId)
                & Builders<ConceptChainTemplate>.Filter.Eq(x => x.SubjectId, subjectId)
                & Builders<ConceptChainTemplate>.Filter.Eq(x => x.ChainCode, chainCode))
            .ToListAsync(cancellationToken);
        return rows.OrderByDescending(x => x.CreatedAt).ToList();
    }

    public async Task InsertAsync(ConceptChainTemplate entity, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task UpdateAsync(ConceptChainTemplate entity, CancellationToken cancellationToken)
        => await _collection.ReplaceOneAsync(
            Builders<ConceptChainTemplate>.Filter.Where(x => x.Id == entity.Id && x.TenantId == entity.TenantId),
            entity, cancellationToken: cancellationToken);
}

/// <summary>MOD-0162 FU03 content ↔ concept link persistence.</summary>
public sealed class KnowledgeContentConceptLinkRepository : IKnowledgeContentConceptLinkRepository
{
    public const string CollectionName = "knowledge_content_concept_links";

    private readonly IMongoCollection<KnowledgeContentConceptLink> _collection;

    public KnowledgeContentConceptLinkRepository(IMongoDatabase database)
        => _collection = database.GetCollection<KnowledgeContentConceptLink>(CollectionName);

    private static FilterDefinition<KnowledgeContentConceptLink> Tenant(Guid tenantId)
        => Builders<KnowledgeContentConceptLink>.Filter.Where(x => x.TenantId == tenantId && !x.IsDeleted);

    public async Task<KnowledgeContentConceptLink?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection.Find(Tenant(tenantId) & Builders<KnowledgeContentConceptLink>.Filter.Eq(x => x.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<KnowledgeContentConceptLink>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken);
        return rows.OrderBy(x => x.SortOrder).ThenByDescending(x => x.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<KnowledgeContentConceptLink>> ListByContentAsync(
        Guid tenantId, Guid knowledgeContentId, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId) & Builders<KnowledgeContentConceptLink>.Filter.Eq(x => x.KnowledgeContentId, knowledgeContentId))
            .ToListAsync(cancellationToken);
        return rows.OrderBy(x => x.SortOrder).ThenByDescending(x => x.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<KnowledgeContentConceptLink>> ListByNodeAsync(
        Guid tenantId, Guid conceptNodeId, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId) & Builders<KnowledgeContentConceptLink>.Filter.Eq(x => x.ConceptNodeId, conceptNodeId))
            .ToListAsync(cancellationToken);
        return rows.OrderBy(x => x.SortOrder).ThenByDescending(x => x.CreatedAt).ToList();
    }

    public async Task InsertAsync(KnowledgeContentConceptLink entity, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(entity, cancellationToken: cancellationToken);

    public async Task UpdateAsync(KnowledgeContentConceptLink entity, CancellationToken cancellationToken)
        => await _collection.ReplaceOneAsync(
            Builders<KnowledgeContentConceptLink>.Filter.Where(x => x.Id == entity.Id && x.TenantId == entity.TenantId),
            entity, cancellationToken: cancellationToken);
}
