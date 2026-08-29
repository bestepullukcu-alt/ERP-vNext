using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.AuthService.Application.Tests.Roles;

public sealed class ProductLegalEntityScopePermissionOnboardingMongoTests
{
    private const string DatabaseName = "diten_auth_fu22_itest";

    [Fact]
    public async Task Real_mongo_reconciles_replays_revokes_and_isolates_exact_composite_profile()
    {
        var settings = MongoClientSettings.FromConnectionString("mongodb://localhost:27017");
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        settings.ConnectTimeout = TimeSpan.FromSeconds(5);
        var client = new MongoClient(settings);
        await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));

        var database = client.GetDatabase(DatabaseName);
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var catalog = ProductItemSkuMasterCatalog();
        try
        {
            await EnsureRequiredIndexesAsync(database);
            var tenantContext = new TenantContext();
            tenantContext.SetTenant(tenantA);
            var permissions = new PermissionRepository(database);
            catalog = await EnsureCatalogAsync(permissions, catalog);
            var roles = new RoleRepository(database, tenantContext);
            var rolePermissions = new RolePermissionRepository(database, tenantContext);
            var service = new EntitlementPermissionSyncService(
                permissions,
                roles,
                rolePermissions,
                NullLogger<EntitlementPermissionSyncService>.Instance);

            var admin = await roles.UpsertSystemRoleAsync("Admin", "Admin", null, tenantA, CancellationToken.None);
            var viewer = await roles.UpsertSystemRoleAsync("Viewer", "Viewer", null, tenantA, CancellationToken.None);
            var manualPermission = catalog.Single(permission => permission.Key == "manual.retained.read");
            var otherModulePermission = catalog.Single(permission => permission.Key == "other.retained.read");
            await rolePermissions.AssignAsync(
                RolePermission.ManualGrant(viewer.Id, manualPermission.Id, tenantA, "operator"),
                CancellationToken.None);
            await rolePermissions.AssignAsync(
                RolePermission.ModuleGrant(admin.Id, otherModulePermission.Id, tenantA, "other", "another-module"),
                CancellationToken.None);

            var permissionKeys = catalog
                .Where(permission => permission.Module == ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode)
                .Select(permission => permission.Key)
                .ToArray();
            await service.GrantModuleWithKeysAsync(
                tenantA,
                ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode,
                permissionKeys,
                "fu22-mongo-test");
            await AssertExactProfileAsync(roles, rolePermissions, catalog, tenantA);

            var rolesCollection = database.GetCollection<Role>("roles");
            var grantsCollection = database.GetCollection<RolePermission>("rolePermissions");
            var roleCountAfterFirst = await rolesCollection.CountDocumentsAsync(role => role.TenantId == tenantA);
            var grantCountAfterFirst = await grantsCollection.CountDocumentsAsync(grant => grant.TenantId == tenantA);

            await service.GrantModuleWithKeysAsync(
                tenantA,
                ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode,
                permissionKeys,
                "fu22-mongo-test");

            Assert.Equal(9, roleCountAfterFirst);
            Assert.Equal(29, grantCountAfterFirst);
            Assert.Equal(27, await grantsCollection.CountDocumentsAsync(grant =>
                grant.TenantId == tenantA
                && grant.GrantSource == GrantSource.Module
                && grant.SourceModuleCode == ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode));
            Assert.Equal(roleCountAfterFirst, await rolesCollection.CountDocumentsAsync(role => role.TenantId == tenantA));
            Assert.Equal(grantCountAfterFirst, await grantsCollection.CountDocumentsAsync(grant => grant.TenantId == tenantA));
            Assert.Equal(0, await database.GetCollection<UserRole>("userRoles").CountDocumentsAsync(role => role.TenantId == tenantA));

            await service.RevokeModuleAsync(
                tenantA,
                ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode,
                "fu22-mongo-test");
            Assert.Equal(0, await grantsCollection.CountDocumentsAsync(grant =>
                grant.TenantId == tenantA
                && grant.GrantSource == GrantSource.Module
                && grant.SourceModuleCode == ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode));
            Assert.Equal(1, await grantsCollection.CountDocumentsAsync(grant =>
                grant.TenantId == tenantA && grant.GrantSource == GrantSource.Manual));
            Assert.Equal(1, await grantsCollection.CountDocumentsAsync(grant =>
                grant.TenantId == tenantA && grant.SourceModuleCode == "another-module"));

            await service.GrantModuleWithKeysAsync(
                tenantA,
                ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode,
                permissionKeys,
                "fu22-mongo-test");
            await AssertExactProfileAsync(roles, rolePermissions, catalog, tenantA);

            var tenantAGrantCount = await grantsCollection.CountDocumentsAsync(grant => grant.TenantId == tenantA);
            tenantContext.SetTenant(tenantB);
            await roles.UpsertSystemRoleAsync("Admin", "Admin", null, tenantB, CancellationToken.None);
            await roles.UpsertSystemRoleAsync("Viewer", "Viewer", null, tenantB, CancellationToken.None);
            await roles.CreateAsync(
                new Role(
                    ProductLegalEntityScopeEntitlementGrantProfile.AuditorRole,
                    "Operator-owned collision",
                    null,
                    tenantB),
                CancellationToken.None);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.GrantModuleWithKeysAsync(
                tenantB,
                ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode,
                permissionKeys,
                "fu22-mongo-test"));

            Assert.Equal(0, await grantsCollection.CountDocumentsAsync(grant => grant.TenantId == tenantB));
            Assert.Equal(tenantAGrantCount, await grantsCollection.CountDocumentsAsync(grant => grant.TenantId == tenantA));
            Assert.Equal(0, await database.GetCollection<UserRole>("userRoles").CountDocumentsAsync(role =>
                role.TenantId == tenantA || role.TenantId == tenantB));

            var inbox = new IntegrationEventInboxRepository(database);
            var eventId = Guid.NewGuid();
            var claimTasks = new[]
            {
                inbox.TryClaimAsync(eventId, "tenant.entitlement.added.v1", tenantA, TimeSpan.FromSeconds(30)),
                inbox.TryClaimAsync(eventId, "tenant.entitlement.added.v1", tenantB, TimeSpan.FromSeconds(30))
            };
            var claimResults = await Task.WhenAll(claimTasks);
            Assert.Equal(1, claimResults.Count(claim => claim.Result == IntegrationEventClaimResult.Claimed));
            Assert.Equal(1, claimResults.Count(claim => claim.Result == IntegrationEventClaimResult.TenantMismatch));

            var winningClaim = claimResults.Single(claim => claim.Result == IntegrationEventClaimResult.Claimed);
            var winningTenant = claimResults[0].Result == IntegrationEventClaimResult.Claimed ? tenantA : tenantB;
            var losingTenant = winningTenant == tenantA ? tenantB : tenantA;
            await inbox.CompleteClaimAsync(eventId, winningTenant, winningClaim.ClaimId!.Value);
            Assert.Equal(
                IntegrationEventClaimResult.Completed,
                (await inbox.TryClaimAsync(
                    eventId,
                    "tenant.entitlement.added.v1",
                    winningTenant,
                    TimeSpan.FromSeconds(30))).Result);
            Assert.Equal(
                IntegrationEventClaimResult.TenantMismatch,
                (await inbox.TryClaimAsync(
                    eventId,
                    "tenant.entitlement.added.v1",
                    losingTenant,
                    TimeSpan.FromSeconds(30))).Result);
            Assert.Equal(
                IntegrationEventClaimResult.IdentityMismatch,
                (await inbox.TryClaimAsync(
                    eventId,
                    "tenant.entitlement.disabled.v1",
                    winningTenant,
                    TimeSpan.FromSeconds(30))).Result);

            var recoveryEventId = Guid.NewGuid();
            var initialRecoveryClaim = await inbox.TryClaimAsync(
                    recoveryEventId,
                    "tenant.entitlement.enabled.v1",
                    tenantA,
                    TimeSpan.FromMilliseconds(20));
            Assert.Equal(IntegrationEventClaimResult.Claimed, initialRecoveryClaim.Result);
            await Task.Delay(TimeSpan.FromMilliseconds(50));
            var reclaimed = await inbox.TryClaimAsync(
                    recoveryEventId,
                    "tenant.entitlement.enabled.v1",
                    tenantA,
                    TimeSpan.FromSeconds(30));
            Assert.Equal(IntegrationEventClaimResult.Claimed, reclaimed.Result);

            await Assert.ThrowsAsync<InvalidOperationException>(() => inbox.CompleteClaimAsync(
                recoveryEventId,
                tenantA,
                initialRecoveryClaim.ClaimId!.Value));
            await inbox.ReleaseClaimAsync(recoveryEventId, tenantA, initialRecoveryClaim.ClaimId!.Value);
            Assert.Equal(
                IntegrationEventClaimResult.Busy,
                (await inbox.TryClaimAsync(
                    recoveryEventId,
                    "tenant.entitlement.enabled.v1",
                    tenantA,
                    TimeSpan.FromSeconds(30))).Result);

            await inbox.ReleaseClaimAsync(recoveryEventId, tenantA, reclaimed.ClaimId!.Value);
            var finalClaim = await inbox.TryClaimAsync(
                recoveryEventId,
                "tenant.entitlement.enabled.v1",
                tenantA,
                TimeSpan.FromSeconds(30));
            Assert.Equal(IntegrationEventClaimResult.Claimed, finalClaim.Result);
            await inbox.CompleteClaimAsync(recoveryEventId, tenantA, finalClaim.ClaimId!.Value);

            var legacyEventId = Guid.NewGuid();
            var legacyDocument = new ProcessedIntegrationEvent(
                legacyEventId,
                "tenant.entitlement.added.v1",
                tenantA).ToBsonDocument();
            legacyDocument.Remove(nameof(ProcessedIntegrationEvent.State));
            legacyDocument.Remove(nameof(ProcessedIntegrationEvent.ClaimId));
            legacyDocument.Remove(nameof(ProcessedIntegrationEvent.LeaseExpiresAtUtc));
            await database.GetCollection<BsonDocument>("integrationEventInbox").InsertOneAsync(legacyDocument);
            Assert.Equal(
                IntegrationEventClaimResult.Completed,
                (await inbox.TryClaimAsync(
                    legacyEventId,
                    "tenant.entitlement.added.v1",
                    tenantA,
                    TimeSpan.FromSeconds(30))).Result);
        }
        finally
        {
            await CleanupAsync(database, tenantA, tenantB);
        }
    }

    private static List<Permission> ProductItemSkuMasterCatalog() =>
    [
        new("mdm", "global-products", "read", "Read Global Products", null, moduleOverride: "product-item-sku-master"),
        new("mdm", "global-products", "create", "Create Global Products", null, moduleOverride: "product-item-sku-master"),
        .. ProductAbbreviationEntitlementGrantProfile.PermissionKeys.Select(PermissionFor),
        .. ProductLegalEntityScopeEntitlementGrantProfile.PermissionKeys.Select(PermissionFor),
        new("manual", "retained", "read", "Manual retained", null, moduleOverride: "manual-module"),
        new("other", "retained", "read", "Other module retained", null, moduleOverride: "another-module")
    ];

    private static Permission PermissionFor(string key)
    {
        var separator = key.LastIndexOf('.');
        return new Permission(
            "mdm",
            key["mdm.".Length..separator],
            key[(separator + 1)..],
            key,
            null,
            moduleOverride: ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode);
    }

    private static async Task AssertExactProfileAsync(
        RoleRepository roles,
        RolePermissionRepository rolePermissions,
        IReadOnlyList<Permission> catalog,
        Guid tenantId)
    {
        await AssertRoleAsync(
            roles,
            rolePermissions,
            catalog,
            "Admin",
            [
                "mdm.global-products.create",
                "mdm.global-products.read",
                ProductAbbreviationEntitlementGrantProfile.Read,
                ProductLegalEntityScopeEntitlementGrantProfile.Read
            ],
            tenantId);
        await AssertRoleAsync(
            roles,
            rolePermissions,
            catalog,
            "Viewer",
            ["mdm.global-products.read", ProductAbbreviationEntitlementGrantProfile.Read],
            tenantId,
            allowAdditionalNonModuleGrants: true);

        foreach (var template in ProductAbbreviationEntitlementGrantProfile.DedicatedRoles)
        {
            await AssertRoleAsync(
                roles,
                rolePermissions,
                catalog,
                template.RoleName,
                template.PermissionKeys.OrderBy(key => key, StringComparer.Ordinal).ToArray(),
                tenantId);
        }
        foreach (var template in ProductLegalEntityScopeEntitlementGrantProfile.DedicatedRoles)
        {
            await AssertRoleAsync(
                roles,
                rolePermissions,
                catalog,
                template.RoleName,
                template.PermissionKeys.OrderBy(key => key, StringComparer.Ordinal).ToArray(),
                tenantId);
        }
    }

    private static async Task AssertRoleAsync(
        RoleRepository roles,
        RolePermissionRepository rolePermissions,
        IReadOnlyList<Permission> catalog,
        string roleName,
        string[] expectedKeys,
        Guid tenantId,
        bool allowAdditionalNonModuleGrants = false)
    {
        var role = await roles.GetByNameAndTenantAsync(roleName, tenantId, CancellationToken.None);
        Assert.NotNull(role);
        Assert.True(role.IsSystem);
        var grants = await rolePermissions.GetByRoleAsync(role.Id, tenantId, CancellationToken.None);
        var moduleGrants = grants.Where(grant =>
            grant.GrantSource == GrantSource.Module
            && grant.SourceModuleCode == ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode).ToList();
        if (!allowAdditionalNonModuleGrants)
        {
            Assert.All(grants.Where(grant => !moduleGrants.Contains(grant)), grant =>
                Assert.Equal("another-module", grant.SourceModuleCode));
        }
        Assert.Equal(
            expectedKeys.OrderBy(key => key, StringComparer.Ordinal),
            moduleGrants.Select(grant => catalog.Single(permission => permission.Id == grant.PermissionId).Key)
                .OrderBy(key => key, StringComparer.Ordinal));
    }

    private static async Task EnsureRequiredIndexesAsync(IMongoDatabase database)
    {
        await database.GetCollection<Role>("roles").Indexes.CreateOneAsync(
            new CreateIndexModel<Role>(
                Builders<Role>.IndexKeys.Ascending(role => role.TenantId).Ascending(role => role.Name),
                new CreateIndexOptions<Role>
                {
                    Unique = true,
                    Name = "uq_roles_tenant_name_active",
                    PartialFilterExpression = Builders<Role>.Filter.Eq(role => role.IsDeleted, false)
                }));
        await database.GetCollection<Permission>("permissions").Indexes.CreateOneAsync(
            new CreateIndexModel<Permission>(
                Builders<Permission>.IndexKeys.Ascending(permission => permission.Key),
                new CreateIndexOptions { Unique = true, Name = "uq_permissions_key" }));
        await database.GetCollection<RolePermission>("rolePermissions").Indexes.CreateOneAsync(
            new CreateIndexModel<RolePermission>(
                Builders<RolePermission>.IndexKeys
                    .Ascending(grant => grant.RoleId)
                    .Ascending(grant => grant.PermissionId)
                    .Ascending(grant => grant.TenantId),
                new CreateIndexOptions { Unique = true, Name = "uq_role_permissions_role_permission_tenant" }));
        await database.GetCollection<ProcessedIntegrationEvent>("integrationEventInbox").Indexes.CreateOneAsync(
            new CreateIndexModel<ProcessedIntegrationEvent>(
                Builders<ProcessedIntegrationEvent>.IndexKeys.Ascending(item => item.EventId),
                new CreateIndexOptions { Unique = true, Name = "uq_integration_event_inbox_event_id" }));
    }

    private static async Task<List<Permission>> EnsureCatalogAsync(
        PermissionRepository repository,
        IEnumerable<Permission> desiredCatalog)
    {
        var persisted = new List<Permission>();
        foreach (var desired in desiredCatalog)
        {
            var existing = await repository.GetByKeyIncludingDeletedAsync(desired.Key, CancellationToken.None);
            if (existing is null)
            {
                try
                {
                    await repository.CreateAsync(desired, CancellationToken.None);
                    persisted.Add(desired);
                }
                catch (MongoWriteException exception)
                    when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
                {
                    var concurrentlyCreated = await repository.GetByKeyIncludingDeletedAsync(
                        desired.Key,
                        CancellationToken.None);
                    Assert.NotNull(concurrentlyCreated);
                    persisted.Add(concurrentlyCreated);
                }
                continue;
            }

            Assert.False(existing.IsDeleted);
            Assert.Equal(desired.Module, existing.Module);
            Assert.Equal(desired.Resource, existing.Resource);
            Assert.Equal(desired.Action, existing.Action);
            persisted.Add(existing);
        }

        return persisted;
    }

    private static async Task CleanupAsync(
        IMongoDatabase database,
        Guid tenantA,
        Guid tenantB)
    {
        var tenantFilter = Builders<Role>.Filter.In(role => role.TenantId, [tenantA, tenantB]);
        await database.GetCollection<Role>("roles").DeleteManyAsync(tenantFilter);
        await database.GetCollection<RolePermission>("rolePermissions").DeleteManyAsync(
            Builders<RolePermission>.Filter.In(grant => grant.TenantId, [tenantA, tenantB]));
        await database.GetCollection<UserRole>("userRoles").DeleteManyAsync(
            Builders<UserRole>.Filter.In(role => role.TenantId, [tenantA, tenantB]));
        await database.GetCollection<ProcessedIntegrationEvent>("integrationEventInbox").DeleteManyAsync(
            Builders<ProcessedIntegrationEvent>.Filter.In(item => item.TenantId, [tenantA, tenantB]));
    }
}
