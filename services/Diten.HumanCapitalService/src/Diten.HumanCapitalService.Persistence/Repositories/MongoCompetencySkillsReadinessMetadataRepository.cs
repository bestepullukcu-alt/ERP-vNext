using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.HumanCapitalService.Persistence.Repositories;

public sealed class MongoCompetencySkillsReadinessMetadataRepository : ICompetencySkillsReadinessMetadataRepository
{
    public const string CollectionName = "hcm_competency_skills_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_hcm_competency_skills_tenant_code_active";
    public const string TenantStateIndexName = "ix_hcm_competency_skills_tenant_state";

    private readonly IMongoCollection<CompetencySkillsReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoCompetencySkillsReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<CompetencySkillsReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<CompetencySkillsReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<CompetencySkillsReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<CompetencySkillsReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<CompetencySkillsReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<CompetencySkillsReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<CompetencySkillsReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<CompetencySkillsReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(CompetencySkillsReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(CompetencySkillsReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<CompetencySkillsReadinessMetadata>.Filter.And(
            Builders<CompetencySkillsReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<CompetencySkillsReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
        await _collection.ReplaceOneAsync(filter, metadata, cancellationToken: ct);
    }

    private async Task EnsureIndexesAsync(CancellationToken ct)
    {
        if (_indexesEnsured)
        {
            return;
        }

        await _collection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<CompetencySkillsReadinessMetadata>(
                Builders<CompetencySkillsReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<CompetencySkillsReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<CompetencySkillsReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<CompetencySkillsReadinessMetadata>(
                Builders<CompetencySkillsReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.CompetencySkillsReadinessState)
                    .Ascending(x => x.AssessmentWorkflowBoundaryState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<CompetencySkillsReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<CompetencySkillsReadinessMetadata>.Filter.And(
            Builders<CompetencySkillsReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<CompetencySkillsReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
