using Diten.AuthService.Application.Common;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Repositories;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Persistence;

/// <summary>
/// D5 (plan v10.0, multi-tenancy soft-delete rule): every Auth soft delete records <c>DeletedAt</c> (UTC, the same
/// instant as <c>UpdatedAt</c>) and every reactivation clears it. Rows deleted before the field existed keep it null
/// (no backfill). Owned replica set only.
/// </summary>
[Collection(AuthReconciliationMongoCollection.Name)]
[Trait("Category", "Mongo")]
public sealed class SoftDeleteDeletedAtMongoTests(DisposableAuthMongoReplicaSet ownedMongo)
{
    private IMongoDatabase Db => ownedMongo.Database;

    private static TenantContext Tenant(Guid tenantId)
    {
        var context = new TenantContext();
        context.SetTenant(tenantId);
        return context;
    }

    [OwnedMongoFact]
    public async Task Role_delete_sets_DeletedAt()
    {
        await ownedMongo.ResetAsync();
        var tenantId = Guid.NewGuid();
        var role = new Role("Planner", "Planner", "d5 fixture", tenantId);
        var roles = Db.GetCollection<Role>("roles");
        await roles.InsertOneAsync(role);

        await new RoleRepository(Db, Tenant(tenantId)).DeleteAsync(role.Id, tenantId, "d5-test", CancellationToken.None);

        var stored = await roles.Find(r => r.Id == role.Id).SingleAsync();
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(stored.UpdatedAt, stored.DeletedAt);
        Assert.Equal("d5-test", stored.UpdatedBy);
    }

    [OwnedMongoFact]
    public async Task User_soft_delete_sets_DeletedAt()
    {
        await ownedMongo.ResetAsync();
        var tenantId = Guid.NewGuid();
        var user = new User($"d5-{Guid.NewGuid():N}@example.test", "hash", "D", "Five", tenantId);
        var users = Db.GetCollection<User>("users");
        await users.InsertOneAsync(user);

        await new UserRepository(Db, Tenant(tenantId)).SoftDeleteAsync(user.Id, tenantId, CancellationToken.None);

        var stored = await users.Find(u => u.Id == user.Id).SingleAsync();
        Assert.True(stored.IsDeleted);
        Assert.NotNull(stored.DeletedAt);
        Assert.Equal(stored.UpdatedAt, stored.DeletedAt);
    }

    [OwnedMongoFact]
    public async Task Permission_delete_sets_DeletedAt_and_reactivation_clears_it()
    {
        await ownedMongo.ResetAsync();
        var permission = new Permission("d5", "records", "read", "D5 read", null);
        var permissions = Db.GetCollection<Permission>("permissions");
        await permissions.InsertOneAsync(permission);
        var repository = new PermissionRepository(Db);

        await repository.DeleteAsync(permission.Id, CancellationToken.None);
        var deleted = await permissions.Find(p => p.Id == permission.Id).SingleAsync();
        Assert.True(deleted.IsDeleted);
        Assert.NotNull(deleted.DeletedAt);
        Assert.Equal(deleted.UpdatedAt, deleted.DeletedAt);

        await repository.ReactivateAsync(permission.Id, "D5 read again", null, CancellationToken.None);
        var revived = await permissions.Find(p => p.Id == permission.Id).SingleAsync();
        Assert.False(revived.IsDeleted);
        Assert.Null(revived.DeletedAt);
    }

    // The e-sign window collection is not one of the fixture's nine, and ResetAsync refuses unknown collections; this
    // test therefore uses its own database on the owned server and drops it, leaving the shared database untouched.
    [OwnedMongoFact]
    public async Task ESign_window_upsert_clears_DeletedAt_of_a_soft_deleted_window()
    {
        var name = "d5_esign_" + Guid.NewGuid().ToString("N");
        var db = ownedMongo.Client.GetDatabase(name);
        try
        {
            var tenantId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var now = DateTime.UtcNow;
            var old = new ESignReauthWindow(tenantId, userId, "ua", now.AddMinutes(-30), now.AddMinutes(-15))
            {
                IsDeleted = true,
                DeletedAt = DateTimeOffset.UtcNow.AddMinutes(-10)
            };
            var windows = db.GetCollection<ESignReauthWindow>(ESignReauthWindowRepository.CollectionName);
            await windows.InsertOneAsync(old);

            await new ESignReauthWindowRepository(db, Tenant(tenantId)).UpsertAsync(
                new ESignReauthWindow(tenantId, userId, "ua", now, now.AddMinutes(15)), CancellationToken.None);

            var stored = await windows.Find(w => w.TenantId == tenantId && w.UserId == userId).SingleAsync();
            Assert.False(stored.IsDeleted);
            Assert.Null(stored.DeletedAt);
        }
        finally
        {
            await ownedMongo.Client.DropDatabaseAsync(name);
        }
    }
}
