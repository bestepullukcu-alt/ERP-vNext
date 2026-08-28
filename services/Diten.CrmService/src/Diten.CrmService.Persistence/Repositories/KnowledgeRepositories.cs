using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.CrmService.Persistence.Repositories;

/// <summary>
/// MOD-0162 FU02 knowledge content persistence. Soft-delete aware and tenant scoped. No delete method exists: closing
/// content is the soft archive lifecycle. EffectiveFrom / EffectiveTo / ArchivedAt (DateTimeOffset → BSON array) are
/// never sorted server-side nor used as index keys; ordering happens in memory. The duplicate-code guard excludes
/// archived rows with an <c>ArchivedAt == null</c> equality filter (never <c>$ne</c>, which crash-loops partial indexes).
/// </summary>
public sealed class KnowledgeContentRepository : IKnowledgeContentRepository
{
    public const string CollectionName = "knowledge_contents";

    private readonly IMongoCollection<KnowledgeContent> _collection;

    public KnowledgeContentRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<KnowledgeContent>(CollectionName);
    }

    private static FilterDefinition<KnowledgeContent> Tenant(Guid tenantId)
        => Builders<KnowledgeContent>.Filter.Where(c => c.TenantId == tenantId && !c.IsDeleted);

    public async Task<KnowledgeContent?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId) & Builders<KnowledgeContent>.Filter.Eq(c => c.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<KnowledgeContent>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken);
        return rows.OrderByDescending(c => c.CreatedAt).ToList();
    }

    public async Task<KnowledgeContent?> GetActiveByCodeAsync(
        Guid tenantId, string contentCode, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId)
                & Builders<KnowledgeContent>.Filter.Eq(c => c.ContentCode, contentCode)
                & Builders<KnowledgeContent>.Filter.Eq(c => c.ArchivedAt, null))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task InsertAsync(KnowledgeContent content, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(content, cancellationToken: cancellationToken);

    public async Task UpdateAsync(KnowledgeContent content, CancellationToken cancellationToken)
    {
        var filter = Builders<KnowledgeContent>.Filter.Where(c => c.Id == content.Id && c.TenantId == content.TenantId);
        await _collection.ReplaceOneAsync(filter, content, cancellationToken: cancellationToken);
    }
}

/// <summary>MOD-0162 FU02 subject taxonomy persistence. Same rules as <see cref="KnowledgeContentRepository"/>.</summary>
public sealed class SubjectRepository : ISubjectRepository
{
    public const string CollectionName = "knowledge_subjects";

    private readonly IMongoCollection<Subject> _collection;

    public SubjectRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<Subject>(CollectionName);
    }

    private static FilterDefinition<Subject> Tenant(Guid tenantId)
        => Builders<Subject>.Filter.Where(s => s.TenantId == tenantId && !s.IsDeleted);

    public async Task<Subject?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId) & Builders<Subject>.Filter.Eq(s => s.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Subject>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken);
        return rows.OrderBy(s => s.SortOrder).ThenByDescending(s => s.CreatedAt).ToList();
    }

    public async Task<Subject?> GetActiveByCodeAsync(
        Guid tenantId, string subjectCode, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId)
                & Builders<Subject>.Filter.Eq(s => s.SubjectCode, subjectCode)
                & Builders<Subject>.Filter.Eq(s => s.ArchivedAt, null))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task InsertAsync(Subject subject, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(subject, cancellationToken: cancellationToken);

    public async Task UpdateAsync(Subject subject, CancellationToken cancellationToken)
    {
        var filter = Builders<Subject>.Filter.Where(s => s.Id == subject.Id && s.TenantId == subject.TenantId);
        await _collection.ReplaceOneAsync(filter, subject, cancellationToken: cancellationToken);
    }
}

/// <summary>MOD-0162 FU02 topic taxonomy persistence (subject-scoped hierarchy). Same rules; code is unique within a
/// subject, so the duplicate-code guard is scoped by SubjectId.</summary>
public sealed class TopicRepository : ITopicRepository
{
    public const string CollectionName = "knowledge_topics";

    private readonly IMongoCollection<Topic> _collection;

    public TopicRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<Topic>(CollectionName);
    }

    private static FilterDefinition<Topic> Tenant(Guid tenantId)
        => Builders<Topic>.Filter.Where(t => t.TenantId == tenantId && !t.IsDeleted);

    public async Task<Topic?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId) & Builders<Topic>.Filter.Eq(t => t.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Topic>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken);
        return rows.OrderBy(t => t.SortOrder).ThenByDescending(t => t.CreatedAt).ToList();
    }

    public async Task<IReadOnlyList<Topic>> ListBySubjectAsync(
        Guid tenantId, Guid subjectId, CancellationToken cancellationToken)
    {
        var rows = await _collection
            .Find(Tenant(tenantId) & Builders<Topic>.Filter.Eq(t => t.SubjectId, subjectId))
            .ToListAsync(cancellationToken);
        return rows.OrderBy(t => t.SortOrder).ThenByDescending(t => t.CreatedAt).ToList();
    }

    public async Task<Topic?> GetActiveByCodeAsync(
        Guid tenantId, Guid subjectId, string topicCode, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId)
                & Builders<Topic>.Filter.Eq(t => t.SubjectId, subjectId)
                & Builders<Topic>.Filter.Eq(t => t.TopicCode, topicCode)
                & Builders<Topic>.Filter.Eq(t => t.ArchivedAt, null))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task InsertAsync(Topic topic, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(topic, cancellationToken: cancellationToken);

    public async Task UpdateAsync(Topic topic, CancellationToken cancellationToken)
    {
        var filter = Builders<Topic>.Filter.Where(t => t.Id == topic.Id && t.TenantId == topic.TenantId);
        await _collection.ReplaceOneAsync(filter, topic, cancellationToken: cancellationToken);
    }
}

/// <summary>MOD-0162 FU02 audience-profile persistence. Same rules as <see cref="KnowledgeContentRepository"/>.</summary>
public sealed class AudienceProfileRepository : IAudienceProfileRepository
{
    public const string CollectionName = "knowledge_audience_profiles";

    private readonly IMongoCollection<AudienceProfile> _collection;

    public AudienceProfileRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<AudienceProfile>(CollectionName);
    }

    private static FilterDefinition<AudienceProfile> Tenant(Guid tenantId)
        => Builders<AudienceProfile>.Filter.Where(p => p.TenantId == tenantId && !p.IsDeleted);

    public async Task<AudienceProfile?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId) & Builders<AudienceProfile>.Filter.Eq(p => p.Id, id))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<AudienceProfile>> ListAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var rows = await _collection.Find(Tenant(tenantId)).ToListAsync(cancellationToken);
        return rows.OrderBy(p => p.SortOrder).ThenByDescending(p => p.CreatedAt).ToList();
    }

    public async Task<AudienceProfile?> GetActiveByCodeAsync(
        Guid tenantId, string profileCode, CancellationToken cancellationToken)
        => await _collection
            .Find(Tenant(tenantId)
                & Builders<AudienceProfile>.Filter.Eq(p => p.ProfileCode, profileCode)
                & Builders<AudienceProfile>.Filter.Eq(p => p.ArchivedAt, null))
            .FirstOrDefaultAsync(cancellationToken);

    public async Task InsertAsync(AudienceProfile profile, CancellationToken cancellationToken)
        => await _collection.InsertOneAsync(profile, cancellationToken: cancellationToken);

    public async Task UpdateAsync(AudienceProfile profile, CancellationToken cancellationToken)
    {
        var filter = Builders<AudienceProfile>.Filter.Where(p => p.Id == profile.Id && p.TenantId == profile.TenantId);
        await _collection.ReplaceOneAsync(filter, profile, cancellationToken: cancellationToken);
    }
}
