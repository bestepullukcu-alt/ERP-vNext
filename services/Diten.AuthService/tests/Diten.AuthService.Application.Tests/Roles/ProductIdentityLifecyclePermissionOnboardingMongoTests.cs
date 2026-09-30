using Diten.AuthService.Application.Common;
using Diten.AuthService.Application.Common.Authorization;
using Diten.AuthService.Application.Common.Interfaces;
using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Domain.Entities;
using Diten.AuthService.Persistence.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using MongoDB.Bson;
using MongoDB.Driver;
using Diten.AuthService.Application.Tests.Persistence;

namespace Diten.AuthService.Application.Tests.Roles;

[CollectionDefinition(AuthPermissionOnboardingMongoCollectionDefinition.Name, DisableParallelization = true)]
public sealed class AuthPermissionOnboardingMongoCollectionDefinition
{
    public const string Name = "Auth permission onboarding Mongo";
}

[Collection(AuthReconciliationMongoCollection.Name)]
public sealed class ProductIdentityLifecyclePermissionOnboardingMongoTests(DisposableAuthMongoReplicaSet ownedMongo)
{

    [Fact]
    public async Task Real_mongo_reconciles_exact_composite_lifecycle_profile_with_replay_revoke_and_isolation()
    {
        await ownedMongo.ResetAsync();

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var database = ownedMongo.Database;
        var catalog = Catalog();
        try
        {
            await CleanupAsync(database, tenantA, tenantB, catalog);
            var tenantContext = new TenantContext();
            tenantContext.SetTenant(tenantA);
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

            var admin = await roles.UpsertSystemRoleAsync("Admin", "Admin", null, tenantA, CancellationToken.None);
            var viewer = await roles.UpsertSystemRoleAsync("Viewer", "Viewer", null, tenantA, CancellationToken.None);
            var lifecycleApprover = await roles.UpsertSystemRoleAsync(
                ProductIdentityLifecycleEntitlementGrantProfile.ApproverRole,
                "Product Identity Approver",
                null,
                tenantA,
                CancellationToken.None);
            var retirementSteward = await roles.UpsertSystemRoleAsync(
                ProductIdentityLifecycleEntitlementGrantProfile.RetirementStewardRole,
                "Product Identity Retirement Steward",
                null,
                tenantA,
                CancellationToken.None);
            var retainedManual = catalog.Single(permission => permission.Key == "manual.retained.read");
            var retainedOther = catalog.Single(permission => permission.Key == "other.retained.read");
            var brandRead = catalog.Single(permission => permission.Key == "mdm.brands.read");
            var lskuRetire = catalog.Single(permission =>
                permission.Key == ProductIdentityLifecycleEntitlementGrantProfile.LskusRetire);
            var recovery = catalog.Single(permission =>
                permission.Key == ProductIdentityLifecycleEntitlementGrantProfile.ProductIdentityLifecycleOperationRecover);
            var historicalRecoveryGrant = RolePermission.ModuleGrant(
                lifecycleApprover.Id,
                recovery.Id,
                tenantA,
                "historical-reconciliation",
                ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode);
            await rolePermissions.AssignAsync(historicalRecoveryGrant, CancellationToken.None);
            await rolePermissions.AssignAsync(
                RolePermission.ManualGrant(viewer.Id, retainedManual.Id, tenantA, "operator"),
                CancellationToken.None);
            await rolePermissions.AssignAsync(
                RolePermission.ModuleGrant(admin.Id, retainedOther.Id, tenantA, "other", "another-module"),
                CancellationToken.None);
            await rolePermissions.AssignAsync(
                RolePermission.ModuleGrant(
                    admin.Id,
                    brandRead.Id,
                    tenantA,
                    "legacy-reconciliation",
                    ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode),
                CancellationToken.None);
            await rolePermissions.AssignAsync(
                RolePermission.ManualGrant(viewer.Id, brandRead.Id, tenantA, "operator"),
                CancellationToken.None);
            await rolePermissions.AssignAsync(
                RolePermission.SystemGrant(lifecycleApprover.Id, brandRead.Id, tenantA, "system"),
                CancellationToken.None);
            await rolePermissions.AssignAsync(
                RolePermission.ModuleGrant(
                    retirementSteward.Id,
                    brandRead.Id,
                    tenantA,
                    "brand-reconciliation",
                    "brand-product-master"),
                CancellationToken.None);
            await rolePermissions.AssignAsync(
                RolePermission.ModuleGrant(
                    retirementSteward.Id,
                    lskuRetire.Id,
                    tenantA,
                    "legacy-reconciliation",
                    ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode),
                CancellationToken.None);
            await rolePermissions.AssignAsync(
                RolePermission.ManualGrant(viewer.Id, lskuRetire.Id, tenantA, "operator"),
                CancellationToken.None);
            await rolePermissions.AssignAsync(
                RolePermission.SystemGrant(lifecycleApprover.Id, lskuRetire.Id, tenantA, "system"),
                CancellationToken.None);

            var declaredKeys = catalog
                .Where(permission =>
                    permission.Module == ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode
                    || ProductIdentityLifecycleEntitlementGrantProfile.IsDeclaredCrossModuleDependencyKey(permission.Key))
                .Select(permission => permission.Key)
                .ToArray();
            Assert.Equal(42, declaredKeys.Length);
            await service.GrantModuleWithKeysAsync(
                tenantA,
                ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode,
                declaredKeys,
                "fu23-mongo-test");
            await AssertExactMatricesAsync(roles, rolePermissions, catalog, tenantA, lifecycleApprover.Id);

            // Live-upgrade shape: the amended Steward has eighteen grants after its workflow-start dependency is
            // removed, while the authoritative descriptor/global catalog still includes that dependency. Full-set
            // sync must add exactly that missing dependency without revoke/recreate or duplicate role/grant rows.
            var stewardBeforeUpgrade = await roles.GetByNameAndTenantAsync(
                ProductIdentityLifecycleEntitlementGrantProfile.StewardRole,
                tenantA,
                CancellationToken.None) ?? throw new InvalidOperationException("Steward missing.");
            var workflowStart = catalog.Single(permission =>
                permission.Key == ProductIdentityLifecycleEntitlementGrantProfile.WorkflowInstancesStart);
            var startGrant = (await rolePermissions.GetByRoleAsync(stewardBeforeUpgrade.Id, tenantA, CancellationToken.None))
                .Single(grant => grant.PermissionId == workflowStart.Id);
            await rolePermissions.RemoveByIdAsync(startGrant.Id, tenantA, CancellationToken.None);
            Assert.Equal(19, (await rolePermissions.GetByRoleAsync(stewardBeforeUpgrade.Id, tenantA, CancellationToken.None)).Count);

            await service.SyncTenantModulesWithKeysAsync(
                tenantA,
                [new EntitledModulePermissionKeys(
                    ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode.ToUpperInvariant(),
                    declaredKeys)],
                "tenant-provisioning");
            await AssertExactMatricesAsync(roles, rolePermissions, catalog, tenantA, lifecycleApprover.Id);
            await rolePermissions.AssignAsync(
                RolePermission.ModuleGrant(admin.Id, retainedOther.Id, tenantA, "other", "another-module"),
                CancellationToken.None);

            var roleCollection = database.GetCollection<Role>("roles");
            var grantCollection = database.GetCollection<RolePermission>("rolePermissions");
            var firstRoleCount = await roleCollection.CountDocumentsAsync(role => role.TenantId == tenantA);
            var firstGrantCount = await grantCollection.CountDocumentsAsync(grant => grant.TenantId == tenantA);
            Assert.Equal(12, firstRoleCount);
            Assert.Equal(82, firstGrantCount);
            Assert.Equal(75, await grantCollection.CountDocumentsAsync(grant =>
                grant.TenantId == tenantA
                && grant.GrantSource == GrantSource.Module
                && grant.SourceModuleCode == ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode));
            var preservedRecovery = Assert.Single(await grantCollection.Find(grant =>
                    grant.TenantId == tenantA
                    && grant.PermissionId == recovery.Id)
                .ToListAsync());
            Assert.Equal(historicalRecoveryGrant.Id, preservedRecovery.Id);
            Assert.Equal(lifecycleApprover.Id, preservedRecovery.RoleId);
            Assert.Equal(GrantSource.Module, preservedRecovery.GrantSource);
            Assert.Equal(ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode, preservedRecovery.SourceModuleCode);
            Assert.Equal(0, await grantCollection.CountDocumentsAsync(grant =>
                grant.TenantId == tenantA
                && grant.PermissionId == brandRead.Id
                && grant.GrantSource == GrantSource.Module
                && grant.SourceModuleCode == ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode));
            Assert.Equal(1, await grantCollection.CountDocumentsAsync(grant =>
                grant.TenantId == tenantA
                && grant.PermissionId == brandRead.Id
                && grant.GrantSource == GrantSource.Manual));
            Assert.Equal(1, await grantCollection.CountDocumentsAsync(grant =>
                grant.TenantId == tenantA
                && grant.PermissionId == brandRead.Id
                && grant.GrantSource == GrantSource.System));
            Assert.Equal(1, await grantCollection.CountDocumentsAsync(grant =>
                grant.TenantId == tenantA
                && grant.PermissionId == brandRead.Id
                && grant.SourceModuleCode == "brand-product-master"));
            Assert.Equal(0, await grantCollection.CountDocumentsAsync(grant =>
                grant.TenantId == tenantA
                && grant.PermissionId == lskuRetire.Id
                && grant.GrantSource == GrantSource.Module
                && grant.SourceModuleCode == ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode));
            Assert.Equal(1, await grantCollection.CountDocumentsAsync(grant =>
                grant.TenantId == tenantA
                && grant.PermissionId == lskuRetire.Id
                && grant.GrantSource == GrantSource.Manual));
            Assert.Equal(1, await grantCollection.CountDocumentsAsync(grant =>
                grant.TenantId == tenantA
                && grant.PermissionId == lskuRetire.Id
                && grant.GrantSource == GrantSource.System));

            await service.GrantModuleWithKeysAsync(
                tenantA,
                ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode,
                declaredKeys,
                "fu23-mongo-test");
            Assert.Equal(firstRoleCount, await roleCollection.CountDocumentsAsync(role => role.TenantId == tenantA));
            Assert.Equal(firstGrantCount, await grantCollection.CountDocumentsAsync(grant => grant.TenantId == tenantA));
            Assert.Equal(0, await database.GetCollection<UserRole>("userRoles").CountDocumentsAsync(role => role.TenantId == tenantA));

            await service.RevokeModuleAsync(
                tenantA,
                ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode,
                "fu23-mongo-test");
            Assert.Equal(0, await grantCollection.CountDocumentsAsync(grant =>
                grant.TenantId == tenantA
                && grant.GrantSource == GrantSource.Module
                && grant.SourceModuleCode == ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode));
            Assert.Equal(3, await grantCollection.CountDocumentsAsync(grant =>
                grant.TenantId == tenantA && grant.GrantSource == GrantSource.Manual));
            Assert.Equal(1, await grantCollection.CountDocumentsAsync(grant =>
                grant.TenantId == tenantA && grant.SourceModuleCode == "another-module"));
            Assert.Equal(2, await grantCollection.CountDocumentsAsync(grant =>
                grant.TenantId == tenantA && grant.GrantSource == GrantSource.System));
            Assert.Equal(1, await grantCollection.CountDocumentsAsync(grant =>
                grant.TenantId == tenantA && grant.SourceModuleCode == "brand-product-master"));

            await service.GrantModuleWithKeysAsync(
                tenantA,
                ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode,
                declaredKeys,
                "fu23-mongo-test");
            await AssertExactMatricesAsync(roles, rolePermissions, catalog, tenantA);

            var tenantAGrants = await grantCollection.CountDocumentsAsync(grant => grant.TenantId == tenantA);
            tenantContext.SetTenant(tenantB);
            await roles.UpsertSystemRoleAsync("Admin", "Admin", null, tenantB, CancellationToken.None);
            await roles.UpsertSystemRoleAsync("Viewer", "Viewer", null, tenantB, CancellationToken.None);
            await roles.CreateAsync(
                new Role(
                    ProductIdentityLifecycleEntitlementGrantProfile.ApproverRole,
                    "Operator collision",
                    null,
                    tenantB),
                CancellationToken.None);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.GrantModuleWithKeysAsync(
                tenantB,
                ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode,
                declaredKeys,
                "fu23-mongo-test"));
            Assert.Equal(0, await grantCollection.CountDocumentsAsync(grant => grant.TenantId == tenantB));
            Assert.Equal(tenantAGrants, await grantCollection.CountDocumentsAsync(grant => grant.TenantId == tenantA));
            Assert.Equal(0, await database.GetCollection<UserRole>("userRoles").CountDocumentsAsync(role =>
                role.TenantId == tenantA || role.TenantId == tenantB));

            // New action keys are owned only as module grants. Explicit manual/other-module grants
            // for those same keys survive revoke and restore; no user receives a role automatically.
            tenantContext.SetTenant(tenantA);
            var preservationRole = await roles.UpsertSystemRoleAsync(
                "TestSourcePreservation", "Test source preservation", null, tenantA, CancellationToken.None);
            var additions = new[] { "mdm.gskus.update", "mdm.gskus.withdraw",
                "mdm.gskus.request-correction", "mdm.gskus.request-retirement",
                "mdm.lskus.withdraw", "mdm.lskus.request-retirement" };
            var retainedIds = new List<Guid>();
            for (var i = 0; i < additions.Length; i++)
            {
                var permission = catalog.Single(p => p.Key == additions[i]);
                var grant = i % 2 == 0
                    ? RolePermission.ManualGrant(preservationRole.Id, permission.Id, tenantA, "test-operator")
                    : RolePermission.ModuleGrant(preservationRole.Id, permission.Id, tenantA, "test-other", "another-module");
                await rolePermissions.AssignAsync(grant, CancellationToken.None);
                retainedIds.Add(grant.Id);
            }
            await service.RevokeModuleAsync(tenantA, ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode, "test");
            Assert.Equal(6, await grantCollection.CountDocumentsAsync(g => retainedIds.Contains(g.Id)));
            await service.GrantModuleWithKeysAsync(tenantA,
                ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode, declaredKeys, "test");
            await service.GrantModuleWithKeysAsync(tenantA,
                ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode, declaredKeys, "test");
            Assert.Equal(6, await grantCollection.CountDocumentsAsync(g => retainedIds.Contains(g.Id)));
            Assert.Equal(0, await grantCollection.CountDocumentsAsync(g => g.TenantId == tenantB));
            Assert.Equal(0, await database.GetCollection<UserRole>("userRoles")
                .CountDocumentsAsync(r => r.TenantId == tenantA || r.TenantId == tenantB));
        }
        finally
        {
            await CleanupAsync(database, tenantA, tenantB, catalog);
        }
    }

    internal static List<Permission> Catalog() =>
    [
        .. ProductIdentityLifecycleEntitlementGrantProfile.BasePermissionKeys.Select(ProductPermission),
        .. ProductIdentityLifecycleEntitlementGrantProfile.PermissionKeys.Select(ProductPermission),
        .. ProductIdentityLifecycleEntitlementGrantProfile.NonHumanPermissionKeys.Select(ProductPermission),
        .. ProductAbbreviationEntitlementGrantProfile.PermissionKeys.Select(ProductPermission),
        .. ProductLegalEntityScopeEntitlementGrantProfile.PermissionKeys.Select(ProductPermission),
        new("mdm", "brands", "read", "Read Brands", null,
            moduleOverride: "brand-product-master", scope: PermissionScope.Tenant),
        new("platform", "work-aggregation.inbox", "view", "Inbox", null,
            moduleOverride: "work-aggregation", scope: PermissionScope.Tenant),
        new("platform", "workflow.instances", "start", "Start", null,
            moduleOverride: "workflow", scope: PermissionScope.Tenant),
        new("platform", "workflow.tasks", "approve", "Approve", null,
            moduleOverride: "workflow", scope: PermissionScope.Tenant),
        new("platform", "workflow.tasks", "reject", "Reject", null,
            moduleOverride: "workflow", scope: PermissionScope.Tenant),
        new("manual", "retained", "read", "Manual", null, moduleOverride: "manual-module"),
        new("other", "retained", "read", "Other", null, moduleOverride: "another-module")
    ];

    private static Permission ProductPermission(string key)
    {
        var separator = key.LastIndexOf('.');
        return new Permission(
            "mdm",
            key["mdm.".Length..separator],
            key[(separator + 1)..],
            key,
            null,
            moduleOverride: ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode,
            scope: PermissionScope.Tenant);
    }

    private static async Task AssertExactMatricesAsync(
        RoleRepository roles,
        RolePermissionRepository rolePermissions,
        IReadOnlyList<Permission> catalog,
        Guid tenantId,
        Guid? preservedRecoveryRoleId = null)
    {
        await AssertRoleAsync(roles, rolePermissions, catalog, tenantId, "Admin",
            ProductIdentityLifecycleEntitlementGrantProfile.BasePermissionKeys
                .Append(ProductAbbreviationEntitlementGrantProfile.Read)
                .Append(ProductLegalEntityScopeEntitlementGrantProfile.Read));
        await AssertRoleAsync(roles, rolePermissions, catalog, tenantId, "Viewer",
            ProductIdentityLifecycleEntitlementGrantProfile.BasePermissionKeys
                .Where(key => key.EndsWith(".read", StringComparison.Ordinal))
                .Append(ProductAbbreviationEntitlementGrantProfile.Read),
            allowAdditionalNonModuleGrants: true);
        foreach (var template in ProductAbbreviationEntitlementGrantProfile.DedicatedRoles)
        {
            await AssertRoleAsync(roles, rolePermissions, catalog, tenantId, template.RoleName, template.PermissionKeys);
        }
        foreach (var template in ProductLegalEntityScopeEntitlementGrantProfile.DedicatedRoles)
        {
            await AssertRoleAsync(roles, rolePermissions, catalog, tenantId, template.RoleName, template.PermissionKeys);
        }
        foreach (var template in ProductIdentityLifecycleEntitlementGrantProfile.DedicatedRoles)
        {
            await AssertRoleAsync(
                roles,
                rolePermissions,
                catalog,
                tenantId,
                template.RoleName,
                template.PermissionKeys,
                allowAdditionalNonModuleGrants: true);
        }

        var recoveryPermissionId = catalog.Single(permission =>
            permission.Key == ProductIdentityLifecycleEntitlementGrantProfile.ProductIdentityLifecycleOperationRecover).Id;
        var recoveryGrants = new List<RolePermission>();
        foreach (var role in await roles.GetAllByTenantAsync(tenantId, CancellationToken.None))
        {
            var grants = await rolePermissions.GetByRoleAsync(role.Id, tenantId, CancellationToken.None);
            recoveryGrants.AddRange(grants.Where(grant => grant.PermissionId == recoveryPermissionId));
        }

        if (preservedRecoveryRoleId.HasValue)
        {
            var preserved = Assert.Single(recoveryGrants);
            Assert.Equal(preservedRecoveryRoleId.Value, preserved.RoleId);
            Assert.Equal(GrantSource.Module, preserved.GrantSource);
            Assert.Equal(ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode, preserved.SourceModuleCode);
        }
        else
        {
            Assert.Empty(recoveryGrants);
        }
    }

    private static async Task AssertRoleAsync(
        RoleRepository roles,
        RolePermissionRepository rolePermissions,
        IReadOnlyList<Permission> catalog,
        Guid tenantId,
        string roleName,
        IEnumerable<string> expectedKeys,
        bool allowAdditionalNonModuleGrants = false)
    {
        var role = await roles.GetByNameAndTenantAsync(roleName, tenantId, CancellationToken.None);
        Assert.NotNull(role);
        Assert.True(role.IsSystem);
        var grants = await rolePermissions.GetByRoleAsync(role.Id, tenantId, CancellationToken.None);
        var recoveryPermissionId = catalog.Single(permission =>
            permission.Key == ProductIdentityLifecycleEntitlementGrantProfile.ProductIdentityLifecycleOperationRecover).Id;
        var matrixGrants = grants.Where(grant => grant.PermissionId != recoveryPermissionId).ToList();
        var moduleGrants = matrixGrants.Where(grant =>
            grant.GrantSource == GrantSource.Module
            && grant.SourceModuleCode == ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode).ToList();
        if (!allowAdditionalNonModuleGrants)
        {
            Assert.All(matrixGrants.Where(grant => !moduleGrants.Contains(grant)), grant =>
                Assert.Equal("another-module", grant.SourceModuleCode));
        }
        Assert.Equal(
            expectedKeys.OrderBy(key => key, StringComparer.Ordinal),
            moduleGrants.Select(grant => catalog.Single(permission => permission.Id == grant.PermissionId).Key)
                .OrderBy(key => key, StringComparer.Ordinal));
    }

    private static async Task CleanupAsync(
        IMongoDatabase database,
        Guid tenantA,
        Guid tenantB,
        IReadOnlyCollection<Permission> catalog)
    {
        var tenants = new[] { tenantA, tenantB };
        await database.GetCollection<UserRole>("userRoles").DeleteManyAsync(
            Builders<UserRole>.Filter.In(role => role.TenantId, tenants));
        await database.GetCollection<RolePermission>("rolePermissions").DeleteManyAsync(
            Builders<RolePermission>.Filter.In(grant => grant.TenantId, tenants));
        await database.GetCollection<Role>("roles").DeleteManyAsync(
            Builders<Role>.Filter.In(role => role.TenantId, tenants));
        await database.GetCollection<Permission>("permissions").DeleteManyAsync(
            Builders<Permission>.Filter.In(permission => permission.Key,
                catalog.Select(permission => permission.Key).Distinct(StringComparer.Ordinal)));
    }
}
