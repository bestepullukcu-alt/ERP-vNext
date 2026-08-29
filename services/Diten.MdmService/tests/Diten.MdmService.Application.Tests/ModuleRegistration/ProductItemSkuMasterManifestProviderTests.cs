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
        Assert.Equal("MASTER-DATA-MANAGEMENT", Manifest.Domain);
        Assert.Equal("DitenMdmService", Manifest.Service);
        Assert.True(Manifest.IsTenantAssignable);
        Assert.False(Manifest.IsBaseline);

        Assert.Equal(7, Manifest.Pages.Count);
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
        var productScopes = Assert.Single(Manifest.Pages, page => page.PageCode == "PRODUCT_LEGAL_ENTITY_SCOPES");
        Assert.Equal("/MasterDataManagement/ProductLegalEntityScopes", productScopes.RoutePath);
        Assert.Equal("mdm.product-legal-entity-scopes.read", productScopes.RequiredPermission);
        Assert.False(productScopes.IsNavigationVisible);
        var brands = Assert.Single(Manifest.Pages, page => page.PageCode == "BRANDS");
        Assert.Equal("/MasterData/Brands", brands.RoutePath);
        Assert.Equal("mdm.brands.read", brands.RequiredPermission);
        Assert.True(brands.IsNavigationVisible);
    }

    [Fact]
    public void Declares_only_permissions_enforced_by_both_product_item_sku_master_controllers()
    {
        const string prefix = "Permission:";
        var policyProperty = typeof(HasPermissionAttribute).GetProperty("Policy");
        var enforced = new[] { typeof(GlobalProductsController), typeof(FinishedGoodsController), typeof(GskusController), typeof(LskusController), typeof(ProductAbbreviationsController), typeof(ProductLegalEntityScopesController), typeof(BrandsController) }
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
                "mdm.brands.read",
                "mdm.finished-goods.create",
                "mdm.finished-goods.read",
                "mdm.global-products.create",
                "mdm.global-products.read",
                "mdm.global-products.retire",
                "mdm.global-products.submit",
                "mdm.gskus.create",
                "mdm.gskus.read",
                "mdm.gskus.retire",
                "mdm.gskus.submit",
                "mdm.lskus.create",
                "mdm.lskus.read",
                "mdm.product-abbreviations.approve",
                "mdm.product-abbreviations.audit",
                "mdm.product-abbreviations.cancel",
                "mdm.product-abbreviations.correct",
                "mdm.product-abbreviations.read",
                "mdm.product-abbreviations.reject",
                "mdm.product-abbreviations.request",
                "mdm.product-abbreviations.retire",
                "mdm.product-legal-entity-scope-rollout.activate",
                "mdm.product-legal-entity-scope-rollout.rollback",
                "mdm.product-legal-entity-scopes.configure",
                "mdm.product-legal-entity-scopes.end",
                "mdm.product-legal-entity-scopes.read",
                "mdm.product-legal-entity-scopes.replace"
            },
            declared.OrderBy(value => value, StringComparer.Ordinal));
        Assert.Equal(27, declared.Count);
        var operatorOnly = new HashSet<string>(StringComparer.Ordinal)
        {
            "mdm.product-legal-entity-scope-rollout.activate",
            "mdm.product-legal-entity-scope-rollout.rollback"
        };
        Assert.True(declared.Where(permission => !operatorOnly.Contains(permission)).ToHashSet(StringComparer.Ordinal).IsSubsetOf(enforced));
    }

    [Fact]
    public void Existing_product_pages_keep_exact_create_and_quick_view_actions()
    {
        var productPages = Manifest.Pages.Where(page => page.PageCode is "GLOBAL_PRODUCTS" or "FINISHED_GOODS" or "GSKUS" or "LSKUS").ToList();
        Assert.Equal(4, productPages.Count);
        foreach (var page in productPages)
        {
            var expectedActions = page.PageCode is "GLOBAL_PRODUCTS" or "GSKUS"
                ? new[] { "ADD_NEW", "RETIRE", "SUBMIT", "VIEW_DETAILS" }
                : ["ADD_NEW", "VIEW_DETAILS"];
            Assert.Equal(expectedActions.Length, page.Actions.Count);
            Assert.Equal(
                expectedActions,
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
    public void Global_product_declares_submit_and_direct_retire_without_approve_or_reject()
    {
        var page = Assert.Single(Manifest.Pages, item => item.PageCode == "GLOBAL_PRODUCTS");
        var submit = Assert.Single(page.Actions, action => action.ActionCode == "SUBMIT");
        var retire = Assert.Single(page.Actions, action => action.ActionCode == "RETIRE");

        Assert.Equal("mdm.global-products.submit", submit.PermissionKey);
        Assert.True(submit.IsRowAction);
        Assert.False(submit.IsToolbarAction);
        Assert.False(submit.IsDangerous);
        Assert.Equal("mdm.global-products.retire", retire.PermissionKey);
        Assert.True(retire.IsRowAction);
        Assert.False(retire.IsToolbarAction);
        Assert.True(retire.IsDangerous);
        Assert.DoesNotContain(page.Actions, action => action.ActionCode is "APPROVE" or "REJECT");
        Assert.True(page.IsNavigationVisible);
    }

    [Fact]
    public void Gsku_declares_submit_and_direct_retire_without_approve_reject_or_navigation_change()
    {
        var page = Assert.Single(Manifest.Pages, item => item.PageCode == "GSKUS");
        var submit = Assert.Single(page.Actions, action => action.ActionCode == "SUBMIT");
        var retire = Assert.Single(page.Actions, action => action.ActionCode == "RETIRE");

        Assert.Equal("mdm.gskus.submit", submit.PermissionKey);
        Assert.True(submit.IsRowAction);
        Assert.False(submit.IsToolbarAction);
        Assert.False(submit.IsDangerous);
        Assert.Equal("mdm.gskus.retire", retire.PermissionKey);
        Assert.True(retire.IsRowAction);
        Assert.False(retire.IsToolbarAction);
        Assert.True(retire.IsDangerous);
        Assert.DoesNotContain(page.Actions, action => action.ActionCode is "APPROVE" or "REJECT");
        Assert.True(page.IsNavigationVisible);
    }

    [Fact]
    public void Product_scope_page_is_navigation_hidden_and_declares_exact_six_permissions()
    {
        var page = Assert.Single(Manifest.Pages, item => item.PageCode == "PRODUCT_LEGAL_ENTITY_SCOPES");
        Assert.False(page.IsNavigationVisible);
        Assert.Equal(6, page.Actions.Count);
        Assert.Equal(
            [
                "mdm.product-legal-entity-scope-rollout.activate",
                "mdm.product-legal-entity-scope-rollout.rollback",
                "mdm.product-legal-entity-scopes.configure",
                "mdm.product-legal-entity-scopes.end",
                "mdm.product-legal-entity-scopes.read",
                "mdm.product-legal-entity-scopes.replace"
            ],
            page.Actions.Select(action => action.PermissionKey).OrderBy(value => value, StringComparer.Ordinal));
        Assert.DoesNotContain(page.Actions, action => action.ActionCode.Contains("DELETE", StringComparison.Ordinal));
        Assert.DoesNotContain(page.Actions, action => action.ActionCode.Contains("WORKFLOW", StringComparison.Ordinal));
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
