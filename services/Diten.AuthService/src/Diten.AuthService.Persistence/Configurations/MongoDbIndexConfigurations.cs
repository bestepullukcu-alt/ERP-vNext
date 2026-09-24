using Diten.AuthService.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Configurations;

public static class MongoDbIndexConfigurations
{
    /// <summary>
    /// WP-AUTH-INVITED-LIFECYCLE-01 — the users e-mail unique index, PARTIAL on IsDeleted=false: a soft-deleted
    /// user no longer holds its address, so the same e-mail can open a NEW account (owner decision A) while the old
    /// record stays with its history. Same keys as before ({ Email, TenantId }); only live users are unique.
    /// </summary>
    public const string UserEmailIndexName = "ux_users_email_tenant_active";

    /// <summary>The driver-default name of the index it replaces: unique on { Email, TenantId }, deleted users included.</summary>
    public const string LegacyUserEmailIndexName = "Email_1_TenantId_1";

    public static async Task EnsureIndexesAsync(IMongoDatabase database)
    {
        // Users
        var usersCol = database.GetCollection<User>("users");
        await EnsureUserEmailIndexAsync(usersCol);
        await usersCol.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<User>(
                Builders<User>.IndexKeys.Ascending(u => u.TenantId).Ascending(u => u.NormalizedUserName),
                new CreateIndexOptions<User>
                {
                    Unique = true,
                    Name = "ux_users_tenant_normalized_username",
                    PartialFilterExpression = Builders<User>.Filter.And(
                        Builders<User>.Filter.Eq(u => u.IsDeleted, false),
                        Builders<User>.Filter.Exists(u => u.NormalizedUserName, true),
                        Builders<User>.Filter.Gt(u => u.NormalizedUserName, string.Empty))
                }),
            new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(u => u.TenantId)),
            new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(u => u.IsActive).Ascending(u => u.TenantId))
        });

        // Roles
        var rolesCol = database.GetCollection<Role>("roles");
        await rolesCol.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Role>(
                Builders<Role>.IndexKeys.Ascending(r => r.TenantId).Ascending(r => r.Name),
                new CreateIndexOptions<Role>
                {
                    Unique = true,
                    Name = "uq_roles_tenant_name_active",
                    PartialFilterExpression = Builders<Role>.Filter.Eq(r => r.IsDeleted, false)
                }),
            new CreateIndexModel<Role>(Builders<Role>.IndexKeys.Ascending(r => r.TenantId))
        });

        // Permissions
        var permissionsCol = database.GetCollection<Permission>("permissions");
        await permissionsCol.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<Permission>(Builders<Permission>.IndexKeys.Ascending(p => p.Key), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<Permission>(Builders<Permission>.IndexKeys.Ascending("Module").Ascending("Resource").Ascending("Action"), new CreateIndexOptions { Unique = true })
        });

        // UserRoles
        var userRolesCol = database.GetCollection<UserRole>("userRoles");
        await userRolesCol.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<UserRole>(Builders<UserRole>.IndexKeys.Ascending("UserId").Ascending("RoleId").Ascending("TenantId"), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<UserRole>(Builders<UserRole>.IndexKeys.Ascending("UserId").Ascending("TenantId"))
        });

        // RolePermissions
        var rolePermissionsCol = database.GetCollection<RolePermission>("rolePermissions");
        await rolePermissionsCol.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<RolePermission>(Builders<RolePermission>.IndexKeys.Ascending("RoleId").Ascending("PermissionId").Ascending("TenantId"), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<RolePermission>(Builders<RolePermission>.IndexKeys.Ascending("RoleId").Ascending("TenantId"))
        });

        // RefreshTokens
        var refreshTokensCol = database.GetCollection<RefreshToken>("refreshTokens");
        await refreshTokensCol.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<RefreshToken>(Builders<RefreshToken>.IndexKeys.Ascending(t => t.Token), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<RefreshToken>(Builders<RefreshToken>.IndexKeys.Ascending("UserId").Ascending("TenantId")),
            new CreateIndexModel<RefreshToken>(Builders<RefreshToken>.IndexKeys.Ascending(t => t.ExpiresAt), new CreateIndexOptions { ExpireAfter = TimeSpan.Zero })
        });

        // Integration event inbox (event_id idempotency)
        var inboxCol = database.GetCollection<ProcessedIntegrationEvent>("integrationEventInbox");
        await inboxCol.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<ProcessedIntegrationEvent>(
                Builders<ProcessedIntegrationEvent>.IndexKeys.Ascending(x => x.EventId),
                new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<ProcessedIntegrationEvent>(
                Builders<ProcessedIntegrationEvent>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.ProcessedAt))
        });

        // Auth audit logs
        var auditCol = database.GetCollection<AuthAuditLog>("authAuditLogs");
        await auditCol.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<AuthAuditLog>(
                Builders<AuthAuditLog>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.OccurredAt)),
            new CreateIndexModel<AuthAuditLog>(
                Builders<AuthAuditLog>.IndexKeys.Ascending(x => x.EventName).Ascending(x => x.OccurredAt))
        });

        var mfaCol = database.GetCollection<MfaChallenge>("mfaChallenges");
        await mfaCol.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<MfaChallenge>(
                Builders<MfaChallenge>.IndexKeys.Ascending(x => x.ChallengeIdHash),
                new CreateIndexOptions { Unique = true, Name = "ux_mfa_challenge_hash" }),
            new CreateIndexModel<MfaChallenge>(
                Builders<MfaChallenge>.IndexKeys.Ascending(x => x.ExpiresAtUtc),
                new CreateIndexOptions { ExpireAfter = TimeSpan.FromHours(1), Name = "ttl_mfa_challenge_expiry" })
        });
    }

    /// <summary>
    /// WP-AUTH-INVITED-LIFECYCLE-01 — idempotent startup step for the users e-mail index.
    ///
    /// <para>The legacy <see cref="LegacyUserEmailIndexName"/> covered deleted users too, so a deleted user's e-mail
    /// could never be used again (the insert died with E11000 → 500). It is dropped FIRST, by name, if present: creating
    /// the partial index beside it would leave the old rule in force, and creating anything under the old name with
    /// different options throws IndexOptionsConflict — which <c>EnsureIndexesAsync</c>'s caller swallows, silently
    /// skipping every later index AND the seeder. Then the partial index is created under its own name. Running it
    /// again finds nothing to drop and an identical index to create: a no-op.</para>
    ///
    /// <para>The window between drop and create has no e-mail uniqueness; it is startup-only, and the handlers probe
    /// live duplicates before inserting anyway.</para>
    /// </summary>
    public static async Task EnsureUserEmailIndexAsync(IMongoCollection<User> users)
    {
        List<BsonDocument> existing;
        try
        {
            using var cursor = await users.Indexes.ListAsync();
            existing = await cursor.ToListAsync();
        }
        catch (MongoCommandException ex) when (ex.Code == 26) // NamespaceNotFound: a fresh database has no users yet
        {
            existing = [];
        }

        if (existing.Any(index => index.TryGetValue("name", out var name) && name.IsString && name.AsString == LegacyUserEmailIndexName))
        {
            await users.Indexes.DropOneAsync(LegacyUserEmailIndexName);
        }

        await users.Indexes.CreateOneAsync(new CreateIndexModel<User>(
            Builders<User>.IndexKeys.Ascending(u => u.Email).Ascending(u => u.TenantId),
            new CreateIndexOptions<User>
            {
                Unique = true,
                Name = UserEmailIndexName,
                PartialFilterExpression = Builders<User>.Filter.Eq(u => u.IsDeleted, false)
            }));
    }
}
