using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

public sealed class PermissionRepository : GlobalRepositoryBase<Permission>, IPermissionRepository
{
    public PermissionRepository(IMongoDatabase database) 
        : base(database, "permissions")
    {
    }

    public async Task<Permission?> GetByIdAsync(Guid id, CancellationToken ct)
    {
        return await FindByIdAsync(id, ct);
    }

    public async Task<Permission?> GetByKeyAsync(string key, CancellationToken ct)
    {
        var filter = Builders<Permission>.Filter.And(
            IsDeletedFilter,
            Builders<Permission>.Filter.Eq(p => p.Key, key.ToLowerInvariant())
        );

        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    // FIX-CATALOG-PERM-RESYNC-DUPKEY — no soft-delete filter: the unique-key index still holds a soft-deleted row,
    // so a re-sync must find it here to REACTIVATE rather than insert a duplicate (E11000).
    public async Task<Permission?> GetByKeyIncludingDeletedAsync(string key, CancellationToken ct)
    {
        var filter = Builders<Permission>.Filter.Eq(p => p.Key, key.ToLowerInvariant());
        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IEnumerable<Permission>> GetAllAsync(CancellationToken ct)
    {
        var result = await FindAllAsync(ct);
        return result;
    }

    public async Task<IEnumerable<Permission>> GetByModuleAsync(string module, CancellationToken ct)
    {
        var filter = Builders<Permission>.Filter.And(
            IsDeletedFilter,
            Builders<Permission>.Filter.Eq(p => p.Module, module)
        );

        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<Permission> CreateAsync(Permission permission, CancellationToken ct)
    {
        await InsertOneAsync(permission, ct);
        return permission;
    }

    public async Task UpdateAsync(Permission permission, CancellationToken ct)
    {
        await ReplaceOneAsync(permission, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var filter = Builders<Permission>.Filter.Eq(p => p.Id, id);
        var update = Builders<Permission>.Update
            .Set(p => p.IsDeleted, true)
            .Set(p => p.UpdatedAt, DateTimeOffset.UtcNow);

        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    // FIX-CATALOG-PERM-REACTIVATE-PERSIST — Id-only filter (NO IsDeletedFilter), mirroring DeleteAsync, so it can
    // revive a soft-deleted doc. The filtered ReplaceOne (UpdateAsync) matches zero rows on IsDeleted=true and would
    // silently no-op, leaving the permission deleted while grants get re-attached (the bug this fixes).
    public async Task ReactivateAsync(Guid id, string displayName, string? description, CancellationToken ct)
    {
        var filter = Builders<Permission>.Filter.Eq(p => p.Id, id);
        var update = Builders<Permission>.Update
            .Set(p => p.IsDeleted, false)
            .Set(p => p.IsSystem, false) // catalog-owned (user-defined) so a later DELETE-sync may remove it again
            .Set(p => p.DisplayName, displayName)
            .Set(p => p.Description, description)
            .Set(p => p.UpdatedAt, DateTimeOffset.UtcNow);

        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
}
