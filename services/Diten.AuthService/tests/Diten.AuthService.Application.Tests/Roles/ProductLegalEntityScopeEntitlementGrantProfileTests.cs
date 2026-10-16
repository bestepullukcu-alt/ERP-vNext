using Diten.AuthService.Application.Common.Services;
using Diten.AuthService.Domain.Authorization;
using Diten.AuthService.Domain.Entities;
using Xunit;

namespace Diten.AuthService.Application.Tests.Roles;

public sealed class ProductLegalEntityScopeEntitlementGrantProfileTests
{
    [Fact]
    public void Declares_exact_six_keys_and_three_dedicated_role_matrices()
    {
        Assert.Equal(6, ProductLegalEntityScopeEntitlementGrantProfile.PermissionKeys.Count);
        Assert.Equal(
            [
                "mdm.product-legal-entity-scope-rollout.activate",
                "mdm.product-legal-entity-scope-rollout.rollback",
                "mdm.product-legal-entity-scopes.configure",
                "mdm.product-legal-entity-scopes.end",
                "mdm.product-legal-entity-scopes.read",
                "mdm.product-legal-entity-scopes.replace"
            ],
            ProductLegalEntityScopeEntitlementGrantProfile.PermissionKeys
                .OrderBy(key => key, StringComparer.Ordinal));

        AssertRole(
            ProductLegalEntityScopeEntitlementGrantProfile.StewardRole,
            [
                ProductLegalEntityScopeEntitlementGrantProfile.Configure,
                ProductLegalEntityScopeEntitlementGrantProfile.End,
                ProductLegalEntityScopeEntitlementGrantProfile.Read,
                ProductLegalEntityScopeEntitlementGrantProfile.Replace
            ]);
        AssertRole(
            ProductLegalEntityScopeEntitlementGrantProfile.AuditorRole,
            [ProductLegalEntityScopeEntitlementGrantProfile.Read]);
        AssertRole(
            ProductLegalEntityScopeEntitlementGrantProfile.RolloutOperatorRole,
            [
                ProductLegalEntityScopeEntitlementGrantProfile.Activate,
                ProductLegalEntityScopeEntitlementGrantProfile.Rollback,
                ProductLegalEntityScopeEntitlementGrantProfile.Read
            ]);
    }

    [Theory]
    [InlineData("mdm.product-legal-entity-scopes.delete")]
    [InlineData("mdm.product-legal-entity-scope-rollout.approve")]
    public void Exact_validation_rejects_forbidden_or_extra_key(string extra)
    {
        var keys = ProductLegalEntityScopeEntitlementGrantProfile.PermissionKeys.Append(extra);

        var exception = Assert.Throws<InvalidOperationException>(
            () => ProductLegalEntityScopeEntitlementGrantProfile.ValidateExactPermissionSet(keys));

        Assert.Contains("exact six-key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Exact_validation_rejects_partial_set()
    {
        var keys = ProductLegalEntityScopeEntitlementGrantProfile.PermissionKeys
            .Where(key => key != ProductLegalEntityScopeEntitlementGrantProfile.Rollback);

        Assert.Throws<InvalidOperationException>(
            () => ProductLegalEntityScopeEntitlementGrantProfile.ValidateExactPermissionSet(keys));
    }

    [Theory]
    [InlineData("module")]
    [InlineData("scope")]
    public void Definition_validation_rejects_catalog_attribution_drift(string variant)
    {
        var permissions = ProductLegalEntityScopeEntitlementGrantProfile.PermissionKeys
            .Select(PermissionFor)
            .ToList();
        var target = permissions.Single(permission =>
            permission.Key == ProductLegalEntityScopeEntitlementGrantProfile.Read);
        if (variant == "module")
        {
            target.SetModule("other-module");
        }
        else
        {
            target.SetScope(PermissionScope.PlatformAdmin);
        }

        Assert.Throws<InvalidOperationException>(() =>
            ProductLegalEntityScopeEntitlementGrantProfile.ValidateExactPermissionDefinitions(permissions));
    }

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

    private static void AssertRole(string roleName, string[] expectedPermissions)
    {
        var role = Assert.Single(
            ProductLegalEntityScopeEntitlementGrantProfile.DedicatedRoles,
            template => template.RoleName == roleName);
        Assert.Equal(
            expectedPermissions,
            role.PermissionKeys.OrderBy(key => key, StringComparer.Ordinal));
    }
}
