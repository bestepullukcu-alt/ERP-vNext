using System.Reflection;
using Diten.MdmService.Api.Controllers;
using Diten.MdmService.Api.ModuleRegistration;
using Diten.MdmService.Infrastructure.Authorization;
using Xunit;

namespace Diten.MdmService.Application.Tests.ModuleRegistration;

public sealed class ProductItemSkuMasterManifestProviderTests
{
    private static readonly Diten.BuildingBlocks.ModuleRegistration.Abstractions.ModuleManifestDocument Manifest =
        new ProductItemSkuMasterManifestProvider().GetManifest();

    [Fact]
    public void Declares_exact_identity_route_and_non_baseline_flags()
    {
        Assert.Equal("product-item-sku-master", Manifest.ModuleCode);
        Assert.Equal("ProductItemSkuMaster", Manifest.ModuleName);
        Assert.Equal("MasterDataManagement", Manifest.Domain);
        Assert.Equal("DitenMdmService", Manifest.Service);
        Assert.True(Manifest.IsTenantAssignable);
        Assert.False(Manifest.IsBaseline);

        Assert.Equal(5, Manifest.Pages.Count);
        var globalProducts = Assert.Single(Manifest.Pages, page => page.PageCode == "GLOBAL_PRODUCTS");
        Assert.Equal("/MasterDataManagement/GlobalProducts", globalProducts.RoutePath);
        Assert.Equal("mdm.global-products.read", globalProducts.RequiredPermission);
        var finishedGoods = Assert.Single(Manifest.Pages, page => page.PageCode == "FINISHED_GOODS");
        Assert.Equal("/MasterDataManagement/FinishedGoods", finishedGoods.RoutePath);
        Assert.Equal("mdm.finished-goods.read", finishedGoods.RequiredPermission);
        var gskus = Assert.Single(Manifest.Pages, page => page.PageCode == "GSKUS");
        Assert.Equal("/MasterDataManagement/Gskus", gskus.RoutePath);
        Assert.Equal("mdm.gskus.read", gskus.RequiredPermission);
        Assert.True(gskus.IsNavigationVisible);
        var lskus = Assert.Single(Manifest.Pages, page => page.PageCode == "LSKUS");
        Assert.Equal("/MasterDataManagement/Lskus", lskus.RoutePath);
        Assert.Equal("mdm.lskus.read", lskus.RequiredPermission);
        Assert.False(lskus.IsNavigationVisible);
        var productAbbreviations = Assert.Single(Manifest.Pages, page => page.PageCode == "PRODUCT_ABBREVIATIONS");
        Assert.Equal("/MDM/ProductAbbreviationRegister", productAbbreviations.RoutePath);
        Assert.Equal("mdm.product-abbreviations.read", productAbbreviations.RequiredPermission);
        Assert.False(productAbbreviations.IsNavigationVisible);
    }

    [Fact]
    public void Declares_only_permissions_enforced_by_both_product_item_sku_master_controllers()
    {
        const string prefix = "Permission:";
        var policyProperty = typeof(HasPermissionAttribute).GetProperty("Policy");
        var enforced = new[] { typeof(GlobalProductsController), typeof(FinishedGoodsController), typeof(GskusController), typeof(LskusController), typeof(ProductAbbreviationsController) }
            .SelectMany(controller => controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .SelectMany(method => method.GetCustomAttributes<HasPermissionAttribute>())
            .Select(attribute => policyProperty?.GetValue(attribute) as string ?? string.Empty)
            .Where(policy => policy.StartsWith(prefix, StringComparison.Ordinal))
            .Select(policy => policy[prefix.Length..])
            .ToHashSet(StringComparer.Ordinal);

        var declared = Manifest.Pages
            .SelectMany(page => page.Actions.Select(action => action.PermissionKey).Append(page.RequiredPermission))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(
            new[]
            {
                "mdm.finished-goods.create",
                "mdm.finished-goods.read",
                "mdm.global-products.create",
                "mdm.global-products.read",
                "mdm.gskus.create",
                "mdm.gskus.read",
                "mdm.lskus.create",
                "mdm.lskus.read",
                "mdm.product-abbreviations.approve",
                "mdm.product-abbreviations.audit",
                "mdm.product-abbreviations.cancel",
                "mdm.product-abbreviations.correct",
                "mdm.product-abbreviations.read",
                "mdm.product-abbreviations.reject",
                "mdm.product-abbreviations.request",
                "mdm.product-abbreviations.retire"
            },
            declared.OrderBy(value => value, StringComparer.Ordinal));
        Assert.Equal(16, declared.Count);
        Assert.True(declared.SetEquals(enforced));
    }

    [Fact]
    public void Existing_product_pages_keep_exact_create_and_quick_view_actions()
    {
        var productPages = Manifest.Pages.Where(page => page.PageCode != "PRODUCT_ABBREVIATIONS").ToList();
        Assert.Equal(4, productPages.Count);
        foreach (var page in productPages)
        {
            Assert.Equal(2, page.Actions.Count);
            Assert.Equal(
                ["ADD_NEW", "VIEW_DETAILS"],
                page.Actions.Select(action => action.ActionCode).OrderBy(value => value, StringComparer.Ordinal));
            var permissionPrefix = page.PageCode switch
            {
                "GLOBAL_PRODUCTS" => "mdm.global-products",
                "FINISHED_GOODS" => "mdm.finished-goods",
                "GSKUS" => "mdm.gskus",
                _ => "mdm.lskus"
            };
            var add = Assert.Single(page.Actions, action => action.ActionCode == "ADD_NEW");
            Assert.Equal(permissionPrefix + ".create", add.PermissionKey);
            Assert.True(add.IsToolbarAction);
            Assert.False(add.IsRowAction);
            Assert.False(add.IsDangerous);

            var details = Assert.Single(page.Actions, action => action.ActionCode == "VIEW_DETAILS");
            Assert.Equal(permissionPrefix + ".read", details.PermissionKey);
            Assert.True(details.IsRowAction);
            Assert.False(details.IsToolbarAction);
            Assert.False(details.IsDangerous);
        }
    }

    [Fact]
    public void Product_abbreviations_page_declares_exact_eight_permission_actions_and_no_forbidden_alias()
    {
        var page = Assert.Single(Manifest.Pages, item => item.PageCode == "PRODUCT_ABBREVIATIONS");
        Assert.Equal(8, page.Actions.Count);
        Assert.Equal(
            ["APPROVE", "CANCEL", "CORRECT", "REJECT", "REQUEST", "RETIRE", "VIEW_AUDIT", "VIEW_DETAILS"],
            page.Actions.Select(action => action.ActionCode).OrderBy(value => value, StringComparer.Ordinal));
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
            page.Actions.Select(action => action.PermissionKey).OrderBy(value => value, StringComparer.Ordinal));
        Assert.DoesNotContain(page.Actions, action => action.PermissionKey.EndsWith(".allocate", StringComparison.Ordinal));
        Assert.DoesNotContain(page.Actions, action => action.PermissionKey.EndsWith(".cancel-own", StringComparison.Ordinal));
        Assert.DoesNotContain(page.Actions, action => action.PermissionKey.EndsWith(".cancel-managed", StringComparison.Ordinal));
    }
}
