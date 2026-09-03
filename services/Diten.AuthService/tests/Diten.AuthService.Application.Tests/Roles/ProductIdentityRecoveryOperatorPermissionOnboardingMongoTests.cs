using System.Text.Json;
using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Infrastructure.Eventing;
using Diten.AuthService.Persistence.Repositories;
using Diten.Platform.Application.Contracts.Eventing;
using Diten.Platform.Contracts.Events;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Diten.AuthService.Application.Tests.Roles;

[Collection(AuthPermissionOnboardingMongoCollectionDefinition.Name)]
public sealed class ProductIdentityRecoveryOperatorPermissionOnboardingMongoTests
{
    private const string DatabaseName = "diten_auth_permission_onboarding_itest";

    [Fact]
    public async Task Real_mongo_reconciles_replays_revokes_and_fences_tenant_wide_contamination()
    {
        var client = CreateClient();
        await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        var database = client.GetDatabase(DatabaseName);
        await database.GetCollection<ProcessedIntegrationEvent>("integrationEventInbox").Indexes.CreateOneAsync(
            new CreateIndexModel<ProcessedIntegrationEvent>(
                Builders<ProcessedIntegrationEvent>.IndexKeys.Ascending(item => item.EventId),
                new CreateIndexOptions { Unique = true }));
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var permissionIds = new List<Guid>();
        var tenantContext = new TenantContext();
        var permissions = new PermissionRepository(database);
        var roles = new RoleRepository(database, tenantContext);
        var grants = new RolePermissionRepository(database, tenantContext);
        var service = new EntitlementPermissionSyncService(
            permissions,
            roles,
            grants,
            NullLogger<EntitlementPermissionSyncService>.Instance);

        try
        {
            await CleanupAsync(database, [tenantA, tenantB], permissionIds);
            await CleanupExactRecoveryAsync(database);
            var recovery = new Permission(
                "mdm",
                ProductIdentityRecoveryOperatorEntitlementGrantProfile.Resource,
                ProductIdentityRecoveryOperatorEntitlementGrantProfile.Action,
                "Recover",
                null,
                moduleOverride: ProductIdentityRecoveryOperatorEntitlementGrantProfile.ModuleCode,
                scope: PermissionScope.Tenant);
            var adjacent = new Permission(
                "auth",
                "users",
                "read",
                "Read Users",
                null,
                moduleOverride: "access-governance",
                scope: PermissionScope.Tenant);
            permissionIds.Add(recovery.Id);
            permissionIds.Add(adjacent.Id);
            await permissions.CreateAsync(recovery, CancellationToken.None);
            await permissions.CreateAsync(adjacent, CancellationToken.None);

            tenantContext.SetTenant(tenantA);
            await roles.UpsertSystemRoleAsync("Admin", "Admin", null, tenantA, CancellationToken.None);
            await roles.UpsertSystemRoleAsync("Viewer", "Viewer", null, tenantA, CancellationToken.None);
            await service.GrantModuleWithKeysAsync(
                tenantA,
                ProductIdentityRecoveryOperatorEntitlementGrantProfile.ModuleCode,
                [ProductIdentityRecoveryOperatorEntitlementGrantProfile.PermissionKey],
                "fu26-mongo");

            var recoveryRole = await roles.GetByNameAndTenantAsync(
                ProductIdentityRecoveryOperatorEntitlementGrantProfile.RoleName,
                tenantA,
                CancellationToken.None);
            Assert.NotNull(recoveryRole);
            Assert.True(recoveryRole.IsSystem);
            var firstGrants = await grants.GetByRoleAsync(recoveryRole.Id, tenantA, CancellationToken.None);
            var only = Assert.Single(firstGrants);
            Assert.Equal(recovery.Id, only.PermissionId);
            Assert.Equal(GrantSource.Module, only.GrantSource);
            Assert.Equal(ProductIdentityRecoveryOperatorEntitlementGrantProfile.ModuleCode, only.SourceModuleCode);
            Assert.Equal(
                0,
                await database.GetCollection<UserRole>("userRoles").CountDocumentsAsync(item =>
                    item.TenantId == tenantA));

            await service.GrantModuleWithKeysAsync(
                tenantA,
                ProductIdentityRecoveryOperatorEntitlementGrantProfile.ModuleCode,
                [ProductIdentityRecoveryOperatorEntitlementGrantProfile.PermissionKey],
                "fu26-mongo");
            Assert.Single(await grants.GetByRoleAsync(recoveryRole.Id, tenantA, CancellationToken.None));

            tenantContext.SetTenant(tenantB);
            var tenantBAdmin = await roles.UpsertSystemRoleAsync("Admin", "Admin", null, tenantB, CancellationToken.None);
            await roles.UpsertSystemRoleAsync("Viewer", "Viewer", null, tenantB, CancellationToken.None);
            await grants.AssignAsync(
                RolePermission.ManualGrant(tenantBAdmin.Id, recovery.Id, tenantB, "operator"),
                CancellationToken.None);

            tenantContext.SetTenant(tenantA);
            await service.GrantModuleWithKeysAsync(
                tenantA,
                ProductIdentityRecoveryOperatorEntitlementGrantProfile.ModuleCode,
                [ProductIdentityRecoveryOperatorEntitlementGrantProfile.PermissionKey],
                "fu26-mongo");
            Assert.Single(await grants.GetByRoleAsync(recoveryRole.Id, tenantA, CancellationToken.None));

            tenantContext.SetTenant(tenantB);
            var blocked = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.GrantModuleWithKeysAsync(
                    tenantB,
                    ProductIdentityRecoveryOperatorEntitlementGrantProfile.ModuleCode,
                    [ProductIdentityRecoveryOperatorEntitlementGrantProfile.PermissionKey],
                    "fu26-mongo"));
            Assert.Equal("PRODUCT_IDENTITY_RECOVERY_GRANT_CONTAMINATION", blocked.Message);
            Assert.Null(await roles.GetByNameAndTenantAsync(
                ProductIdentityRecoveryOperatorEntitlementGrantProfile.RoleName,
                tenantB,
                CancellationToken.None));

            tenantContext.SetTenant(tenantA);
            await grants.AssignAsync(
                RolePermission.ModuleGrant(
                    recoveryRole.Id,
                    adjacent.Id,
                    tenantA,
                    "operator",
                    ProductIdentityRecoveryOperatorEntitlementGrantProfile.ModuleCode),
                CancellationToken.None);
            var inbox = new IntegrationEventInboxRepository(database);
            var consumer = new EntitlementSyncConsumer(
                service,
                new UnusedEntitlementClient(),
                inbox,
                NullLogger<EntitlementSyncConsumer>.Instance);
            var eventId = Guid.NewGuid();
            var message = new EventTransportMessage(
                eventId,
                TenantEntitlementDisabledV1.Name,
                1,
                Guid.NewGuid(),
                null,
                tenantA,
                "platform",
                DateTimeOffset.UtcNow,
                JsonSerializer.Serialize(new
                {
                    tenantId = tenantA,
                    moduleCode = ProductIdentityRecoveryOperatorEntitlementGrantProfile.ModuleCode
                }));

            var revokeBlocked = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                consumer.ConsumeAsync(message));
            Assert.Equal("PRODUCT_IDENTITY_RECOVERY_GRANT_CONTAMINATION", revokeBlocked.Message);
            var afterRevoke = await grants.GetByRoleAsync(recoveryRole.Id, tenantA, CancellationToken.None);
            Assert.DoesNotContain(afterRevoke, grant => grant.PermissionId == recovery.Id);
            var foreign = Assert.Single(afterRevoke);
            Assert.Equal(adjacent.Id, foreign.PermissionId);
            Assert.Equal(GrantSource.Module, foreign.GrantSource);
            Assert.Equal(ProductIdentityRecoveryOperatorEntitlementGrantProfile.ModuleCode, foreign.SourceModuleCode);

            Assert.Equal(
                0,
                await database.GetCollection<ProcessedIntegrationEvent>("integrationEventInbox")
                    .CountDocumentsAsync(item => item.EventId == eventId));
            var replayBlocked = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                consumer.ConsumeAsync(message));
            Assert.Equal("PRODUCT_IDENTITY_RECOVERY_GRANT_CONTAMINATION", replayBlocked.Message);
            Assert.Single(await grants.GetByRoleAsync(recoveryRole.Id, tenantA, CancellationToken.None));
            Assert.Equal(
                0,
                await database.GetCollection<ProcessedIntegrationEvent>("integrationEventInbox")
                    .CountDocumentsAsync(item => item.EventId == eventId));
        }
        finally
        {
            await CleanupAsync(database, [tenantA, tenantB], permissionIds);
            await CleanupExactRecoveryAsync(database);
        }
    }

    [Fact]
    public async Task Real_mongo_narrowing_revoke_uses_exact_key_for_deleted_and_divergent_catalog_rows()
    {
        var client = CreateClient();
        await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));
        var database = client.GetDatabase(DatabaseName);

        foreach (var mode in new[] { "deleted", "divergent" })
        {
            var tenantId = Guid.NewGuid();
            var permissionIds = new List<Guid>();
            var tenantContext = new TenantContext();
            var permissions = new PermissionRepository(database);
            var roles = new RoleRepository(database, tenantContext);
            var grants = new RolePermissionRepository(database, tenantContext);
            var service = new EntitlementPermissionSyncService(
                permissions,
                roles,
                grants,
                NullLogger<EntitlementPermissionSyncService>.Instance);

            try
            {
                await CleanupAsync(database, [tenantId], permissionIds);
                await CleanupExactRecoveryAsync(database);
                var recovery = new Permission(
                    "mdm",
                    ProductIdentityRecoveryOperatorEntitlementGrantProfile.Resource,
                    ProductIdentityRecoveryOperatorEntitlementGrantProfile.Action,
                    "Recover",
                    null,
                    moduleOverride: ProductIdentityRecoveryOperatorEntitlementGrantProfile.ModuleCode,
                    scope: PermissionScope.Tenant);
                if (mode == "deleted")
                {
                    recovery.IsDeleted = true;
                }
                else
                {
                    recovery.SetModule("divergent-owner");
                    recovery.SetScope(PermissionScope.PlatformAdmin);
                }

                permissionIds.Add(recovery.Id);
                await permissions.CreateAsync(recovery, CancellationToken.None);
                tenantContext.SetTenant(tenantId);
                var recoveryRole = await roles.UpsertSystemRoleAsync(
                    ProductIdentityRecoveryOperatorEntitlementGrantProfile.RoleName,
                    ProductIdentityRecoveryOperatorEntitlementGrantProfile.DisplayName,
                    ProductIdentityRecoveryOperatorEntitlementGrantProfile.Description,
                    tenantId,
                    CancellationToken.None);
                await grants.AssignAsync(
                    RolePermission.ModuleGrant(
                        recoveryRole.Id,
                        recovery.Id,
                        tenantId,
                        "fu26-mongo",
                        ProductIdentityRecoveryOperatorEntitlementGrantProfile.ModuleCode),
                    CancellationToken.None);

                await service.RevokeModuleAsync(
                    tenantId,
                    ProductIdentityRecoveryOperatorEntitlementGrantProfile.ModuleCode,
                    "fu26-mongo");

                Assert.Empty(await grants.GetByRoleAsync(recoveryRole.Id, tenantId, CancellationToken.None));
            }
            finally
            {
                await CleanupAsync(database, [tenantId], permissionIds);
                await CleanupExactRecoveryAsync(database);
            }
        }
    }

    private static MongoClient CreateClient()
    {
        var settings = MongoClientSettings.FromConnectionString("mongodb://localhost:27017");
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        settings.ConnectTimeout = TimeSpan.FromSeconds(5);
        return new MongoClient(settings);
    }

    private static async Task CleanupAsync(
        IMongoDatabase database,
        IReadOnlyCollection<Guid> tenantIds,
        IReadOnlyCollection<Guid> permissionIds)
    {
        await database.GetCollection<ProcessedIntegrationEvent>("integrationEventInbox").DeleteManyAsync(
            Builders<ProcessedIntegrationEvent>.Filter.In(item => item.TenantId, tenantIds));
        await database.GetCollection<UserRole>("userRoles").DeleteManyAsync(
            Builders<UserRole>.Filter.In(item => item.TenantId, tenantIds));
        await database.GetCollection<RolePermission>("rolePermissions").DeleteManyAsync(
            Builders<RolePermission>.Filter.In(item => item.TenantId, tenantIds));
        await database.GetCollection<Role>("roles").DeleteManyAsync(
            Builders<Role>.Filter.In(item => item.TenantId, tenantIds));
        if (permissionIds.Count > 0)
        {
            await database.GetCollection<Permission>("permissions").DeleteManyAsync(
                Builders<Permission>.Filter.In(item => item.Id, permissionIds));
        }
    }

    private static async Task CleanupExactRecoveryAsync(IMongoDatabase database)
    {
        var permissionCollection = database.GetCollection<Permission>("permissions");
        var matching = await permissionCollection.Find(permission =>
                permission.Key == ProductIdentityRecoveryOperatorEntitlementGrantProfile.PermissionKey)
            .ToListAsync();
        if (matching.Count == 0)
        {
            return;
        }

        var ids = matching.Select(permission => permission.Id).ToArray();
        await database.GetCollection<RolePermission>("rolePermissions").DeleteManyAsync(
            Builders<RolePermission>.Filter.In(item => item.PermissionId, ids));
        await permissionCollection.DeleteManyAsync(Builders<Permission>.Filter.In(item => item.Id, ids));
    }

    private sealed class UnusedEntitlementClient : ITenantEntitlementClient
    {
        public Task<TenantEntitlementReadResult> ReadEntitledModulesWithPermissionKeysAsync(
            Guid tenantId,
            CancellationToken ct)
            => throw new InvalidOperationException("Disabled entitlement delivery must not read the provider.");

        public Task<IReadOnlyList<string>> GetEntitledModuleCodesAsync(Guid tenantId, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<IReadOnlyList<EntitledModulePermissionKeys>> GetEntitledModulesWithPermissionKeysAsync(
            Guid tenantId,
            CancellationToken ct)
            => throw new NotSupportedException();
    }
}
