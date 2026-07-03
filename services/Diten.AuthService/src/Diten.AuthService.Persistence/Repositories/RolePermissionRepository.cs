using Diten.AuthService.Application.Common;
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
        await InsertOneAsync(rolePermission, ct);
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
