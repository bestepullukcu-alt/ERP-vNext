using Diten.AuthService.Application.Common.Services;
using Xunit;

namespace Diten.AuthService.Application.Tests.Roles;

public sealed class ProductAbbreviationEntitlementGrantProfileTests
{
    [Fact]
    public void Declares_exact_eight_keys_and_four_responsibility_role_matrices()
    {
        Assert.Equal(8, ProductAbbreviationEntitlementGrantProfile.PermissionKeys.Count);
        Assert.Equal(
            [
                "mdm.product-abbreviations.approve",
                "mdm.product-abbreviations.audit",
                "mdm.product-abbreviations.cancel",
                "mdm.product-abbreviations.correct",
                "mdm.product-abbreviations.read",
                "mdm.product-abbreviations.reject",
                "mdm.product-abbreviations.request",
                "mdm.product-abbreviations.retire"
            ],
            ProductAbbreviationEntitlementGrantProfile.PermissionKeys.OrderBy(key => key, StringComparer.Ordinal));

        AssertRole(
            ProductAbbreviationEntitlementGrantProfile.RequesterRole,
            ["mdm.product-abbreviations.cancel", "mdm.product-abbreviations.read", "mdm.product-abbreviations.request"]);
        AssertRole(
            ProductAbbreviationEntitlementGrantProfile.StewardRole,
            ["mdm.product-abbreviations.cancel", "mdm.product-abbreviations.correct", "mdm.product-abbreviations.read", "mdm.product-abbreviations.request", "mdm.product-abbreviations.retire"]);
        AssertRole(
            ProductAbbreviationEntitlementGrantProfile.ApproverRole,
            ["mdm.product-abbreviations.approve", "mdm.product-abbreviations.read", "mdm.product-abbreviations.reject"]);
        AssertRole(
            ProductAbbreviationEntitlementGrantProfile.AuditorRole,
            ["mdm.product-abbreviations.audit", "mdm.product-abbreviations.read"]);
    }

    [Theory]
    [InlineData("mdm.product-abbreviations.allocate")]
    [InlineData("mdm.product-abbreviations.cancel-own")]
    [InlineData("mdm.product-abbreviations.cancel-managed")]
    public void Exact_validation_rejects_forbidden_or_extra_alias(string alias)
    {
        var keys = ProductAbbreviationEntitlementGrantProfile.PermissionKeys.Append(alias);

        var exception = Assert.Throws<InvalidOperationException>(
            () => ProductAbbreviationEntitlementGrantProfile.ValidateExactPermissionSet(keys));

        Assert.Contains("exact eight-key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Exact_validation_rejects_partial_set()
    {
        var keys = ProductAbbreviationEntitlementGrantProfile.PermissionKeys
            .Where(key => key != ProductAbbreviationEntitlementGrantProfile.Audit);

        Assert.Throws<InvalidOperationException>(
            () => ProductAbbreviationEntitlementGrantProfile.ValidateExactPermissionSet(keys));
    }

    private static void AssertRole(string roleName, string[] expectedPermissions)
    {
        var role = Assert.Single(
            ProductAbbreviationEntitlementGrantProfile.DedicatedRoles,
            template => template.RoleName == roleName);
        Assert.Equal(
            expectedPermissions,
            role.PermissionKeys.OrderBy(key => key, StringComparer.Ordinal));
    }
}
