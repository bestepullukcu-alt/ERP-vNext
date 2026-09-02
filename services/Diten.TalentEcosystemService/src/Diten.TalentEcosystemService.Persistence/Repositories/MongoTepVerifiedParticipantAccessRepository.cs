using Diten.TalentEcosystemService.Domain.Entities;
using Diten.TalentEcosystemService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.TalentEcosystemService.Persistence.Repositories;

public sealed class MongoTepVerifiedParticipantAccessRepository : ITepVerifiedParticipantAccessRepository
{
    public const string CollectionName = "tep_verified_participant_access";
    public const string ActiveCodeUniqueIndexName = "ux_tep_verified_participants_tenant_code_active";
    public const string TenantStateIndexName = "ix_tep_verified_participants_tenant_state";

    private readonly IMongoCollection<TepVerifiedParticipantAccess> _collection;
    private bool _indexesEnsured;

    public MongoTepVerifiedParticipantAccessRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<TepVerifiedParticipantAccess>(CollectionName);
    }

    public async Task<IReadOnlyList<TepVerifiedParticipantAccess>> ListAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        return await _collection
            .Find(ActiveTenantFilter(tenantId))
            .SortBy(x => x.Code)
            .ToListAsync(ct);
    }

    public async Task<TepVerifiedParticipantAccess?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepVerifiedParticipantAccess>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<TepVerifiedParticipantAccess>.Filter.Eq(x => x.Id, id));
        return await _collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> ExistsActiveCodeAsync(Guid tenantId, string code, Guid? excludingId, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepVerifiedParticipantAccess>.Filter.And(
            ActiveTenantFilter(tenantId),
            Builders<TepVerifiedParticipantAccess>.Filter.Eq(x => x.Code, code));

        if (excludingId is { } id)
        {
            filter &= Builders<TepVerifiedParticipantAccess>.Filter.Ne(x => x.Id, id);
        }

        return await _collection.CountDocumentsAsync(filter, cancellationToken: ct) > 0;
    }

    public async Task CreateAsync(TepVerifiedParticipantAccess access, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        await _collection.InsertOneAsync(access, cancellationToken: ct);
    }

    public async Task UpdateAsync(TepVerifiedParticipantAccess access, CancellationToken ct)
    {
        await EnsureIndexesAsync(ct);
        var filter = Builders<TepVerifiedParticipantAccess>.Filter.And(
            Builders<TepVerifiedParticipantAccess>.Filter.Eq(x => x.TenantId, access.TenantId),
            Builders<TepVerifiedParticipantAccess>.Filter.Eq(x => x.Id, access.Id));
        await _collection.ReplaceOneAsync(filter, access, cancellationToken: ct);
    }

    private async Task EnsureIndexesAsync(CancellationToken ct)
    {
        if (_indexesEnsured)
        {
            return;
        }

        await _collection.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<TepVerifiedParticipantAccess>(
                Builders<TepVerifiedParticipantAccess>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.Code),
                new CreateIndexOptions<TepVerifiedParticipantAccess>
                {
                    Unique = true,
                    Name = ActiveCodeUniqueIndexName,
                    PartialFilterExpression = Builders<TepVerifiedParticipantAccess>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<TepVerifiedParticipantAccess>(
                Builders<TepVerifiedParticipantAccess>.IndexKeys
                    .Ascending(x => x.TenantId)
                    .Ascending(x => x.VerificationState)
                    .Ascending(x => x.AccessState)
                    .Ascending(x => x.PolicyEvaluationState)
                    .Ascending(x => x.VisibilityApprovalState)
                    .Ascending(x => x.IsDeleted),
                new CreateIndexOptions { Name = TenantStateIndexName })
        }, ct);

        _indexesEnsured = true;
    }

    private static FilterDefinition<TepVerifiedParticipantAccess> ActiveTenantFilter(Guid tenantId) =>
        Builders<TepVerifiedParticipantAccess>.Filter.And(
            Builders<TepVerifiedParticipantAccess>.Filter.Eq(x => x.TenantId, tenantId),
            Builders<TepVerifiedParticipantAccess>.Filter.Eq(x => x.IsDeleted, false));
}
