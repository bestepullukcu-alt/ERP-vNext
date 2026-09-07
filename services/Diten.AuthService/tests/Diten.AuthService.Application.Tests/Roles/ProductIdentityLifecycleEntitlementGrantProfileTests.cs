using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Tests.Roles;

public sealed class ProductIdentityLifecycleEntitlementGrantProfileTests
{
    [Fact]
    public void Profile_owns_exact_twelve_lifecycle_keys_and_zero_revision_or_decision_keys()
    {
        Assert.Equal(12, ProductIdentityLifecycleEntitlementGrantProfile.PermissionKeys.Count);
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
        Assert.Equal(4, ProductIdentityLifecycleEntitlementGrantProfile.SharedDependencyKeys.Count);
    }

    [Fact]
    public void Dedicated_roles_have_exact_sixteen_seven_ten_matrices()
    {
        var roles = ProductIdentityLifecycleEntitlementGrantProfile.DedicatedRoles
            .ToDictionary(role => role.RoleName, StringComparer.Ordinal);

        Assert.Equal(16, roles[ProductIdentityLifecycleEntitlementGrantProfile.StewardRole].PermissionKeys.Count);
        Assert.Equal(7, roles[ProductIdentityLifecycleEntitlementGrantProfile.ApproverRole].PermissionKeys.Count);
        Assert.Equal(10, roles[ProductIdentityLifecycleEntitlementGrantProfile.RetirementStewardRole].PermissionKeys.Count);

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
        var retirement = roles[ProductIdentityLifecycleEntitlementGrantProfile.RetirementStewardRole].PermissionKeys;
        Assert.Contains(ProductIdentityLifecycleEntitlementGrantProfile.GlobalProductsWithdraw, retirement);
        Assert.Contains(ProductIdentityLifecycleEntitlementGrantProfile.GlobalProductsRequestRetirement, retirement);
        Assert.Contains(ProductIdentityLifecycleEntitlementGrantProfile.WorkflowInstancesStart, retirement);
        Assert.DoesNotContain(ProductIdentityLifecycleEntitlementGrantProfile.GlobalProductsRetire, retirement);
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
        var keys = ProductIdentityLifecycleEntitlementGrantProfile.BasePermissionKeys
            .Concat(ProductIdentityLifecycleEntitlementGrantProfile.PermissionKeys)
            .ToList();
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

    private static List<Permission> ProductCatalog()
        => ProductIdentityLifecycleEntitlementGrantProfile.BasePermissionKeys
            .Concat(ProductIdentityLifecycleEntitlementGrantProfile.PermissionKeys)
            .Select(ProductPermission)
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
