using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

public sealed class RoleRepository : RepositoryBase<Role>, IRoleRepository
{
    public RoleRepository(IMongoDatabase database, ITenantContext tenantContext)
        : base(database, tenantContext, "roles")
    {
    }

    public async Task<Role?> GetByIdAndTenantAsync(Guid id, Guid tenantId, CancellationToken ct)
    {
        var filter = Builders<Role>.Filter.And(
            Builders<Role>.Filter.Eq(r => r.Id, id),
            Builders<Role>.Filter.Eq(r => r.TenantId, tenantId),
            Builders<Role>.Filter.Eq(r => r.IsDeleted, false)
        );

        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<Role?> GetByNameAndTenantAsync(string name, Guid tenantId, CancellationToken ct)
    {
        var filter = Builders<Role>.Filter.And(
            Builders<Role>.Filter.Eq(r => r.Name, name),
            Builders<Role>.Filter.Eq(r => r.TenantId, tenantId),
            Builders<Role>.Filter.Eq(r => r.IsDeleted, false)
        );

        return await Collection.Find(filter).FirstOrDefaultAsync(ct);
    }

    public async Task<IEnumerable<Role>> GetAllByTenantAsync(Guid tenantId, CancellationToken ct)
    {
        var filter = Builders<Role>.Filter.And(
            Builders<Role>.Filter.Eq(r => r.TenantId, tenantId),
            Builders<Role>.Filter.Eq(r => r.IsDeleted, false)
        );

        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task<Role> CreateAsync(Role role, CancellationToken ct)
    {
        await InsertOneAsync(role, ct);
        return role;
    }

    public async Task<Role> UpsertSystemRoleAsync(string name, string displayName, string? description, Guid tenantId, CancellationToken ct)
    {
        var filter = Builders<Role>.Filter.And(
            Builders<Role>.Filter.Eq(r => r.TenantId, tenantId),
            Builders<Role>.Filter.Eq(r => r.Name, name),
            Builders<Role>.Filter.Eq(r => r.IsDeleted, false));

        try
        {
            var update = Builders<Role>.Update
                .SetOnInsert(r => r.Id, Guid.NewGuid())
                .SetOnInsert(r => r.TenantId, tenantId)
                .SetOnInsert(r => r.Name, name)
                .SetOnInsert(r => r.DisplayName, displayName)
                .SetOnInsert(r => r.Description, description)
                .SetOnInsert(r => r.IsSystem, true)
                .SetOnInsert(r => r.IsDeleted, false)
                .SetOnInsert(r => r.CreatedAt, DateTimeOffset.UtcNow)
                .SetOnInsert(r => r.CreatedBy, "system");

            await Collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // Parallel provisioners yarışabilir. Duplicate key bu durumda beklenen bir no-op'tur.
        }

        var retried = await Collection.Find(filter).FirstOrDefaultAsync(ct);
        if (retried is not null)
        {
            return retried;
        }

        throw new InvalidOperationException($"System role upsert failed for tenant '{tenantId}' and role '{name}'.");
    }

    public async Task<Role> UpdateAsync(Role role, CancellationToken ct)
    {
        return await ReplaceOneAsync(role, ct);
    }

    public async Task DeleteAsync(Guid id, Guid tenantId, CancellationToken ct)
    {
        var filter = Builders<Role>.Filter.And(
            Builders<Role>.Filter.Eq(r => r.Id, id),
            Builders<Role>.Filter.Eq(r => r.TenantId, tenantId)
        );

        var update = Builders<Role>.Update
            .Set(r => r.IsDeleted, true)
            .Set(r => r.UpdatedAt, DateTimeOffset.UtcNow);

        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }
}
