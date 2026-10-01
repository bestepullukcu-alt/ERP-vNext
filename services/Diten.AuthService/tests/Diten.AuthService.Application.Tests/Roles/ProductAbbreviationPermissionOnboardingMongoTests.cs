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

[Collection(AuthPermissionOnboardingMongoCollectionDefinition.Name)]
public sealed class ProductAbbreviationPermissionOnboardingMongoTests
{
    private const string DatabaseName = "diten_auth_fu20_itest";
    private readonly Guid TenantA = Guid.NewGuid();
    private readonly Guid TenantB = Guid.NewGuid();

    [Fact]
    public async Task Real_mongo_reconciles_replays_revokes_and_restores_exact_tenant_scoped_profile()
    {
        var database = OpenOwnedTestDatabase();
        var catalog = ProductItemSkuMasterCatalog();
        try
        {
            await MongoDbIndexConfigurations.EnsurePermissionOnboardingIndexesAsync(database);
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
            Assert.Equal(0, await database.GetCollection<UserRole>("userRoles").CountDocumentsAsync(
                role => role.TenantId == TenantA || role.TenantId == TenantB));

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

            // Exercise source preservation after the original exact 6-role / 18-grant assertions.
            tenantContext.SetTenant(TenantA);
            var viewer = await roles.GetByNameAndTenantAsync("Viewer", TenantA, CancellationToken.None);
            Assert.NotNull(viewer);
            var manualPermission = new Permission("fu20", "manual", "read", "Test manual", null);
            var otherPermission = new Permission("fu20", "other", "read", "Test other source", null);
            catalog.AddRange([manualPermission, otherPermission]);
            await permissions.CreateAsync(manualPermission, CancellationToken.None);
            await permissions.CreateAsync(otherPermission, CancellationToken.None);
            var manual = RolePermission.ManualGrant(viewer.Id, manualPermission.Id, TenantA, "fu20-test");
            var other = RolePermission.ModuleGrant(viewer.Id, otherPermission.Id, TenantA, "fu20-test", "other-module");
            await rolePermissions.AssignAsync(manual, CancellationToken.None);
            await rolePermissions.AssignAsync(other, CancellationToken.None);
            await service.RevokeModuleAsync(TenantA, ProductAbbreviationEntitlementGrantProfile.ModuleCode, "fu20-test");
            var retained = await grantsCollection.Find(grant => grant.TenantId == TenantA).ToListAsync();
            Assert.Equal(new[] { manual.Id, other.Id }.OrderBy(id => id), retained.Select(grant => grant.Id).OrderBy(id => id));
            Assert.Equal(GrantSource.Manual, retained.Single(grant => grant.Id == manual.Id).GrantSource);
            Assert.Equal("other-module", retained.Single(grant => grant.Id == other.Id).SourceModuleCode);
            await service.GrantModuleWithKeysAsync(TenantA, ProductAbbreviationEntitlementGrantProfile.ModuleCode, permissionKeys, "fu20-test");
            await service.GrantModuleWithKeysAsync(TenantA, ProductAbbreviationEntitlementGrantProfile.ModuleCode, permissionKeys, "fu20-test");
            Assert.Equal(20, await grantsCollection.CountDocumentsAsync(grant => grant.TenantId == TenantA));
            Assert.Equal(2, await grantsCollection.CountDocumentsAsync(grant => grant.Id == manual.Id || grant.Id == other.Id));
            Assert.Equal(0, await grantsCollection.CountDocumentsAsync(grant => grant.TenantId == TenantB));
            Assert.Equal(0, await database.GetCollection<UserRole>("userRoles").CountDocumentsAsync(
                role => role.TenantId == TenantA || role.TenantId == TenantB));
        }
        finally
        {
            await CleanupAsync(database, catalog);
        }
    }

    [Fact]
    public async Task Real_mongo_preserves_production_unique_indexes_and_idempotent_schema()
    {
        var database = OpenOwnedTestDatabase();
        var permission = new Permission("fu20-index", "resource", "read", "Index test", null);
        try
        {
            await MongoDbIndexConfigurations.EnsurePermissionOnboardingIndexesAsync(database);
            var before = await ReadIndexesAsync(database);
            await MongoDbIndexConfigurations.EnsurePermissionOnboardingIndexesAsync(database);
            Assert.Equal(before, await ReadIndexesAsync(database));
            var collections = (await database.ListCollectionNamesAsync()).ToList();
            Assert.Equal(new[] { "permissions", "rolePermissions", "roles", "userRoles" }, collections.OrderBy(name => name));

            var permissions = database.GetCollection<BsonDocument>("permissions");
            var original = permission.ToBsonDocument();
            await permissions.InsertOneAsync(original);
            var duplicateKey = original.DeepClone().AsBsonDocument;
            duplicateKey["_id"] = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard);
            duplicateKey["Action"] = "different";
            await AssertDuplicateAsync(() => permissions.InsertOneAsync(duplicateKey));
            var duplicateTuple = original.DeepClone().AsBsonDocument;
            duplicateTuple["_id"] = new BsonBinaryData(Guid.NewGuid(), GuidRepresentation.Standard);
            duplicateTuple["Key"] = "fu20-index.different.key";
            await AssertDuplicateAsync(() => permissions.InsertOneAsync(duplicateTuple));

            var userRoles = database.GetCollection<UserRole>("userRoles");
            var userId = Guid.NewGuid();
            var roleId = Guid.NewGuid();
            await userRoles.InsertOneAsync(new UserRole(userId, roleId, TenantA, "fu20-test"));
            await AssertDuplicateAsync(() => userRoles.InsertOneAsync(new UserRole(userId, roleId, TenantA, "fu20-test")));
            await userRoles.InsertOneAsync(new UserRole(userId, roleId, TenantB, "fu20-test"));
            Assert.Equal(1, await userRoles.CountDocumentsAsync(role => role.TenantId == TenantA));
            Assert.Equal(1, await userRoles.CountDocumentsAsync(role => role.TenantId == TenantB));

            var roles = database.GetCollection<Role>("roles");
            await roles.InsertOneAsync(new Role("FU20-index-role", "Test", null, TenantA));
            await AssertDuplicateAsync(() => roles.InsertOneAsync(new Role("FU20-index-role", "Test", null, TenantA)));
            await roles.InsertOneAsync(new Role("FU20-index-role", "Test", null, TenantB));
            var grants = database.GetCollection<RolePermission>("rolePermissions");
            await grants.InsertOneAsync(RolePermission.ManualGrant(roleId, permission.Id, TenantA, "fu20-test"));
            await AssertDuplicateAsync(() => grants.InsertOneAsync(RolePermission.ManualGrant(roleId, permission.Id, TenantA, "fu20-test")));
            await grants.InsertOneAsync(RolePermission.ManualGrant(roleId, permission.Id, TenantB, "fu20-test"));
        }
        finally
        {
            await CleanupAsync(database, [permission]);
        }
    }

    private static IMongoDatabase OpenOwnedTestDatabase()
    {
        var uri = Environment.GetEnvironmentVariable("MONGO_TEST_URI")
            ?? throw new InvalidOperationException("An explicit owned-test Mongo URI is required.");
        var url = new MongoUrl(uri);
        if (url.Servers.Count() != 1 || url.Server.Host != "127.0.0.1" || url.Server.Port == 27017
            || url.Username is not null || url.Password is not null)
            throw new InvalidOperationException("FU20 requires an isolated loopback test Mongo port, never application Mongo.");
        var settings = MongoClientSettings.FromUrl(url);
        settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
        settings.ConnectTimeout = TimeSpan.FromSeconds(5);
        return new MongoClient(settings).GetDatabase(DatabaseName);
    }

    private async Task CleanupAsync(IMongoDatabase database, IReadOnlyList<Permission> catalog)
    {
        await database.GetCollection<UserRole>("userRoles").DeleteManyAsync(role => role.TenantId == TenantA || role.TenantId == TenantB);
        await database.GetCollection<RolePermission>("rolePermissions").DeleteManyAsync(grant => grant.TenantId == TenantA || grant.TenantId == TenantB);
        await database.GetCollection<Role>("roles").DeleteManyAsync(role => role.TenantId == TenantA || role.TenantId == TenantB);
        var ids = catalog.Select(permission => permission.Id).ToArray();
        await database.GetCollection<Permission>("permissions").DeleteManyAsync(permission => ids.Contains(permission.Id));
    }

    private static async Task AssertDuplicateAsync(Func<Task> insert)
    {
        var error = await Assert.ThrowsAsync<MongoWriteException>(insert);
        Assert.Equal(ServerErrorCategory.DuplicateKey, error.WriteError.Category);
    }

    private static async Task<string[]> ReadIndexesAsync(IMongoDatabase database)
    {
        var result = new List<string>();
        foreach (var name in new[] { "permissions", "rolePermissions", "roles", "userRoles" })
        {
            var indexes = await (await database.GetCollection<BsonDocument>(name).Indexes.ListAsync()).ToListAsync();
            result.AddRange(indexes.Select(index => name + ":" + index.ToJson()));
        }
        return result.OrderBy(index => index, StringComparer.Ordinal).ToArray();
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

    private async Task AssertExactProfileAsync(
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

    private async Task AssertRoleAsync(
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
