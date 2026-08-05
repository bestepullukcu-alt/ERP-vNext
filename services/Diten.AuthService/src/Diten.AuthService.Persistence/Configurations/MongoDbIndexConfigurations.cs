using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Domain.S2S;
using Diten.AuthService.Persistence.Repositories;
using MongoDB.Driver;

namespace Diten.AuthService.Persistence.Configurations;

public static class MongoDbIndexConfigurations
{
    public static async Task EnsureIndexesAsync(IMongoDatabase database)
    {
        // Users
        var usersCol = database.GetCollection<User>("users");
        await usersCol.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<User>(Builders<User>.IndexKeys.Ascending(u => u.Email).Ascending(u => u.TenantId), new CreateIndexOptions { Unique = true }),
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
            new CreateIndexModel<RolePermission>(
                Builders<RolePermission>.IndexKeys.Ascending("RoleId").Ascending("PermissionId").Ascending("TenantId"),
                new CreateIndexOptions { Unique = true, Name = RolePermissionRepository.AssignmentUniqueIndexName }),
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

        // FU16-A global S2S identity registry. Mongo's default binary collation preserves exact case-sensitive values.
        var servicePrincipals = database.GetCollection<ServicePrincipal>(ServicePrincipalRepository.CollectionName);
        await servicePrincipals.Indexes.CreateOneAsync(new CreateIndexModel<ServicePrincipal>(
            Builders<ServicePrincipal>.IndexKeys.Ascending(x => x.ClientId),
            new CreateIndexOptions { Unique = true, Name = ServicePrincipalRepository.ClientIdUniqueIndexName }));

        var credentials = database.GetCollection<ServiceCredentialDescriptor>(ServiceCredentialDescriptorRepository.CollectionName);
        await credentials.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<ServiceCredentialDescriptor>(
                Builders<ServiceCredentialDescriptor>.IndexKeys.Ascending(x => x.CredentialId),
                new CreateIndexOptions { Unique = true, Name = ServiceCredentialDescriptorRepository.CredentialIdUniqueIndexName }),
            new CreateIndexModel<ServiceCredentialDescriptor>(
                Builders<ServiceCredentialDescriptor>.IndexKeys.Ascending(x => x.Kid),
                new CreateIndexOptions { Unique = true, Name = ServiceCredentialDescriptorRepository.KidUniqueIndexName }),
            new CreateIndexModel<ServiceCredentialDescriptor>(
                Builders<ServiceCredentialDescriptor>.IndexKeys.Ascending(x => x.ServicePrincipalId).Ascending(x => x.Generation))
        });

        var replayReceipts = database.GetCollection<S2SReplayReceipt>(S2SReplayReceiptStore.CollectionName);
        await replayReceipts.Indexes.CreateManyAsync(new[]
        {
            new CreateIndexModel<S2SReplayReceipt>(
                Builders<S2SReplayReceipt>.IndexKeys.Ascending(x => x.Issuer).Ascending(x => x.Jti),
                new CreateIndexOptions { Unique = true, Name = S2SReplayReceiptStore.IssuerJtiUniqueIndexName }),
            new CreateIndexModel<S2SReplayReceipt>(
                Builders<S2SReplayReceipt>.IndexKeys.Ascending(x => x.Issuer).Ascending(x => x.Nonce),
                new CreateIndexOptions { Unique = true, Name = S2SReplayReceiptStore.IssuerNonceUniqueIndexName })
        });
    }
}
