using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Repositories;

public sealed class UserRoleRepository : RepositoryBase<UserRole>, IUserRoleRepository
{
    private readonly IMongoCollection<Role> _roleCollection;

    public UserRoleRepository(IMongoDatabase database, ITenantContext tenantContext) 
        : base(database, tenantContext, "userRoles")
    {
        _roleCollection = database.GetCollection<Role>("roles");
    }

    public async Task<IEnumerable<string>> GetRolesByUserAsync(Guid userId, Guid tenantId, CancellationToken ct)
    {
        var userRoles = await Collection.Find(ur => ur.IsDeleted == false).ToListAsync(ct);
        var roleIds = userRoles
            .Where(ur => ur.UserId == userId && ur.TenantId == tenantId)
            .Select(ur => ur.RoleId)
            .ToList();

        if (!roleIds.Any()) return Enumerable.Empty<string>();

        var allRoles = await _roleCollection.Find(r => r.IsDeleted == false).ToListAsync(ct);
        return allRoles
            .Where(r => roleIds.Contains(r.Id) && r.TenantId == tenantId)
            .Select(r => r.Name);
    }

    public async Task AssignAsync(UserRole userRole, CancellationToken ct)
    {
        await InsertOneAsync(userRole, ct);
    }

    public async Task RevokeAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct)
    {
        var userRoles = await Collection.Find(ur => ur.IsDeleted == false).ToListAsync(ct);
        var target = userRoles.FirstOrDefault(ur => ur.UserId == userId && ur.RoleId == roleId && ur.TenantId == tenantId);
        
        if (target != null)
        {
            await Collection.DeleteOneAsync(ur => ur.Id == target.Id, ct);
        }
    }

    public async Task<bool> ExistsAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct)
    {
        var userRoles = await Collection.Find(ur => ur.IsDeleted == false).ToListAsync(ct);
        return userRoles.Any(ur => ur.UserId == userId && ur.RoleId == roleId && ur.TenantId == tenantId);
    }

    // AG-STEP-010 / MOD-0018-FU13 Group C — distinct, tenant-scoped holder user ids (active rows only). The role,
    // tenant and lifecycle predicate is applied SERVER-SIDE in the Mongo Find filter (RoleId + TenantId + IsDeleted),
    // and only the UserId field is projected — no full-collection scan and no in-process tenant/role filtering. Tenant
    // scope is mandatory: a cross-tenant user id can never be returned.
    public async Task<IReadOnlyCollection<Guid>> GetUserIdsByRoleAsync(Guid roleId, Guid tenantId, CancellationToken ct)
    {
        var holderIds = await Collection
            .Find(ur => ur.RoleId == roleId && ur.TenantId == tenantId && ur.IsDeleted == false)
            .Project(ur => ur.UserId)
            .ToListAsync(ct);

        return holderIds.Distinct().ToList();
    }
}
