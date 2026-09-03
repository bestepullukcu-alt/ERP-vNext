using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Domain.Entities;

namespace Diten.AuthService.Application.Tests.Roles;

public sealed class ProductIdentityRecoveryOperatorEntitlementGrantProfileTests
{
    [Fact]
    public void Exact_profile_is_singleton_and_only_applies_to_the_canonical_module()
    {
        Assert.True(ProductIdentityRecoveryOperatorEntitlementGrantProfile.AppliesTo(
            ProductIdentityRecoveryOperatorEntitlementGrantProfile.ModuleCode,
            [ProductIdentityRecoveryOperatorEntitlementGrantProfile.PermissionKey]));
        Assert.False(ProductIdentityRecoveryOperatorEntitlementGrantProfile.AppliesTo(
            "another-module",
            [ProductIdentityRecoveryOperatorEntitlementGrantProfile.PermissionKey]));
        Assert.Equal("ProductIdentityRecoveryOperator", ProductIdentityRecoveryOperatorEntitlementGrantProfile.RoleName);
    }

    [Theory]
    [InlineData("mdm.product-identity.lifecycle-operations.RECOVER")]
    [InlineData("mdm.product-identity.lifecycle-operations.recover ")]
    [InlineData("mdm.product-identity.lifecycle-operations.recover.extra")]
    public void Alias_or_drift_is_not_the_recovery_permission(string key)
        => Assert.False(ProductIdentityRecoveryOperatorEntitlementGrantProfile.IsRecoveryPermissionKey(key));

    [Fact]
    public void Definition_requires_exact_module_resource_action_scope_and_active_state()
    {
        var valid = Permission();
        Assert.Same(
            valid,
            ProductIdentityRecoveryOperatorEntitlementGrantProfile.ValidateAndResolveDefinition([valid]));

        var wrongScope = Permission(scope: PermissionScope.PlatformAdmin);
        Assert.Throws<InvalidOperationException>(() =>
            ProductIdentityRecoveryOperatorEntitlementGrantProfile.ValidateAndResolveDefinition([wrongScope]));
    }

    private static Permission Permission(PermissionScope scope = PermissionScope.Tenant)
        => new(
            "mdm",
            ProductIdentityRecoveryOperatorEntitlementGrantProfile.Resource,
            ProductIdentityRecoveryOperatorEntitlementGrantProfile.Action,
            "Recover",
            null,
            moduleOverride: ProductIdentityRecoveryOperatorEntitlementGrantProfile.ModuleCode,
            scope: scope);
}
