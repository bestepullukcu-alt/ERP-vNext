using System.Reflection;
using Diten.MdmService.Api.Controllers;
using Diten.MdmService.Api.ModuleRegistration;
using Diten.MdmService.Infrastructure.Authorization;
using Xunit;

namespace Diten.MdmService.Application.Tests.ModuleRegistration;

/// <summary>
/// WP-MDM-BRAND-ACCEPT-01 FIX2 (item 1, CT model A) — Brand and Product master as their own self-registered module. The
/// declared keys are exactly the keys the Brand / Product controllers enforce, and no other module declares them.
/// </summary>
public sealed class BrandProductMasterManifestProviderTests
{
    private static readonly Diten.BuildingBlocks.ModuleRegistration.Abstractions.ModuleManifestDocument Manifest =
        new BrandProductMasterManifestProvider().GetManifest();

    [Fact]
    public void Declares_its_own_tenant_assignable_module_with_the_brands_page_in_navigation()
    {
        Assert.Equal("brand-product-master", Manifest.ModuleCode);
        Assert.Equal("MASTER-DATA-MANAGEMENT", Manifest.Domain);
        Assert.Equal("DitenMdmService", Manifest.Service);
        Assert.True(Manifest.IsTenantAssignable);
        Assert.False(Manifest.IsBaseline);

        var brands = Assert.Single(Manifest.Pages, page => page.PageCode == "BRANDS");
        Assert.Equal("/MasterData/Brands", brands.RoutePath);
        Assert.Equal("mdm.brands.read", brands.RequiredPermission);
        Assert.True(brands.IsNavigationVisible);
        Assert.Null(brands.PermissionOwnerModuleCode);
        var products = Assert.Single(Manifest.Pages, page => page.PageCode == "PRODUCTS");
        Assert.Equal("/MasterData/Products", products.RoutePath);
        Assert.False(products.IsNavigationVisible);
    }

    [Fact]
    public void Declares_exactly_the_keys_the_brand_and_product_controllers_enforce_including_export()
    {
        const string prefix = "Permission:";
        var policyProperty = typeof(HasPermissionAttribute).GetProperty("Policy");
        var enforced = new[] { typeof(BrandsController), typeof(ProductsController) }
            .SelectMany(controller => controller.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .SelectMany(method => method.GetCustomAttributes<HasPermissionAttribute>())
            .Select(attribute => policyProperty?.GetValue(attribute) as string ?? string.Empty)
            .Where(policy => policy.StartsWith(prefix, StringComparison.Ordinal))
            .SelectMany(policy => policy[prefix.Length..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .ToHashSet(StringComparer.Ordinal);

        var declared = Manifest.Pages
            .SelectMany(page => page.Actions.Select(action => action.PermissionKey).Append(page.RequiredPermission))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(
            new[]
            {
                "mdm.brands.archive", "mdm.brands.create", "mdm.brands.export", "mdm.brands.read", "mdm.brands.update",
                "mdm.products.archive", "mdm.products.create", "mdm.products.read", "mdm.products.update"
            },
            declared.OrderBy(x => x, StringComparer.Ordinal));
        Assert.True(declared.SetEquals(enforced), $"declared [{string.Join(", ", declared)}] vs enforced [{string.Join(", ", enforced)}]");
    }

    [Fact]
    public void No_other_mdm_manifest_declares_a_brand_or_product_master_key()
    {
        var others = new Diten.BuildingBlocks.ModuleRegistration.Abstractions.ModuleManifestDocument[]
        {
            new ProductItemSkuMasterManifestProvider().GetManifest(),
            new LegalEntityManifestProvider().GetManifest()
        };

        var foreign = others
            .SelectMany(manifest => manifest.Pages)
            .SelectMany(page => page.Actions.Select(action => action.PermissionKey).Append(page.RequiredPermission))
            .Where(key => key.StartsWith("mdm.brands.", StringComparison.Ordinal) || key.StartsWith("mdm.products.", StringComparison.Ordinal))
            .ToList();

        Assert.Empty(foreign);
    }
}
