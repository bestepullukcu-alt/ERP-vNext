using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Tests.Roles;

public sealed class ProductIdentityLifecycleEntitlementGrantProfileTests
{
    [Fact]
    public void Profile_owns_exact_lifecycle_non_human_and_cross_module_descriptor_sets()
    {
        // MOD-0290-FU05 slice 2: 18 → 19 (+ mdm.global-products.update-brand), the exact set below.
        Assert.Equal(19, ProductIdentityLifecycleEntitlementGrantProfile.PermissionKeys.Count);
        Assert.Equal(
            [
                "mdm.finished-goods.retire", "mdm.finished-goods.submit",
                "mdm.global-products.request-correction", "mdm.global-products.request-retirement",
                "mdm.global-products.retire", "mdm.global-products.submit", "mdm.global-products.update",
                "mdm.global-products.update-brand", "mdm.global-products.withdraw",
                "mdm.gskus.request-correction", "mdm.gskus.request-retirement", "mdm.gskus.retire", "mdm.gskus.submit",
                "mdm.gskus.update", "mdm.gskus.withdraw",
                "mdm.lskus.request-retirement", "mdm.lskus.retire", "mdm.lskus.submit", "mdm.lskus.withdraw"
            ],
            ProductIdentityLifecycleEntitlementGrantProfile.PermissionKeys.Order(StringComparer.Ordinal));
        Assert.Contains(ProductIdentityLifecycleEntitlementGrantProfile.GlobalProductsUpdate,
            ProductIdentityLifecycleEntitlementGrantProfile.PermissionKeys);
        Assert.Contains(ProductIdentityLifecycleEntitlementGrantProfile.GlobalProductsWithdraw,
            ProductIdentityLifecycleEntitlementGrantProfile.PermissionKeys);
        Assert.Contains(ProductIdentityLifecycleEntitlementGrantProfile.GlobalProductsRequestCorrection,
            ProductIdentityLifecycleEntitlementGrantProfile.PermissionKeys);
        Assert.Contains(ProductIdentityLifecycleEntitlementGrantProfile.GlobalProductsRequestRetirement,
            ProductIdentityLifecycleEntitlementGrantProfile.PermissionKeys);
        Assert.All(ProductIdentityLifecycleEntitlementGrantProfile.PermissionKeys, key =>
        {
            Assert.DoesNotContain("product-definition-revisions", key, StringComparison.Ordinal);
            Assert.DoesNotContain(".approve", key, StringComparison.Ordinal);
            Assert.DoesNotContain(".reject", key, StringComparison.Ordinal);
        });
        Assert.Equal(8, ProductIdentityLifecycleEntitlementGrantProfile.BasePermissionKeys.Count);
        Assert.Equal(
            [ProductIdentityLifecycleEntitlementGrantProfile.ProductIdentityLifecycleOperationRecover],
            ProductIdentityLifecycleEntitlementGrantProfile.NonHumanPermissionKeys);
        // FIX2 (item 1, CT model A) — Brand is its own module (brand-product-master); the product module declares
        // no cross-module key and does not depend on mdm.brands.read.
        Assert.Empty(ProductIdentityLifecycleEntitlementGrantProfile.DeclaredCrossModuleDependencyKeys);
        Assert.Equal(4, ProductIdentityLifecycleEntitlementGrantProfile.SharedDependencyKeys.Count);
        Assert.DoesNotContain("mdm.brands.read", ProductIdentityLifecycleEntitlementGrantProfile.SharedDependencyKeys);
        Assert.DoesNotContain(ProductIdentityLifecycleEntitlementGrantProfile.SharedDependencyKeys,
            key => key.StartsWith("mdm.brands.", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Dedicated_roles_have_exact_twenty_one_seven_nine_matrices()
    {
        var roles = ProductIdentityLifecycleEntitlementGrantProfile.DedicatedRoles
            .ToDictionary(role => role.RoleName, StringComparer.Ordinal);

        // MOD-0290-FU05 slice 2: 20 → 21 — update-brand goes to the Steward (the draft edit's role) and to no other role.
        Assert.Equal(21, roles[ProductIdentityLifecycleEntitlementGrantProfile.StewardRole].PermissionKeys.Count);
        Assert.Equal(7, roles[ProductIdentityLifecycleEntitlementGrantProfile.ApproverRole].PermissionKeys.Count);
        // FG FIX1 item 9 (CT decision B): 10 → 9 — direct FG retire is a system action, no longer the steward's.
        Assert.Equal(9, roles[ProductIdentityLifecycleEntitlementGrantProfile.RetirementStewardRole].PermissionKeys.Count);

        var approver = roles[ProductIdentityLifecycleEntitlementGrantProfile.ApproverRole].PermissionKeys;
        Assert.Contains(ProductIdentityLifecycleEntitlementGrantProfile.WorkflowInstancesStart,
            roles[ProductIdentityLifecycleEntitlementGrantProfile.StewardRole].PermissionKeys);
        Assert.DoesNotContain(ProductIdentityLifecycleEntitlementGrantProfile.WorkflowInstancesStart, approver);
        Assert.Contains(ProductIdentityLifecycleEntitlementGrantProfile.WorkCenterInboxView, approver);
        Assert.Contains(ProductIdentityLifecycleEntitlementGrantProfile.WorkflowTasksApprove, approver);
        Assert.Contains(ProductIdentityLifecycleEntitlementGrantProfile.WorkflowTasksReject, approver);
        Assert.DoesNotContain(approver, key => key.StartsWith("mdm.", StringComparison.Ordinal)
                                               && !key.EndsWith(".read", StringComparison.Ordinal));

        var steward = roles[ProductIdentityLifecycleEntitlementGrantProfile.StewardRole].PermissionKeys;
        Assert.Contains(ProductIdentityLifecycleEntitlementGrantProfile.GlobalProductsUpdate, steward);
        Assert.Contains(ProductIdentityLifecycleEntitlementGrantProfile.GlobalProductsWithdraw, steward);
        Assert.Contains(ProductIdentityLifecycleEntitlementGrantProfile.GlobalProductsRequestCorrection, steward);
        Assert.Contains(ProductIdentityLifecycleEntitlementGrantProfile.GlobalProductsUpdateBrand, steward);
        Assert.Equal([ProductIdentityLifecycleEntitlementGrantProfile.StewardRole],
            ProductIdentityLifecycleEntitlementGrantProfile.DedicatedRoles
                .Where(role => role.PermissionKeys.Contains(ProductIdentityLifecycleEntitlementGrantProfile.GlobalProductsUpdateBrand))
                .Select(role => role.RoleName));
        Assert.True(ProductIdentityLifecycleEntitlementGrantProfile.IsDedicatedOnlyPermissionKey(
            ProductIdentityLifecycleEntitlementGrantProfile.GlobalProductsUpdateBrand));
        var retirement = roles[ProductIdentityLifecycleEntitlementGrantProfile.RetirementStewardRole].PermissionKeys;
        Assert.Contains(ProductIdentityLifecycleEntitlementGrantProfile.GlobalProductsWithdraw, retirement);
        Assert.Contains(ProductIdentityLifecycleEntitlementGrantProfile.GlobalProductsRequestRetirement, retirement);
        Assert.Contains(ProductIdentityLifecycleEntitlementGrantProfile.WorkflowInstancesStart, retirement);
        Assert.DoesNotContain(ProductIdentityLifecycleEntitlementGrantProfile.GlobalProductsRetire, retirement);
        Assert.DoesNotContain(ProductIdentityLifecycleEntitlementGrantProfile.FinishedGoodsRetire, retirement);
        Assert.DoesNotContain(
            ProductIdentityLifecycleEntitlementGrantProfile.ProductIdentityLifecycleOperationRecover,
            ProductIdentityLifecycleEntitlementGrantProfile.DedicatedRoles.SelectMany(role => role.PermissionKeys));
        Assert.DoesNotContain(
            ProductIdentityLifecycleEntitlementGrantProfile.DedicatedRoles.SelectMany(role => role.PermissionKeys),
            key => key.StartsWith("mdm.brands.", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("approve")]
    [InlineData("reject")]
    [InlineData("revision")]
    [InlineData("alias")]
    [InlineData("duplicate")]
    public void Declared_permission_set_fails_closed_on_drift(string drift)
    {
        var keys = DeclaredKeys();
        switch (drift)
        {
            case "missing": keys.Remove(ProductIdentityLifecycleEntitlementGrantProfile.GskusSubmit); break;
            case "approve": keys.Add("mdm.gskus.approve"); break;
            case "reject": keys.Add("mdm.lskus.reject"); break;
            case "revision": keys.Add("mdm.product-definition-revisions.submit"); break;
            case "alias":
                keys[keys.IndexOf(ProductIdentityLifecycleEntitlementGrantProfile.GskusSubmit)] =
                    ProductIdentityLifecycleEntitlementGrantProfile.GskusSubmit.ToUpperInvariant();
                break;
            case "duplicate":
                keys.Add(ProductIdentityLifecycleEntitlementGrantProfile.GlobalProductsRequestRetirement);
                break;
        }

        Assert.Throws<InvalidOperationException>(() =>
            ProductIdentityLifecycleEntitlementGrantProfile.ValidateExactDeclaredPermissionSet(keys));
    }

    [Theory]
    [InlineData("module")]
    [InlineData("resource")]
    [InlineData("action")]
    [InlineData("scope")]
    [InlineData("deleted")]
    public void Global_product_lifecycle_additions_require_exact_active_tenant_tuples(string drift)
    {
        var declared = ProductCatalog();
        var targetIndex = declared.FindIndex(permission =>
            permission.Key == ProductIdentityLifecycleEntitlementGrantProfile.GlobalProductsRequestRetirement);
        Assert.True(targetIndex >= 0);
        declared[targetIndex] = new Permission(
            "mdm",
            drift == "resource" ? "global-product" : "global-products",
            drift == "action" ? "retire" : "request-retirement",
            "Request Retirement",
            null,
            moduleOverride: drift == "module" ? "another-module" : ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode,
            scope: drift == "scope" ? PermissionScope.PlatformAdmin : PermissionScope.Tenant)
        { IsDeleted = drift == "deleted" };

        Assert.Throws<InvalidOperationException>(() =>
            ProductIdentityLifecycleEntitlementGrantProfile.ValidateAndResolveDefinitions(
                declared,
                declared.Concat(SharedDependencies())));
    }

    [Fact]
    public void Definitions_resolve_exact_existing_shared_dependencies_without_reowning_them()
    {
        var declared = ProductCatalog();
        var global = declared.Concat(SharedDependencies()).ToList();

        var resolved = ProductIdentityLifecycleEntitlementGrantProfile.ValidateAndResolveDefinitions(declared, global);

        Assert.Equal(ProductIdentityLifecycleEntitlementGrantProfile.SharedDependencyKeys, resolved.Keys.ToHashSet(StringComparer.Ordinal));
        Assert.Equal(4, resolved.Count);
        Assert.DoesNotContain("mdm.brands.read", resolved.Keys);
        Assert.Equal("work-aggregation", resolved[ProductIdentityLifecycleEntitlementGrantProfile.WorkCenterInboxView].Module);
        Assert.Equal("workflow", resolved[ProductIdentityLifecycleEntitlementGrantProfile.WorkflowInstancesStart].Module);
        Assert.Equal("workflow", resolved[ProductIdentityLifecycleEntitlementGrantProfile.WorkflowTasksApprove].Module);
        Assert.Equal("workflow", resolved[ProductIdentityLifecycleEntitlementGrantProfile.WorkflowTasksReject].Module);
    }

    [Theory]
    [InlineData("module")]
    [InlineData("resource")]
    [InlineData("action")]
    [InlineData("scope")]
    [InlineData("deleted")]
    public void Workflow_instance_start_dependency_requires_exact_active_tenant_tuple(string drift)
    {
        var declared = ProductCatalog();
        var dependencies = SharedDependencies();
        var index = dependencies.FindIndex(permission =>
            permission.Key == ProductIdentityLifecycleEntitlementGrantProfile.WorkflowInstancesStart);
        Assert.True(index >= 0);
        var invalid = new Permission(
            "platform",
            drift == "resource" ? "workflow.instance" : "workflow.instances",
            drift == "action" ? "manage" : "start",
            "Start",
            null,
            moduleOverride: drift == "module" ? "product-item-sku-master" : "workflow",
            scope: drift == "scope" ? PermissionScope.PlatformAdmin : PermissionScope.Tenant)
        { IsDeleted = drift == "deleted" };
        dependencies[index] = invalid;

        Assert.Throws<InvalidOperationException>(() =>
            ProductIdentityLifecycleEntitlementGrantProfile.ValidateAndResolveDefinitions(
                declared, declared.Concat(dependencies)));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    public void Workflow_instance_start_dependency_requires_exactly_one_definition(string drift)
    {
        var declared = ProductCatalog();
        var dependencies = SharedDependencies();
        var start = dependencies.Single(permission =>
            permission.Key == ProductIdentityLifecycleEntitlementGrantProfile.WorkflowInstancesStart);
        if (drift == "missing")
        {
            dependencies.Remove(start);
        }
        else
        {
            dependencies.Add(new Permission(
                "platform", "workflow.instances", "start", "Duplicate Start", null,
                moduleOverride: "workflow", scope: PermissionScope.Tenant));
        }

        Assert.Throws<InvalidOperationException>(() =>
            ProductIdentityLifecycleEntitlementGrantProfile.ValidateAndResolveDefinitions(
                declared, declared.Concat(dependencies)));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong-module")]
    [InlineData("wrong-scope")]
    [InlineData("deleted")]
    public void Shared_dependency_drift_fails_closed(string drift)
    {
        var declared = ProductCatalog();
        var dependencies = SharedDependencies();
        if (drift == "missing")
        {
            dependencies.RemoveAll(permission =>
                permission.Key == ProductIdentityLifecycleEntitlementGrantProfile.WorkflowTasksReject);
        }
        else if (drift == "wrong-module")
        {
            dependencies.RemoveAll(permission =>
                permission.Key == ProductIdentityLifecycleEntitlementGrantProfile.WorkflowTasksApprove);
            dependencies.Add(new Permission(
                "platform",
                "workflow.tasks",
                "approve",
                "Approve",
                null,
                moduleOverride: "wrong-module",
                scope: PermissionScope.Tenant));
        }
        else if (drift == "wrong-scope")
        {
            dependencies.RemoveAll(permission =>
                permission.Key == ProductIdentityLifecycleEntitlementGrantProfile.WorkflowTasksApprove);
            dependencies.Add(new Permission(
                "platform",
                "workflow.tasks",
                "approve",
                "Approve",
                null,
                moduleOverride: "workflow",
                scope: PermissionScope.PlatformAdmin));
        }
        else
        {
            dependencies.Single(permission =>
                    permission.Key == ProductIdentityLifecycleEntitlementGrantProfile.WorkCenterInboxView)
                .IsDeleted = true;
        }

        Assert.Throws<InvalidOperationException>(() =>
            ProductIdentityLifecycleEntitlementGrantProfile.ValidateAndResolveDefinitions(declared, declared.Concat(dependencies)));
    }

    [Theory]
    [InlineData("mdm.brands.read")]
    [InlineData("mdm.brands.create")]
    [InlineData("mdm.brands.export")]
    [InlineData("MDM.BRANDS.READ")]
    public void Product_descriptor_carrying_a_brand_key_is_refused(string brandKey)
    {
        // FIX2 (item 1, CT model A) — a Brand key in the product module's declared set is a stale manifest.
        Assert.True(ProductIdentityLifecycleEntitlementGrantProfile.IsBrandModuleKey(brandKey));
        var keys = DeclaredKeys();
        keys.Add(brandKey);

        var error = Assert.Throws<InvalidOperationException>(() =>
            ProductIdentityLifecycleEntitlementGrantProfile.ValidateExactDeclaredPermissionSet(keys));
        Assert.Contains("no Brand", error.Message, StringComparison.Ordinal);

        var separator = brandKey.LastIndexOf('.');
        var declared = ProductCatalog();
        declared.Add(new Permission(
            "mdm",
            brandKey["mdm.".Length..separator].ToLowerInvariant(),
            brandKey[(separator + 1)..].ToLowerInvariant(),
            brandKey,
            null,
            moduleOverride: "brand-product-master",
            scope: PermissionScope.Tenant));
        Assert.Throws<InvalidOperationException>(() =>
            ProductIdentityLifecycleEntitlementGrantProfile.ValidateAndResolveDefinitions(
                declared,
                declared.Concat(SharedDependencies())));
    }

    [Fact]
    public void Product_descriptor_without_any_brand_key_passes_the_exact_set()
    {
        var keys = DeclaredKeys();
        Assert.DoesNotContain(keys, ProductIdentityLifecycleEntitlementGrantProfile.IsBrandModuleKey);
        // MOD-0290-FU05 slice 2: 27 → 28 (+ mdm.global-products.update-brand — not a Brand-module key).
        Assert.Equal(28, keys.Count);
        Assert.False(ProductIdentityLifecycleEntitlementGrantProfile.IsBrandModuleKey(
            ProductIdentityLifecycleEntitlementGrantProfile.GlobalProductsUpdateBrand));

        ProductIdentityLifecycleEntitlementGrantProfile.ValidateExactDeclaredPermissionSet(keys);
        var declared = ProductCatalog();
        var resolved = ProductIdentityLifecycleEntitlementGrantProfile.ValidateAndResolveDefinitions(
            declared,
            declared.Concat(SharedDependencies()));
        Assert.Equal(ProductIdentityLifecycleEntitlementGrantProfile.SharedDependencyKeys,
            resolved.Keys.ToHashSet(StringComparer.Ordinal));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("module")]
    [InlineData("resource")]
    [InlineData("action")]
    [InlineData("scope")]
    [InlineData("deleted")]
    public void Recovery_descriptor_is_exact_and_remains_non_human(string drift)
    {
        var declared = ProductCatalog();
        var recovery = declared.Single(permission =>
            permission.Key == ProductIdentityLifecycleEntitlementGrantProfile.ProductIdentityLifecycleOperationRecover);
        declared.Remove(recovery);
        if (drift != "missing")
        {
            declared.Add(new Permission(
                "mdm",
                drift == "resource" ? "product-identity.lifecycle-operation" : "product-identity.lifecycle-operations",
                drift == "action" ? "execute" : "recover",
                "Recover Product Identity Lifecycle Operation",
                null,
                moduleOverride: drift == "module" ? "another-module" : ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode,
                scope: drift == "scope" ? PermissionScope.PlatformAdmin : PermissionScope.Tenant)
            { IsDeleted = drift == "deleted" });
        }

        Assert.Throws<InvalidOperationException>(() =>
            ProductIdentityLifecycleEntitlementGrantProfile.ValidateAndResolveDefinitions(
                declared,
                declared.Concat(SharedDependencies())));
        Assert.DoesNotContain(
            ProductIdentityLifecycleEntitlementGrantProfile.DedicatedRoles,
            role => role.PermissionKeys.Contains(
                ProductIdentityLifecycleEntitlementGrantProfile.ProductIdentityLifecycleOperationRecover));
    }

    [Theory]
    [InlineData("mdm.gskus.update", "ProductDataSteward")]
    [InlineData("mdm.gskus.withdraw", "ProductDataSteward")]
    [InlineData("mdm.gskus.request-correction", "ProductDataSteward")]
    [InlineData("mdm.gskus.request-retirement", "ProductIdentityRetirementSteward")]
    public void Gsku_amendment_has_one_exact_responsible_role_and_rejects_catalog_drift(string key, string role)
    {
        Assert.Contains(key, ProductIdentityLifecycleEntitlementGrantProfile.PermissionKeys);
        Assert.Equal(role, Assert.Single(ProductIdentityLifecycleEntitlementGrantProfile.DedicatedRoles,
            template => template.PermissionKeys.Contains(key)).RoleName);
        var keys = DeclaredKeys();
        keys.Remove(key);
        Assert.Throws<InvalidOperationException>(() =>
            ProductIdentityLifecycleEntitlementGrantProfile.ValidateExactDeclaredPermissionSet(keys));
        keys.Add(key.ToUpperInvariant());
        Assert.Throws<InvalidOperationException>(() =>
            ProductIdentityLifecycleEntitlementGrantProfile.ValidateExactDeclaredPermissionSet(keys));
        foreach (var drift in new[] { "module", "resource", "action", "scope", "deleted" })
        {
            var catalog = ProductCatalog();
            catalog.RemoveAll(permission => permission.Key == key);
            catalog.Add(new Permission("mdm", drift == "resource" ? "gsku" : "gskus",
                drift == "action" ? "read" : key["mdm.gskus.".Length..], key, null,
                moduleOverride: drift == "module" ? "another-module" : ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode,
                scope: drift == "scope" ? PermissionScope.PlatformAdmin : PermissionScope.Tenant)
                { IsDeleted = drift == "deleted" });
            Assert.Throws<InvalidOperationException>(() =>
                ProductIdentityLifecycleEntitlementGrantProfile.ValidateAndResolveDefinitions(catalog,
                    catalog.Concat(SharedDependencies())));
        }
    }

    [Theory]
    [InlineData("mdm.lskus.withdraw", "ProductDataSteward")]
    [InlineData("mdm.lskus.request-retirement", "ProductIdentityRetirementSteward")]
    public void Lsku_amendment_has_one_exact_owner_and_no_other_role_mutation(string key, string role)
    {
        Assert.Equal(role, Assert.Single(ProductIdentityLifecycleEntitlementGrantProfile.DedicatedRoles,
            template => template.PermissionKeys.Contains(key)).RoleName);
        var retirement = ProductIdentityLifecycleEntitlementGrantProfile.DedicatedRoles.Single(x =>
            x.RoleName == ProductIdentityLifecycleEntitlementGrantProfile.RetirementStewardRole);
        Assert.DoesNotContain("mdm.lskus.retire", retirement.PermissionKeys);
        Assert.DoesNotContain("mdm.gskus.retire", retirement.PermissionKeys); // B5: a system action since GSKU FIX1
        var keys = DeclaredKeys();
        keys.Remove(key);
        Assert.Throws<InvalidOperationException>(() => ProductIdentityLifecycleEntitlementGrantProfile.ValidateExactDeclaredPermissionSet(keys));
        keys.Add(key.ToUpperInvariant());
        Assert.Throws<InvalidOperationException>(() => ProductIdentityLifecycleEntitlementGrantProfile.ValidateExactDeclaredPermissionSet(keys));
    }

    // ------------------------------------------------------------------------------------------- BL-592 FIX1
    // Written out — never read from the production constants.
    private static readonly string[] SixItemKeys =
    [
        "mdm.items.read", "mdm.items.create", "mdm.items.update", "mdm.items.submit", "mdm.items.deactivate",
        "mdm.items.retire"
    ];

    // CT decision: the allowed item sets are the six, or the six + mdm.items.withdraw (FU04 S2), both all-or-nothing.
    [Fact]
    public void BL592_FIX1_The_six_item_keys_with_withdraw_are_an_allowed_set()
    {
        var keys = DeclaredKeys();
        keys.AddRange(SixItemKeys);
        keys.Add("mdm.items.withdraw");
        keys.Add("mdm.product-master.read");

        ProductIdentityLifecycleEntitlementGrantProfile.ValidateExactDeclaredPermissionSet(keys);
    }

    [Theory]
    [InlineData("mdm.items.create, mdm.items.deactivate, mdm.items.read, mdm.items.retire, mdm.items.submit, mdm.items.update")]
    [InlineData("mdm.items.retire")]
    public void BL592_FIX1_Withdraw_without_all_six_is_refused_naming_the_missing_keys(string missing)
    {
        var keys = DeclaredKeys();
        var absent = missing.Split(", ");
        keys.AddRange(SixItemKeys.Where(key => !absent.Contains(key)));
        keys.Add("mdm.items.withdraw");

        var refused = Assert.Throws<InvalidOperationException>(() =>
            ProductIdentityLifecycleEntitlementGrantProfile.ValidateExactDeclaredPermissionSet(keys));

        Assert.Contains($"Missing: [{missing}].", refused.Message, StringComparison.Ordinal);
        Assert.Contains("Unexpected: [].", refused.Message, StringComparison.Ordinal);
    }

    // Mutation P2 (the two resource prefixes compared Ordinal instead of OrdinalIgnoreCase): a differently cased key of
    // either resource must still be seen — and named — not skipped as a key the rule never looked at.
    [Theory]
    [InlineData("MDM.ITEMS.READ")]
    [InlineData("Mdm.Product-Master.Read")]
    public void BL592_FIX1_A_differently_cased_item_or_product_master_key_is_refused_by_name(string cased)
    {
        var keys = DeclaredKeys();
        keys.AddRange(SixItemKeys);
        keys.Add("mdm.product-master.read");
        keys.Add(cased);

        var refused = Assert.Throws<InvalidOperationException>(() =>
            ProductIdentityLifecycleEntitlementGrantProfile.ValidateExactDeclaredPermissionSet(keys));

        Assert.Contains($"Unexpected: [{cased}].", refused.Message, StringComparison.Ordinal);
    }

    // Mutation P1 (the item / product-master keys dropped from the definition check): a catalogued item or read-contract
    // row under another module, with platform scope, or soft-deleted is refused like any product key.
    [Theory]
    [InlineData("mdm.items.update", "module")]
    [InlineData("mdm.items.update", "scope")]
    [InlineData("mdm.items.update", "deleted")]
    [InlineData("mdm.items.withdraw", "scope")]
    [InlineData("mdm.product-master.read", "module")]
    [InlineData("mdm.product-master.read", "scope")]
    [InlineData("mdm.product-master.read", "deleted")]
    public void BL592_FIX1_Item_and_read_contract_definitions_must_match_the_contract(string key, string drift)
    {
        var declared = ProductCatalog();
        declared.AddRange(SixItemKeys.Append("mdm.items.withdraw").Append("mdm.product-master.read")
            .Where(candidate => candidate != key)
            .Select(ProductPermission));
        var separator = key.LastIndexOf('.');
        declared.Add(new Permission("mdm", key["mdm.".Length..separator], key[(separator + 1)..], key, null,
            moduleOverride: drift == "module" ? "another-module" : ProductIdentityLifecycleEntitlementGrantProfile.ModuleCode,
            scope: drift == "scope" ? PermissionScope.PlatformAdmin : PermissionScope.Tenant)
            { IsDeleted = drift == "deleted" });

        var refused = Assert.Throws<InvalidOperationException>(() =>
            ProductIdentityLifecycleEntitlementGrantProfile.ValidateAndResolveDefinitions(
                declared,
                declared.Concat(SharedDependencies())));

        Assert.Equal("Product Identity permission catalog definitions do not match the exact approved contract.", refused.Message);
    }

    private static List<Permission> ProductCatalog()
        => ProductIdentityLifecycleEntitlementGrantProfile.BasePermissionKeys
            .Concat(ProductIdentityLifecycleEntitlementGrantProfile.PermissionKeys)
            .Concat(ProductIdentityLifecycleEntitlementGrantProfile.NonHumanPermissionKeys)
            .Select(ProductPermission)
            .ToList();

    private static List<string> DeclaredKeys()
        => ProductIdentityLifecycleEntitlementGrantProfile.BasePermissionKeys
            .Concat(ProductIdentityLifecycleEntitlementGrantProfile.PermissionKeys)
            .Concat(ProductIdentityLifecycleEntitlementGrantProfile.NonHumanPermissionKeys)
            .Concat(ProductIdentityLifecycleEntitlementGrantProfile.DeclaredCrossModuleDependencyKeys)
            .ToList();

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

    private static List<Permission> SharedDependencies() =>
    [
        new("platform", "work-aggregation.inbox", "view", "Inbox", null,
            moduleOverride: "work-aggregation", scope: PermissionScope.Tenant),
        new("platform", "workflow.instances", "start", "Start", null,
            moduleOverride: "workflow", scope: PermissionScope.Tenant),
        new("platform", "workflow.tasks", "approve", "Approve", null,
            moduleOverride: "workflow", scope: PermissionScope.Tenant),
        new("platform", "workflow.tasks", "reject", "Reject", null,
            moduleOverride: "workflow", scope: PermissionScope.Tenant)
    ];
}
