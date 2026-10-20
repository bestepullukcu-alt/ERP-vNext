using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Exceptions;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

public sealed class RolePermissionRepository : RepositoryBase<RolePermission>, IRolePermissionRepository
{
    private readonly IMongoCollection<Permission> _permissionCollection;

    public RolePermissionRepository(IMongoDatabase database, ITenantContext tenantContext) 
        : base(database, tenantContext, "rolePermissions")
    {
        _permissionCollection = database.GetCollection<Permission>("permissions");
    }

    public async Task<IEnumerable<string>> GetPermissionsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct)
    {
        return await GetPermissionsByRolesAsync(new List<Guid> { roleId }, tenantId, ct);
    }

    public async Task<IEnumerable<string>> GetPermissionsByRolesAsync(List<Guid> roleIds, Guid tenantId, CancellationToken ct)
    {
        if (roleIds == null || !roleIds.Any()) return Enumerable.Empty<string>();

        var allRPs = await Collection.Find(rp => rp.IsDeleted == false).ToListAsync(ct);
        var permIds = allRPs
            .Where(rp => roleIds.Contains(rp.RoleId) && rp.TenantId == tenantId)
            .Select(rp => rp.PermissionId)
            .Distinct()
            .ToList();

        if (!permIds.Any()) return Enumerable.Empty<string>();

        var allPerms = await _permissionCollection.Find(p => p.IsDeleted == false).ToListAsync(ct);
        return allPerms
            .Where(p => permIds.Contains(p.Id))
            .Select(p => p.Key);
    }

    public async Task AssignAsync(RolePermission rolePermission, CancellationToken ct)
    {
        try
        {
            await InsertOneAsync(rolePermission, ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // The unique (role, permission, tenant) index is not partial: a SOFT-DELETED grant (Q358 clean-up) still owns
            // the slot. Granting it again revives that document with the new grant's source — only while it is still
            // deleted, so a live grant (or a second writer that revived it first) stays the duplicate it is.
            if (await ReactivateAsync(rolePermission, ct)) return;

            // The role already holds it. Said in the Application's own words.
            throw new DuplicateRolePermissionException(rolePermission.RoleId, rolePermission.PermissionId, ex);
        }
    }

    private async Task<bool> ReactivateAsync(RolePermission rolePermission, CancellationToken ct)
    {
        var filter = Builders<RolePermission>.Filter.And(
            Builders<RolePermission>.Filter.Eq(rp => rp.RoleId, rolePermission.RoleId),
            Builders<RolePermission>.Filter.Eq(rp => rp.PermissionId, rolePermission.PermissionId),
            Builders<RolePermission>.Filter.Eq(rp => rp.TenantId, rolePermission.TenantId),
            Builders<RolePermission>.Filter.Eq(rp => rp.IsDeleted, true));
        var update = Builders<RolePermission>.Update
            .Set(rp => rp.IsDeleted, false)
            .Set(rp => rp.GrantSource, rolePermission.GrantSource)
            .Set(rp => rp.SourceModuleCode, rolePermission.SourceModuleCode)
            .Set(rp => rp.AssignedAt, rolePermission.AssignedAt)
            .Set(rp => rp.AssignedBy, rolePermission.AssignedBy)
            .Set(rp => rp.UpdatedAt, DateTimeOffset.UtcNow)
            .Set(rp => rp.UpdatedBy, rolePermission.AssignedBy);
        var result = await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
        return result.ModifiedCount == 1;
    }

    public async Task RevokeAsync(Guid roleId, Guid permissionId, Guid tenantId, CancellationToken ct)
    {
        var allRPs = await Collection.Find(rp => rp.IsDeleted == false).ToListAsync(ct);
        var target = allRPs.FirstOrDefault(rp => rp.RoleId == roleId && rp.PermissionId == permissionId && rp.TenantId == tenantId);

        if (target != null)
        {
            await Collection.DeleteOneAsync(rp => rp.Id == target.Id, ct);
        }
    }

    public async Task<IReadOnlyList<RolePermission>> GetByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct)
    {
        var filter = Builders<RolePermission>.Filter.And(
            Builders<RolePermission>.Filter.Eq(rp => rp.RoleId, roleId),
            Builders<RolePermission>.Filter.Eq(rp => rp.TenantId, tenantId),
            Builders<RolePermission>.Filter.Eq(rp => rp.IsDeleted, false));

        return await Collection.Find(filter).ToListAsync(ct);
    }

    public async Task RemoveByIdAsync(Guid id, Guid tenantId, CancellationToken ct)
    {
        var filter = Builders<RolePermission>.Filter.And(
            Builders<RolePermission>.Filter.Eq(rp => rp.Id, id),
            Builders<RolePermission>.Filter.Eq(rp => rp.TenantId, tenantId));

        await Collection.DeleteOneAsync(filter, ct);
    }

    // FEAT-CATALOG-PERM-DELETE-SYNC — a catalog permission is global, so removing it clears its grants in every
    // tenant/role (RevokeAsync/RemoveByIdAsync are hard-deletes; this mirrors that across all rows for the id).
    public async Task<long> RemoveByPermissionIdAsync(Guid permissionId, CancellationToken ct)
    {
        var filter = Builders<RolePermission>.Filter.Eq(rp => rp.PermissionId, permissionId);
        var result = await Collection.DeleteManyAsync(filter, ct);
        return result.IsAcknowledged ? result.DeletedCount : 0;
    }
}
