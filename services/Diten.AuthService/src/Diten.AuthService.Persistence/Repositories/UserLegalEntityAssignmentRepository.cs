using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

public sealed class UserLegalEntityAssignmentRepository : GlobalRepositoryBase<UserLegalEntityAssignment>, IUserLegalEntityAssignmentRepository
{
    public UserLegalEntityAssignmentRepository(IMongoDatabase database)
        : base(database, "user_legal_entity_assignments") { }

    public async Task<IReadOnlyList<UserLegalEntityAssignment>> GetByUserIdAsync(Guid userId, CancellationToken ct)
    {
        var filter = Builders<UserLegalEntityAssignment>.Filter.And(
            IsDeletedFilter,
            Builders<UserLegalEntityAssignment>.Filter.Eq(x => x.UserId, userId));
        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<bool> ExistsAsync(Guid userId, Guid legalEntityId, Guid tenantId, CancellationToken ct)
    {
        var filter = Builders<UserLegalEntityAssignment>.Filter.And(
            IsDeletedFilter,
            Builders<UserLegalEntityAssignment>.Filter.Eq(x => x.UserId, userId),
            Builders<UserLegalEntityAssignment>.Filter.Eq(x => x.LegalEntityId, legalEntityId),
            Builders<UserLegalEntityAssignment>.Filter.Eq(x => x.TenantId, tenantId));
        return await Collection.Find(filter).AnyAsync(ct);
    }

    public async Task<UserLegalEntityAssignment> CreateAsync(UserLegalEntityAssignment assignment, CancellationToken ct)
    {
        await InsertOneAsync(assignment, ct);
        return assignment;
    }
}
