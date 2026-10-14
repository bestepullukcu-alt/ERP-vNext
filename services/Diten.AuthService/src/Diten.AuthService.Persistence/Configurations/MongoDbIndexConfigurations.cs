using Diten.AuthService.Persistence.Repositories;
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

    /// <summary>WP-USERS-ROLES-CLOSE-FIX1 FIX2 item 5 — the set-password link's lookup.</summary>
    public const string PasswordResetTokenHashIndexName = "ix_users_password_reset_token_hash_v2";

    /// <summary>FIX2's sparse index (held every user: the field is written as null). No longer created; to be removed later.</summary>
    public const string RetiredSparsePasswordResetTokenHashIndexName = "ix_users_password_reset_token_hash";

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
            new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(u => u.IsActive).Ascending(u => u.TenantId)),
            // WP-USERS-ROLES-CLOSE-FIX1 FIX2 item 5 — the set-password link finds its account by the link's hash, across
            // tenants and anonymously (the page's check and the redemption): without an index every such request scanned
            // the whole users collection. FIX3 item 7 — PARTIAL on a string value: User writes PasswordResetTokenHash: null
            // explicitly, so FIX2's sparse index held every user. A new NAME (an index of the same name with other options
            // cannot be created over it); FIX2's ix_users_password_reset_token_hash is no longer created here and is NOT
            // dropped — remove it later where it exists.
            new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(u => u.PasswordResetTokenHash),
                new CreateIndexOptions<User>
                {
                    Name = PasswordResetTokenHashIndexName,
                    PartialFilterExpression = Builders<User>.Filter.Type(u => u.PasswordResetTokenHash, MongoDB.Bson.BsonType.String)
                })
        });

        await EnsurePermissionOnboardingIndexesAsync(database);

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

        // MOD-0022 E1 — e-signature re-authentication tickets (hash only, single use) and KS-3 windows.
        var esignTicketCol = database.GetCollection<ESignReauthTicket>(ESignReauthTicketRepository.CollectionName);
        await esignTicketCol.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<ESignReauthTicket>(
                Builders<ESignReauthTicket>.IndexKeys.Ascending(x => x.TicketHash),
                new CreateIndexOptions { Unique = true, Name = "ux_esign_ticket_hash" }),
            new CreateIndexModel<ESignReauthTicket>(
                Builders<ESignReauthTicket>.IndexKeys.Ascending(x => x.ExpiresAtUtc),
                new CreateIndexOptions { ExpireAfter = TimeSpan.FromHours(1), Name = "ttl_esign_ticket_expiry" })
        });

        var esignWindowCol = database.GetCollection<ESignReauthWindow>(ESignReauthWindowRepository.CollectionName);
        await esignWindowCol.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<ESignReauthWindow>(
                Builders<ESignReauthWindow>.IndexKeys.Ascending(x => x.TenantId).Ascending(x => x.UserId),
                new CreateIndexOptions { Unique = true, Name = "ux_esign_window_tenant_user" }),
            new CreateIndexModel<ESignReauthWindow>(
                Builders<ESignReauthWindow>.IndexKeys.Ascending(x => x.ExpiresAtUtc),
                new CreateIndexOptions { ExpireAfter = TimeSpan.FromHours(1), Name = "ttl_esign_window_expiry" })
        });

        await EnsureServiceIdentityIndexesAsync(database);
    }

    public static async Task EnsurePermissionOnboardingIndexesAsync(IMongoDatabase database)
    {
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
    }

    public static async Task EnsureServiceIdentityIndexesAsync(IMongoDatabase database)
    {
        var serviceClients = database.GetCollection<ServiceClientIdentity>("serviceClientIdentities");
        await serviceClients.Indexes.CreateOneAsync(new CreateIndexModel<ServiceClientIdentity>(
            Builders<ServiceClientIdentity>.IndexKeys.Ascending(x => x.ClientCode),
            new CreateIndexOptions<ServiceClientIdentity>
            {
                Unique = true,
                Name = "ux_service_client_identity_code_active",
                PartialFilterExpression = Builders<ServiceClientIdentity>.Filter.Eq(x => x.IsDeleted, false)
            }));

        var serviceGrants = database.GetCollection<ServiceClientTenantGrant>("serviceClientTenantGrants");
        await serviceGrants.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<ServiceClientTenantGrant>(
                Builders<ServiceClientTenantGrant>.IndexKeys
                    .Ascending(x => x.TenantId).Ascending(x => x.ServiceClientIdentityId).Ascending(x => x.Audience),
                new CreateIndexOptions<ServiceClientTenantGrant>
                {
                    Unique = true,
                    Name = "ux_service_client_grant_tenant_client_audience_active",
                    PartialFilterExpression = Builders<ServiceClientTenantGrant>.Filter.Eq(x => x.IsDeleted, false)
                }),
            new CreateIndexModel<ServiceClientTenantGrant>(
                Builders<ServiceClientTenantGrant>.IndexKeys
                    .Ascending(x => x.TenantId).Ascending(x => x.IsEnabled),
                new CreateIndexOptions { Name = "ix_service_client_grant_tenant_enabled" })
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
