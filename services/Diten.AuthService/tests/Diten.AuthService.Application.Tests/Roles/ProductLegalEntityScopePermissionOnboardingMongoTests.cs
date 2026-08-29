using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.AuthService.Application.Tests.Roles;

[CollectionDefinition(AuthPermissionOnboardingMongoCollectionDefinition.Name, DisableParallelization = true)]
public sealed class AuthPermissionOnboardingMongoCollectionDefinition
{
    public const string Name = "AuthPermissionOnboardingMongo";
}

[Collection(AuthPermissionOnboardingMongoCollectionDefinition.Name)]
public sealed class ProductLegalEntityScopePermissionOnboardingMongoTests
{
    private const string DatabaseName = "diten_auth_permission_onboarding_itest";

    [Fact]
    public async Task Real_mongo_reconciles_replays_revokes_and_isolates_exact_composite_profile()
    {
        var settings = MongoClientSettings.FromConnectionString("mongodb://localhost:27017");
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        settings.ConnectTimeout = TimeSpan.FromSeconds(5);
        var client = new MongoClient(settings);
        await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var database = client.GetDatabase(DatabaseName);
        var catalog = ProductItemSkuMasterCatalog();
        try
        {
            var permissionsCollection = database.GetCollection<Permission>("permissions");
            var simulatedCrashResidue = PermissionFor(ProductLegalEntityScopeEntitlementGrantProfile.Read);
            await permissionsCollection.InsertOneAsync(simulatedCrashResidue);
            await CleanupOwnedRowsAsync(database, tenantA, tenantB, catalog);
            Assert.Equal(0, await CountOwnedPermissionsAsync(database, catalog));
            var tenantContext = new TenantContext();
            tenantContext.SetTenant(tenantA);
            var permissions = new PermissionRepository(database);
            var roles = new RoleRepository(database, tenantContext);
            var rolePermissions = new RolePermissionRepository(database, tenantContext);
            var service = new EntitlementPermissionSyncService(
                permissions,
                roles,
                rolePermissions,
                NullLogger<EntitlementPermissionSyncService>.Instance);

            foreach (var permission in catalog)
            {
                await permissions.CreateAsync(permission, CancellationToken.None);
            }

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
        }
        finally
        {
            await CleanupOwnedRowsAsync(database, tenantA, tenantB, catalog);
            Assert.Equal(0, await CountOwnedPermissionsAsync(database, catalog));
            Assert.Equal(0, await database.GetCollection<Role>("roles").CountDocumentsAsync(role =>
                role.TenantId == tenantA || role.TenantId == tenantB));
            Assert.Equal(0, await database.GetCollection<RolePermission>("rolePermissions").CountDocumentsAsync(grant =>
                grant.TenantId == tenantA || grant.TenantId == tenantB));
            Assert.Equal(0, await database.GetCollection<UserRole>("userRoles").CountDocumentsAsync(role =>
                role.TenantId == tenantA || role.TenantId == tenantB));
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
            tenantId,
            "Admin",
            [
                "mdm.global-products.create",
                "mdm.global-products.read",
                ProductAbbreviationEntitlementGrantProfile.Read,
                ProductLegalEntityScopeEntitlementGrantProfile.Read
            ]);
        await AssertRoleAsync(
            roles,
            rolePermissions,
            catalog,
            tenantId,
            "Viewer",
            ["mdm.global-products.read", ProductAbbreviationEntitlementGrantProfile.Read],
            allowAdditionalNonModuleGrants: true);

        foreach (var template in ProductAbbreviationEntitlementGrantProfile.DedicatedRoles)
        {
            await AssertRoleAsync(
                roles,
                rolePermissions,
                catalog,
                tenantId,
                template.RoleName,
                template.PermissionKeys.OrderBy(key => key, StringComparer.Ordinal).ToArray());
        }
        foreach (var template in ProductLegalEntityScopeEntitlementGrantProfile.DedicatedRoles)
        {
            await AssertRoleAsync(
                roles,
                rolePermissions,
                catalog,
                tenantId,
                template.RoleName,
                template.PermissionKeys.OrderBy(key => key, StringComparer.Ordinal).ToArray());
        }
    }

    private static async Task AssertRoleAsync(
        RoleRepository roles,
        RolePermissionRepository rolePermissions,
        IReadOnlyList<Permission> catalog,
        Guid tenantId,
        string roleName,
        string[] expectedKeys,
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

    private static async Task CleanupOwnedRowsAsync(
        IMongoDatabase database,
        Guid tenantA,
        Guid tenantB,
        IReadOnlyCollection<Permission> catalog)
    {
        var tenantFilter = Builders<Role>.Filter.In(role => role.TenantId, [tenantA, tenantB]);
        await database.GetCollection<UserRole>("userRoles").DeleteManyAsync(
            Builders<UserRole>.Filter.In(role => role.TenantId, [tenantA, tenantB]));
        await database.GetCollection<RolePermission>("rolePermissions").DeleteManyAsync(
            Builders<RolePermission>.Filter.In(grant => grant.TenantId, [tenantA, tenantB]));
        await database.GetCollection<Role>("roles").DeleteManyAsync(tenantFilter);
        await database.GetCollection<Permission>("permissions").DeleteManyAsync(
            Builders<Permission>.Filter.In(
                permission => permission.Key,
                catalog.Select(permission => permission.Key.ToLowerInvariant()).Distinct(StringComparer.Ordinal)));
    }

    private static Task<long> CountOwnedPermissionsAsync(
        IMongoDatabase database,
        IReadOnlyCollection<Permission> catalog)
        => database.GetCollection<Permission>("permissions").CountDocumentsAsync(
            Builders<Permission>.Filter.In(
                permission => permission.Key,
                catalog.Select(permission => permission.Key.ToLowerInvariant()).Distinct(StringComparer.Ordinal)));
}
