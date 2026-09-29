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
        var brands = Assert.Single(Manifest.Pages, page => page.PageCode == "BRANDS");
        Assert.Equal("/MasterData/Brands", brands.RoutePath);
        Assert.Equal("mdm.brands.read", brands.RequiredPermission);
        Assert.Equal("brand-product-master", brands.PermissionOwnerModuleCode);
        Assert.True(brands.IsNavigationVisible);
        var productScopes = Assert.Single(Manifest.Pages, page => page.PageCode == "PRODUCT_LEGAL_ENTITY_SCOPES");
        Assert.Equal("/MasterDataManagement/ProductLegalEntityScopes", productScopes.RoutePath);
        Assert.Equal("mdm.product-legal-entity-scopes.read", productScopes.RequiredPermission);
        Assert.False(productScopes.IsNavigationVisible);
    }

    [Fact]
    public void Declares_exact_unique_canonical_permission_contract_and_matches_runtime_enforcement()
    {
        const string prefix = "Permission:";
        var policyProperty = typeof(HasPermissionAttribute).GetProperty("Policy");
        var enforced = new[] { typeof(GlobalProductsController), typeof(FinishedGoodsController), typeof(GskusController), typeof(LskusController), typeof(ProductAbbreviationsController), typeof(ProductLegalEntityScopesController) }
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
                "mdm.finished-goods.retire",
                "mdm.finished-goods.submit",
                "mdm.global-products.create",
                "mdm.global-products.read",
                "mdm.global-products.request-correction",
                "mdm.global-products.request-retirement",
                "mdm.global-products.retire",
                "mdm.global-products.submit",
                "mdm.global-products.update",
                "mdm.global-products.withdraw",
                "mdm.gskus.create",
                "mdm.gskus.read",
                "mdm.gskus.request-correction",
                "mdm.gskus.request-retirement",
                "mdm.gskus.retire",
                "mdm.gskus.submit",
                "mdm.gskus.update",
                "mdm.gskus.withdraw",
                "mdm.lskus.create",
                "mdm.lskus.read",
                "mdm.lskus.request-retirement",
                "mdm.lskus.retire",
                "mdm.lskus.submit",
                "mdm.lskus.withdraw",
                "mdm.product-abbreviations.approve",
                "mdm.product-abbreviations.audit",
                "mdm.product-abbreviations.cancel",
                "mdm.product-abbreviations.correct",
                "mdm.product-abbreviations.read",
                "mdm.product-abbreviations.reject",
                "mdm.product-abbreviations.request",
                "mdm.product-abbreviations.retire",
                "mdm.product-identity.lifecycle-operations.recover",
                "mdm.product-legal-entity-scope-rollout.activate",
                "mdm.product-legal-entity-scope-rollout.rollback",
                "mdm.product-legal-entity-scopes.configure",
                "mdm.product-legal-entity-scopes.end",
                "mdm.product-legal-entity-scopes.read",
                "mdm.product-legal-entity-scopes.replace"
            },
            declared.OrderBy(value => value, StringComparer.Ordinal));
        Assert.Equal(42, declared.Count);
        Assert.All(declared, permission =>
        {
            Assert.Equal(permission.Trim(), permission);
            Assert.Equal(permission.ToLowerInvariant(), permission);
            Assert.StartsWith("mdm.", permission, StringComparison.Ordinal);
            Assert.DoesNotContain("..", permission, StringComparison.Ordinal);
        });
        var nonControllerPermissions = new HashSet<string>(StringComparer.Ordinal)
        {
            "mdm.brands.read",
            "mdm.finished-goods.retire",
            "mdm.finished-goods.submit",
            "mdm.product-legal-entity-scope-rollout.activate",
            "mdm.product-legal-entity-scope-rollout.rollback",
            "mdm.product-identity.lifecycle-operations.recover"
        };
        Assert.True(declared.Where(permission => !nonControllerPermissions.Contains(permission)).ToHashSet(StringComparer.Ordinal).SetEquals(enforced));
    }

    [Fact]
    public void Global_product_declares_exact_lifecycle_actions_without_approval_or_rejection_actions()
    {
        var page = Assert.Single(Manifest.Pages, item => item.PageCode == "GLOBAL_PRODUCTS");
        Assert.Equal(
            ["ADD_NEW", "EDIT", "RECOVER_ORPHANED_LIFECYCLE_OPERATION", "REQUEST_CORRECTION", "REQUEST_RETIREMENT", "RETIRE", "SUBMIT", "VIEW_DETAILS", "WITHDRAW_APPROVAL"],
            page.Actions.Select(action => action.ActionCode).OrderBy(value => value, StringComparer.Ordinal));
        Assert.Equal(
            [
                "mdm.global-products.create",
                "mdm.global-products.read",
                "mdm.global-products.request-correction",
                "mdm.global-products.request-retirement",
                "mdm.global-products.retire",
                "mdm.global-products.submit",
                "mdm.global-products.update",
                "mdm.global-products.withdraw",
                "mdm.product-identity.lifecycle-operations.recover"
            ],
            page.Actions.Select(action => action.PermissionKey).OrderBy(value => value, StringComparer.Ordinal));
        Assert.True(Assert.Single(page.Actions, action => action.ActionCode == "EDIT").IsRowAction);
        Assert.True(Assert.Single(page.Actions, action => action.ActionCode == "SUBMIT").IsRowAction);
        Assert.True(Assert.Single(page.Actions, action => action.ActionCode == "WITHDRAW_APPROVAL").IsRowAction);
        var correction = Assert.Single(page.Actions, action => action.ActionCode == "REQUEST_CORRECTION");
        Assert.True(correction.IsRowAction);
        Assert.False(correction.IsDangerous);
        var requestRetirement = Assert.Single(page.Actions, action => action.ActionCode == "REQUEST_RETIREMENT");
        Assert.True(requestRetirement.IsRowAction);
        Assert.True(requestRetirement.IsDangerous);
        var retire = Assert.Single(page.Actions, action => action.ActionCode == "RETIRE");
        Assert.Equal("System", retire.ActionType);
        Assert.True(retire.IsDangerous);
        Assert.False(retire.IsRowAction);
        Assert.False(retire.IsToolbarAction);
        Assert.DoesNotContain(page.Actions, action => action.ActionCode is "APPROVE" or "REJECT");
    }

    [Fact]
    public void Recovery_is_an_exact_non_interactive_system_descriptor()
    {
        var recovery = Assert.Single(
            Manifest.Pages.SelectMany(page => page.Actions),
            action => action.PermissionKey == "mdm.product-identity.lifecycle-operations.recover");

        Assert.Equal("RECOVER_ORPHANED_LIFECYCLE_OPERATION", recovery.ActionCode);
        Assert.Equal("System", recovery.ActionType);
        Assert.True(recovery.IsDangerous);
        Assert.False(recovery.IsToolbarAction);
        Assert.False(recovery.IsRowAction);
    }

    [Fact]
    public void Gsku_has_exact_lifecycle_actions_and_hidden_system_retire()
    {
        var page = Assert.Single(Manifest.Pages, p => p.PageCode == "GSKUS");
        Assert.Equal(["ADD_NEW", "VIEW_DETAILS", "EDIT", "SUBMIT", "WITHDRAW_APPROVAL",
            "REQUEST_CORRECTION", "REQUEST_RETIREMENT", "RETIRE"], page.Actions.Select(a => a.ActionCode));
        var retire = Assert.Single(page.Actions, a => a.ActionCode == "RETIRE");
        Assert.False(retire.IsRowAction);
        Assert.False(retire.IsToolbarAction);
        Assert.Equal("mdm.gskus.retire", retire.PermissionKey);
    }

    [Fact]
    public void Finished_good_keeps_the_approved_four_key_contract_without_new_aliases()
    {
        var productPages = Manifest.Pages.Where(page => page.PageCode is "FINISHED_GOODS").ToList();
        Assert.Single(productPages);
        foreach (var page in productPages)
        {
            Assert.Equal(4, page.Actions.Count);
            Assert.Equal(
                ["ADD_NEW", "RETIRE", "SUBMIT", "VIEW_DETAILS"],
                page.Actions.Select(action => action.ActionCode).OrderBy(value => value, StringComparer.Ordinal));
            Assert.Equal(
                [
                    "mdm.finished-goods.create",
                    "mdm.finished-goods.read",
                    "mdm.finished-goods.retire",
                    "mdm.finished-goods.submit"
                ],
                page.Actions.Select(action => action.PermissionKey).OrderBy(value => value, StringComparer.Ordinal));
        }
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
