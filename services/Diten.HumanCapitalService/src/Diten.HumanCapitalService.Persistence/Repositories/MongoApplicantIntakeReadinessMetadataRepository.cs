using Diten.HumanCapitalService.Domain.Entities;
using Diten.HumanCapitalService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.HumanCapitalService.Persistence.Repositories;

public sealed class MongoApplicantIntakeReadinessMetadataRepository : IApplicantIntakeReadinessMetadataRepository
{
    public const string CollectionName = "hcm_applicant_intake_readiness";
    public const string ActiveCodeUniqueIndexName = "ux_hcm_applicant_intake_tenant_code_active";
    public const string TenantStateIndexName = "ix_hcm_applicant_intake_tenant_state";

    private readonly IMongoCollection<ApplicantIntakeReadinessMetadata> _collection;
    private bool _indexesEnsured;

    public MongoApplicantIntakeReadinessMetadataRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<ApplicantIntakeReadinessMetadata>(CollectionName);
    }

    public async Task<IReadOnlyList<ApplicantIntakeReadinessMetadata>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<ApplicantIntakeReadinessMetadata?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<ApplicantIntakeReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<ApplicantIntakeReadinessMetadata>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<ApplicantIntakeReadinessMetadata>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<ApplicantIntakeReadinessMetadata>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<ApplicantIntakeReadinessMetadata>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(ApplicantIntakeReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(metadata, cancellationToken: ct);
    }

    public async Task UpdateAsync(ApplicantIntakeReadinessMetadata metadata, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<ApplicantIntakeReadinessMetadata>.Filter.And(
            Builders<ApplicantIntakeReadinessMetadata>.Filter.Eq(x => x.TenantId, metadata.TenantId),
            Builders<ApplicantIntakeReadinessMetadata>.Filter.Eq(x => x.Id, metadata.Id));
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
            new CreateIndexModel<ApplicantIntakeReadinessMetadata>(
                Builders<ApplicantIntakeReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<ApplicantIntakeReadinessMetadata>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<ApplicantIntakeReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<ApplicantIntakeReadinessMetadata>(
                Builders<ApplicantIntakeReadinessMetadata>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.IntakeState)
                    .Ascending(x => x.SourceChannelState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<ApplicantIntakeReadinessMetadata> ActiveTenantFilter(Guid tenantId) =>
        Builders<ApplicantIntakeReadinessMetadata>.Filter.And(
            Builders<ApplicantIntakeReadinessMetadata>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<ApplicantIntakeReadinessMetadata>.Filter.Eq(x => x.IsDeleted, false));
}
