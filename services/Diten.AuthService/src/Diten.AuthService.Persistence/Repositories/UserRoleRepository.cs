using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Domain.Entities;
using MongoDB.Bson;
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

    // BL-521 FIX5 item 4(d) — THE READS ARE BOUNDED TO THE TENANT AND THE USER THEY ARE ASKED ABOUT, in the database. They
    // used to load the whole userRoles and roles collections on every call (every sign-in, refresh and convergence) and
    // filter in memory; the result is the same, the cost no longer grows with every tenant's every assignment. (The
    // existing (UserId, TenantId) index serves the first read; no index or schema changes.)
    public async Task<IEnumerable<string>> GetRolesByUserAsync(Guid userId, Guid tenantId, CancellationToken ct)
    {
        var roleIds = await Collection
            .Find(ur => ur.UserId == userId && ur.TenantId == tenantId && ur.IsDeleted == false)
            .Project(ur => ur.RoleId)
            .ToListAsync(ct);

        if (roleIds.Count == 0) return Enumerable.Empty<string>();

        var distinctRoleIds = roleIds.Distinct().ToList();
        return await _roleCollection
            .Find(r => distinctRoleIds.Contains(r.Id) && r.TenantId == tenantId && r.IsDeleted == false)
            .Project(r => r.Name)
            .ToListAsync(ct);
    }

    // BL-521 FIX5 item 4(c) — A GRANT IS AN IDEMPOTENT UPSERT on user + role + tenant: granting what is already granted
    // changes nothing and fails nothing, with or without the unique index. Two grants at the same moment: the index lets
    // one insert and the other's duplicate-key error is the same "already granted" (and a duplicate that got in without
    // the index is harmless: every read de-duplicates and a revoke removes them all).
    public async Task AssignAsync(UserRole userRole, CancellationToken ct)
    {
        var filter = Builders<UserRole>.Filter.And(
            Builders<UserRole>.Filter.Eq(ur => ur.UserId, userRole.UserId),
            Builders<UserRole>.Filter.Eq(ur => ur.RoleId, userRole.RoleId),
            Builders<UserRole>.Filter.Eq(ur => ur.TenantId, userRole.TenantId));
        var update = new BsonDocumentUpdateDefinition<UserRole>(
            new BsonDocument("$setOnInsert", userRole.ToBsonDocument(Collection.DocumentSerializer)));

        try
        {
            await Collection.UpdateOneAsync(filter, update, new UpdateOptions { IsUpsert = true }, ct);
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            // A concurrent grant of the same user + role + tenant won the insert: it is granted, which is all this asks.
        }
    }

    // BL-521 FIX5 item 4(c) — EVERY matching assignment row is removed, not the first one found: a duplicate left behind
    // kept the role alive after "revoking" it.
    public async Task RevokeAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct)
    {
        await Collection.DeleteManyAsync(
            ur => ur.UserId == userId && ur.RoleId == roleId && ur.TenantId == tenantId && ur.IsDeleted == false,
            ct);
    }

    public async Task<bool> ExistsAsync(Guid userId, Guid roleId, Guid tenantId, CancellationToken ct)
    {
        return await Collection
            .Find(ur => ur.UserId == userId && ur.RoleId == roleId && ur.TenantId == tenantId && ur.IsDeleted == false)
            .AnyAsync(ct);
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
