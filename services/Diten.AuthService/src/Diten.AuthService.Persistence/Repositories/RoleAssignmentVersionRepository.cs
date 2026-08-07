using Diten.AuthService.Application.Common.Interfaces;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

/// <summary>
/// MOD-0018-FU13 — persists the per-tenant role-assignment version counter in MongoDB. Increment is a single
/// atomic <c>$inc</c> upsert (FindOneAndUpdate, ReturnDocument.After), so concurrent mutations never lose a bump.
/// </summary>
public sealed class RoleAssignmentVersionRepository : IRoleAssignmentVersionService
{
    private readonly IMongoCollection<RoleAssignmentVersionDocument> _collection;
    private readonly AuthCommonUuidCompatibilityGuard _compatibilityGuard;

    public RoleAssignmentVersionRepository(IMongoDatabase database)
    {
        _collection = database.GetCollection<RoleAssignmentVersionDocument>("auth_role_assignment_versions");
        _compatibilityGuard = new AuthCommonUuidCompatibilityGuard(database);
    }

    public async Task<long> GetAsync(Guid tenantId, CancellationToken ct)
    {
        await _compatibilityGuard.EnsureRoleAssignmentVersionCompatibleAsync(tenantId, ct);
        var doc = await _collection
            .Find(Builders<RoleAssignmentVersionDocument>.Filter.Eq(x => x.TenantId, tenantId))
            .FirstOrDefaultAsync(ct);
        return doc?.Version ?? 0;
    }

    public async Task<long> IncrementAsync(Guid tenantId, CancellationToken ct)
    {
        await _compatibilityGuard.EnsureRoleAssignmentVersionCompatibleAsync(tenantId, ct);
        var update = Builders<RoleAssignmentVersionDocument>.Update
            .Inc(x => x.Version, 1)
            .Set(x => x.UpdatedAt, DateTimeOffset.UtcNow);

        var options = new FindOneAndUpdateOptions<RoleAssignmentVersionDocument>
        {
            IsUpsert = true,
            ReturnDocument = ReturnDocument.After
        };

        var doc = await _collection.FindOneAndUpdateAsync(
            Builders<RoleAssignmentVersionDocument>.Filter.Eq(x => x.TenantId, tenantId),
            update,
            options,
            ct);

        return doc.Version;
    }

    [BsonIgnoreExtraElements]
    internal sealed class RoleAssignmentVersionDocument
    {
        [BsonId]
        public Guid TenantId { get; set; }

        public long Version { get; set; }

        public DateTimeOffset UpdatedAt { get; set; }
    }
}
