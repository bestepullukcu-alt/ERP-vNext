using Diten.HcmService.Domain.Entities;
using Diten.HcmService.Domain.Repositories;
using MongoDB.Driver;

namespace Diten.HcmService.Persistence.Repositories;

public sealed class EmployeeDraftSessionRepository : IEmployeeDraftSessionRepository
{
    private const string CollectionName = "hcm_employee_draft_sessions";
    private readonly IMongoCollection<EmployeeDraftSession> _collection;

    public EmployeeDraftSessionRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<EmployeeDraftSession>(CollectionName);
    }

    public async Task<EmployeeDraftSession?> GetByIdAsync(Guid tenantId, Guid draftSessionId, CancellationToken cancellationToken)
    {
        var filter = Builders<EmployeeDraftSession>.Filter.And(
            Builders<EmployeeDraftSession>.Filter.Eq(session => session.Id, draftSessionId),
            Builders<EmployeeDraftSession>.Filter.Eq(session => session.TenantId, tenantId),
            Builders<EmployeeDraftSession>.Filter.Eq(session => session.IsDeleted, false));

        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<EmployeeDraftSession?> GetByCreateIdempotencyKeyAsync(Guid tenantId, string idempotencyKeyHash, CancellationToken cancellationToken)
    {
        var filter = Builders<EmployeeDraftSession>.Filter.And(
            Builders<EmployeeDraftSession>.Filter.Eq(session => session.TenantId, tenantId),
            Builders<EmployeeDraftSession>.Filter.Eq(session => session.CreateIdempotencyKeyHash, idempotencyKeyHash),
            Builders<EmployeeDraftSession>.Filter.Eq(session => session.IsDeleted, false));

        return await _collection.Find(filter).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(EmployeeDraftSession draftSession, CancellationToken cancellationToken)
    {
        await _collection.InsertOneAsync(draftSession, cancellationToken: cancellationToken);
    }

    public async Task<bool> ReplaceAsync(EmployeeDraftSession draftSession, int expectedVersion, CancellationToken cancellationToken)
    {
        var filter = Builders<EmployeeDraftSession>.Filter.And(
            Builders<EmployeeDraftSession>.Filter.Eq(session => session.Id, draftSession.Id),
            Builders<EmployeeDraftSession>.Filter.Eq(session => session.TenantId, draftSession.TenantId),
            Builders<EmployeeDraftSession>.Filter.Eq(session => session.Version, expectedVersion),
            Builders<EmployeeDraftSession>.Filter.Eq(session => session.IsDeleted, false));

        var result = await _collection.ReplaceOneAsync(filter, draftSession, cancellationToken: cancellationToken);
        return result.ModifiedCount == 1;
    }
}
