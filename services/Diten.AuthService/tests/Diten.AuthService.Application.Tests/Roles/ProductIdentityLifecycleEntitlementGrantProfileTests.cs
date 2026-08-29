using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Tests.Roles;

public sealed class ProductIdentityLifecycleEntitlementGrantProfileTests
{
    [Fact]
    public void Profile_owns_exact_eight_submit_retire_keys_and_zero_revision_or_decision_keys()
    {
        Assert.Equal(8, ProductIdentityLifecycleEntitlementGrantProfile.PermissionKeys.Count);
        Assert.All(ProductIdentityLifecycleEntitlementGrantProfile.PermissionKeys, key =>
        {
            Assert.True(key.EndsWith(".submit", StringComparison.Ordinal)
                        || key.EndsWith(".retire", StringComparison.Ordinal));
            Assert.DoesNotContain("product-definition-revisions", key, StringComparison.Ordinal);
            Assert.DoesNotContain(".approve", key, StringComparison.Ordinal);
            Assert.DoesNotContain(".reject", key, StringComparison.Ordinal);
        });
        Assert.Equal(8, ProductIdentityLifecycleEntitlementGrantProfile.BasePermissionKeys.Count);
        Assert.Equal(3, ProductIdentityLifecycleEntitlementGrantProfile.SharedDependencyKeys.Count);
    }

    [Fact]
    public void Dedicated_roles_have_exact_twelve_seven_eight_matrices()
    {
        var roles = ProductIdentityLifecycleEntitlementGrantProfile.DedicatedRoles
            .ToDictionary(role => role.RoleName, StringComparer.Ordinal);

        Assert.Equal(12, roles[ProductIdentityLifecycleEntitlementGrantProfile.StewardRole].PermissionKeys.Count);
        Assert.Equal(7, roles[ProductIdentityLifecycleEntitlementGrantProfile.ApproverRole].PermissionKeys.Count);
        Assert.Equal(8, roles[ProductIdentityLifecycleEntitlementGrantProfile.RetirementStewardRole].PermissionKeys.Count);

        var approver = roles[ProductIdentityLifecycleEntitlementGrantProfile.ApproverRole].PermissionKeys;
        Assert.True(approver.IsSupersetOf(ProductIdentityLifecycleEntitlementGrantProfile.SharedDependencyKeys));
        Assert.DoesNotContain(approver, key => key.StartsWith("mdm.", StringComparison.Ordinal)
                                               && !key.EndsWith(".read", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("approve")]
    [InlineData("reject")]
    [InlineData("revision")]
    [InlineData("alias")]
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
        }

        Assert.Throws<InvalidOperationException>(() =>
            ProductIdentityLifecycleEntitlementGrantProfile.ValidateExactDeclaredPermissionSet(keys));
    }

    [Fact]
    public void Definitions_resolve_exact_existing_shared_dependencies_without_reowning_them()
    {
        var declared = ProductCatalog();
        var global = declared.Concat(SharedDependencies()).ToList();

        var resolved = ProductIdentityLifecycleEntitlementGrantProfile.ValidateAndResolveDefinitions(declared, global);

        Assert.Equal(ProductIdentityLifecycleEntitlementGrantProfile.SharedDependencyKeys, resolved.Keys.ToHashSet(StringComparer.Ordinal));
        Assert.Equal("work-aggregation", resolved[ProductIdentityLifecycleEntitlementGrantProfile.WorkCenterInboxView].Module);
        Assert.Equal("workflow", resolved[ProductIdentityLifecycleEntitlementGrantProfile.WorkflowTasksApprove].Module);
        Assert.Equal("workflow", resolved[ProductIdentityLifecycleEntitlementGrantProfile.WorkflowTasksReject].Module);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong-module")]
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
        new("platform", "workflow.tasks", "approve", "Approve", null,
            moduleOverride: "workflow", scope: PermissionScope.Tenant),
        new("platform", "workflow.tasks", "reject", "Reject", null,
            moduleOverride: "workflow", scope: PermissionScope.Tenant)
    ];
}
