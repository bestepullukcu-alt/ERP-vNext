using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Configurations;
using Diten.AuthService.Persistence.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.AuthService.Application.Tests.Roles;

public sealed class ProductLegalEntityScopePermissionOnboardingMongoTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task Real_mongo_reconciles_replays_revokes_and_isolates_exact_composite_profile()
    {
        var settings = MongoClientSettings.FromConnectionString("mongodb://localhost:27017");
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        settings.ConnectTimeout = TimeSpan.FromSeconds(5);
        var client = new MongoClient(settings);
        await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));

        var databaseName = "diten_auth_fu22_" + Guid.NewGuid().ToString("N");
        var database = client.GetDatabase(databaseName);
        try
        {
            await MongoDbIndexConfigurations.EnsureIndexesAsync(database);
            var tenantContext = new TenantContext();
            tenantContext.SetTenant(TenantA);
            var permissions = new PermissionRepository(database);
            var roles = new RoleRepository(database, tenantContext);
            var rolePermissions = new RolePermissionRepository(database, tenantContext);
            var service = new EntitlementPermissionSyncService(
                permissions,
                roles,
                rolePermissions,
                NullLogger<EntitlementPermissionSyncService>.Instance);

            var catalog = ProductItemSkuMasterCatalog();
            foreach (var permission in catalog)
            {
                await permissions.CreateAsync(permission, CancellationToken.None);
            }

            var admin = await roles.UpsertSystemRoleAsync("Admin", "Admin", null, TenantA, CancellationToken.None);
            var viewer = await roles.UpsertSystemRoleAsync("Viewer", "Viewer", null, TenantA, CancellationToken.None);
            var manualPermission = catalog.Single(permission => permission.Key == "manual.retained.read");
            var otherModulePermission = catalog.Single(permission => permission.Key == "other.retained.read");
            await rolePermissions.AssignAsync(
                RolePermission.ManualGrant(viewer.Id, manualPermission.Id, TenantA, "operator"),
                CancellationToken.None);
            await rolePermissions.AssignAsync(
                RolePermission.ModuleGrant(admin.Id, otherModulePermission.Id, TenantA, "other", "another-module"),
                CancellationToken.None);

            var permissionKeys = catalog
                .Where(permission => permission.Module == ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode)
                .Select(permission => permission.Key)
                .ToArray();
            await service.GrantModuleWithKeysAsync(
                TenantA,
                ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode,
                permissionKeys,
                "fu22-mongo-test");
            await AssertExactProfileAsync(roles, rolePermissions, catalog);

            var rolesCollection = database.GetCollection<Role>("roles");
            var grantsCollection = database.GetCollection<RolePermission>("rolePermissions");
            var roleCountAfterFirst = await rolesCollection.CountDocumentsAsync(role => role.TenantId == TenantA);
            var grantCountAfterFirst = await grantsCollection.CountDocumentsAsync(grant => grant.TenantId == TenantA);

            await service.GrantModuleWithKeysAsync(
                TenantA,
                ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode,
                permissionKeys,
                "fu22-mongo-test");

            Assert.Equal(9, roleCountAfterFirst);
            Assert.Equal(29, grantCountAfterFirst);
            Assert.Equal(27, await grantsCollection.CountDocumentsAsync(grant =>
                grant.TenantId == TenantA
                && grant.GrantSource == GrantSource.Module
                && grant.SourceModuleCode == ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode));
            Assert.Equal(roleCountAfterFirst, await rolesCollection.CountDocumentsAsync(role => role.TenantId == TenantA));
            Assert.Equal(grantCountAfterFirst, await grantsCollection.CountDocumentsAsync(grant => grant.TenantId == TenantA));
            Assert.Equal(0, await database.GetCollection<UserRole>("userRoles").CountDocumentsAsync(FilterDefinition<UserRole>.Empty));

            await service.RevokeModuleAsync(
                TenantA,
                ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode,
                "fu22-mongo-test");
            Assert.Equal(0, await grantsCollection.CountDocumentsAsync(grant =>
                grant.TenantId == TenantA
                && grant.GrantSource == GrantSource.Module
                && grant.SourceModuleCode == ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode));
            Assert.Equal(1, await grantsCollection.CountDocumentsAsync(grant =>
                grant.TenantId == TenantA && grant.GrantSource == GrantSource.Manual));
            Assert.Equal(1, await grantsCollection.CountDocumentsAsync(grant =>
                grant.TenantId == TenantA && grant.SourceModuleCode == "another-module"));

            await service.GrantModuleWithKeysAsync(
                TenantA,
                ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode,
                permissionKeys,
                "fu22-mongo-test");
            await AssertExactProfileAsync(roles, rolePermissions, catalog);

            var tenantAGrantCount = await grantsCollection.CountDocumentsAsync(grant => grant.TenantId == TenantA);
            tenantContext.SetTenant(TenantB);
            await roles.UpsertSystemRoleAsync("Admin", "Admin", null, TenantB, CancellationToken.None);
            await roles.UpsertSystemRoleAsync("Viewer", "Viewer", null, TenantB, CancellationToken.None);
            await roles.CreateAsync(
                new Role(
                    ProductLegalEntityScopeEntitlementGrantProfile.AuditorRole,
                    "Operator-owned collision",
                    null,
                    TenantB),
                CancellationToken.None);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.GrantModuleWithKeysAsync(
                TenantB,
                ProductLegalEntityScopeEntitlementGrantProfile.ModuleCode,
                permissionKeys,
                "fu22-mongo-test"));

            Assert.Equal(0, await grantsCollection.CountDocumentsAsync(grant => grant.TenantId == TenantB));
            Assert.Equal(tenantAGrantCount, await grantsCollection.CountDocumentsAsync(grant => grant.TenantId == TenantA));
            Assert.Equal(0, await database.GetCollection<UserRole>("userRoles").CountDocumentsAsync(FilterDefinition<UserRole>.Empty));
        }
        finally
        {
            await client.DropDatabaseAsync(databaseName);
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
        IReadOnlyList<Permission> catalog)
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
            ]);
        await AssertRoleAsync(
            roles,
            rolePermissions,
            catalog,
            "Viewer",
            ["mdm.global-products.read", ProductAbbreviationEntitlementGrantProfile.Read],
            allowAdditionalNonModuleGrants: true);

        foreach (var template in ProductAbbreviationEntitlementGrantProfile.DedicatedRoles)
        {
            await AssertRoleAsync(
                roles,
                rolePermissions,
                catalog,
                template.RoleName,
                template.PermissionKeys.OrderBy(key => key, StringComparer.Ordinal).ToArray());
        }
        foreach (var template in ProductLegalEntityScopeEntitlementGrantProfile.DedicatedRoles)
        {
            await AssertRoleAsync(
                roles,
                rolePermissions,
                catalog,
                template.RoleName,
                template.PermissionKeys.OrderBy(key => key, StringComparer.Ordinal).ToArray());
        }
    }

    private static async Task AssertRoleAsync(
        RoleRepository roles,
        RolePermissionRepository rolePermissions,
        IReadOnlyList<Permission> catalog,
        string roleName,
        string[] expectedKeys,
        bool allowAdditionalNonModuleGrants = false)
    {
        var role = await roles.GetByNameAndTenantAsync(roleName, TenantA, CancellationToken.None);
        Assert.NotNull(role);
        Assert.True(role.IsSystem);
        var grants = await rolePermissions.GetByRoleAsync(role.Id, TenantA, CancellationToken.None);
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
}
