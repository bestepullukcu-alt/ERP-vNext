using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Authorization;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Configurations;
using Diten.AuthService.Persistence.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Xunit;

namespace Diten.AuthService.Application.Tests.Roles;

public sealed class ProductAbbreviationPermissionOnboardingMongoTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    [Fact]
    public async Task Real_mongo_reconciles_replays_revokes_and_restores_exact_tenant_scoped_profile()
    {
        var settings = MongoClientSettings.FromConnectionString("mongodb://localhost:27017");
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        settings.ConnectTimeout = TimeSpan.FromSeconds(5);
        var client = new MongoClient(settings);
        await client.GetDatabase("admin").RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1));

        var databaseName = "diten_auth_fu20_" + Guid.NewGuid().ToString("N");
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
                new PpmEntitlementPermissionPolicy(),
                NullLogger<EntitlementPermissionSyncService>.Instance);

            var catalog = ProductItemSkuMasterCatalog();
            foreach (var permission in catalog)
            {
                await permissions.CreateAsync(permission, CancellationToken.None);
            }

            await roles.UpsertSystemRoleAsync("Admin", "Admin", null, TenantA, CancellationToken.None);
            await roles.UpsertSystemRoleAsync("Viewer", "Viewer", null, TenantA, CancellationToken.None);
            var permissionKeys = catalog.Select(permission => permission.Key).ToArray();

            await service.GrantModuleWithKeysAsync(
                TenantA,
                ProductAbbreviationEntitlementGrantProfile.ModuleCode,
                permissionKeys,
                "fu20-mongo-test");
            await AssertExactProfileAsync(roles, rolePermissions, catalog);

            var rolesCollection = database.GetCollection<Role>("roles");
            var grantsCollection = database.GetCollection<RolePermission>("rolePermissions");
            var roleCountAfterFirst = await rolesCollection.CountDocumentsAsync(role => role.TenantId == TenantA);
            var grantCountAfterFirst = await grantsCollection.CountDocumentsAsync(grant => grant.TenantId == TenantA);

            await service.GrantModuleWithKeysAsync(
                TenantA,
                ProductAbbreviationEntitlementGrantProfile.ModuleCode,
                permissionKeys,
                "fu20-mongo-test");

            Assert.Equal(roleCountAfterFirst, await rolesCollection.CountDocumentsAsync(role => role.TenantId == TenantA));
            Assert.Equal(grantCountAfterFirst, await grantsCollection.CountDocumentsAsync(grant => grant.TenantId == TenantA));
            Assert.Equal(6, roleCountAfterFirst);
            Assert.Equal(18, grantCountAfterFirst);
            Assert.Equal(0, await database.GetCollection<UserRole>("userRoles").CountDocumentsAsync(FilterDefinition<UserRole>.Empty));

            await service.SyncTenantModulesWithKeysAsync(
                TenantA,
                Array.Empty<EntitledModulePermissionKeys>(),
                "fu20-mongo-test");
            Assert.Equal(
                0,
                await grantsCollection.CountDocumentsAsync(grant =>
                    grant.TenantId == TenantA
                    && grant.GrantSource == GrantSource.Module
                    && grant.SourceModuleCode == ProductAbbreviationEntitlementGrantProfile.ModuleCode));

            await service.GrantModuleWithKeysAsync(
                TenantA,
                ProductAbbreviationEntitlementGrantProfile.ModuleCode,
                permissionKeys,
                "fu20-mongo-test");
            await AssertExactProfileAsync(roles, rolePermissions, catalog);

            tenantContext.SetTenant(TenantB);
            await roles.UpsertSystemRoleAsync("Admin", "Admin", null, TenantB, CancellationToken.None);
            await roles.UpsertSystemRoleAsync("Viewer", "Viewer", null, TenantB, CancellationToken.None);
            await roles.CreateAsync(
                new Role(
                    ProductAbbreviationEntitlementGrantProfile.ApproverRole,
                    "Operator-owned collision",
                    null,
                    TenantB),
                CancellationToken.None);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.GrantModuleWithKeysAsync(
                TenantB,
                ProductAbbreviationEntitlementGrantProfile.ModuleCode,
                permissionKeys,
                "fu20-mongo-test"));

            Assert.Equal(0, await grantsCollection.CountDocumentsAsync(grant => grant.TenantId == TenantB));
            Assert.Equal(grantCountAfterFirst, await grantsCollection.CountDocumentsAsync(grant => grant.TenantId == TenantA));
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
        .. ProductAbbreviationEntitlementGrantProfile.PermissionKeys
            .Select(key => new Permission(
                "mdm",
                "product-abbreviations",
                key[(key.LastIndexOf('.') + 1)..],
                key,
                null,
                moduleOverride: "product-item-sku-master"))
    ];

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
            ["mdm.global-products.create", "mdm.global-products.read", ProductAbbreviationEntitlementGrantProfile.Read]);
        await AssertRoleAsync(
            roles,
            rolePermissions,
            catalog,
            "Viewer",
            ["mdm.global-products.read", ProductAbbreviationEntitlementGrantProfile.Read]);

        foreach (var template in ProductAbbreviationEntitlementGrantProfile.DedicatedRoles)
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
        string[] expectedKeys)
    {
        var role = await roles.GetByNameAndTenantAsync(roleName, TenantA, CancellationToken.None);
        Assert.NotNull(role);
        Assert.True(role.IsSystem);
        var grants = await rolePermissions.GetByRoleAsync(role.Id, TenantA, CancellationToken.None);
        Assert.All(grants, grant =>
        {
            Assert.Equal(GrantSource.Module, grant.GrantSource);
            Assert.Equal(ProductAbbreviationEntitlementGrantProfile.ModuleCode, grant.SourceModuleCode);
        });
        Assert.Equal(
            expectedKeys.OrderBy(key => key, StringComparer.Ordinal),
            grants.Select(grant => catalog.Single(permission => permission.Id == grant.PermissionId).Key)
                .OrderBy(key => key, StringComparer.Ordinal));
    }
}
