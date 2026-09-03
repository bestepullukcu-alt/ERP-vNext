using System.Reflection;
using Diten.MdmService.Api.Controllers;
using Diten.MdmService.Api.ModuleRegistration;
using Diten.MdmService.Infrastructure.Authorization;
using Xunit;

namespace Diten.MdmService.Application.Tests.ModuleRegistration;

public sealed class BrandProductMasterManifestProviderTests
{
    private static readonly Diten.BuildingBlocks.ModuleRegistration.Abstractions.ModuleManifestDocument Manifest =
        new BrandProductMasterManifestProvider().GetManifest();

    [Fact]
    public void Declares_exact_module_identity_pages_routes_and_flags()
    {
        Assert.Equal("brand-product-master", Manifest.ModuleCode);
        Assert.Equal("BrandProductMaster", Manifest.ModuleName);
        Assert.Equal("Brand / Product Master", Manifest.DisplayName);
        Assert.Equal("MASTER-DATA-MANAGEMENT", Manifest.Domain);
        Assert.Equal("DitenMdmService", Manifest.Service);
        Assert.True(Manifest.IsTenantAssignable);
        Assert.False(Manifest.IsBaseline);

        Assert.Equal(2, Manifest.Pages.Count);
        var brands = Assert.Single(Manifest.Pages, page => page.PageCode == "BRANDS");
        Assert.Equal("/MasterData/Brands", brands.RoutePath);
        Assert.Equal("mdm.brands.read", brands.RequiredPermission);
        Assert.True(brands.IsNavigationVisible);

        var products = Assert.Single(Manifest.Pages, page => page.PageCode == "PRODUCTS");
        Assert.Equal("/MasterData/Products", products.RoutePath);
        Assert.Equal("mdm.products.read", products.RequiredPermission);
        Assert.True(products.IsNavigationVisible);
    }

    [Fact]
    public void Declares_exact_eight_controller_permissions_without_cross_module_keys()
    {
        const string policyPrefix = "Permission:";
        var enforced = new[] { typeof(BrandsController), typeof(ProductsController) }
            .SelectMany(controller => controller
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .SelectMany(method => method.GetCustomAttributes<HasPermissionAttribute>())
            .Select(attribute => attribute.Policy)
            .Where(policy => policy?.StartsWith(policyPrefix, StringComparison.Ordinal) is true)
            .Select(policy => policy![policyPrefix.Length..])
            .ToHashSet(StringComparer.Ordinal);

        var declared = Manifest.Pages
            .SelectMany(page => page.Actions.Select(action => action.PermissionKey).Append(page.RequiredPermission))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(
            [
                "mdm.brands.archive",
                "mdm.brands.create",
                "mdm.brands.read",
                "mdm.brands.update",
                "mdm.products.archive",
                "mdm.products.create",
                "mdm.products.read",
                "mdm.products.update"
            ],
            declared.OrderBy(value => value, StringComparer.Ordinal));
        Assert.Equal(8, declared.Count);
        Assert.True(declared.SetEquals(enforced));
        Assert.All(declared, permission => Assert.True(
            permission.StartsWith("mdm.brands.", StringComparison.Ordinal)
            || permission.StartsWith("mdm.products.", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData("BRANDS", "mdm.brands")]
    [InlineData("PRODUCTS", "mdm.products")]
    public void Page_actions_mirror_the_real_toolbar_and_row_actions(string pageCode, string permissionPrefix)
    {
        var page = Assert.Single(Manifest.Pages, item => item.PageCode == pageCode);
        Assert.Equal(["ADD_NEW", "ARCHIVE", "EDIT", "VIEW_DETAILS"],
            page.Actions.Select(action => action.ActionCode).OrderBy(value => value, StringComparer.Ordinal));

        var add = Assert.Single(page.Actions, action => action.ActionCode == "ADD_NEW");
        Assert.Equal(permissionPrefix + ".create", add.PermissionKey);
        Assert.True(add.IsToolbarAction);
        Assert.False(add.IsRowAction);

        var details = Assert.Single(page.Actions, action => action.ActionCode == "VIEW_DETAILS");
        Assert.Equal(permissionPrefix + ".read", details.PermissionKey);
        Assert.True(details.IsRowAction);

        var edit = Assert.Single(page.Actions, action => action.ActionCode == "EDIT");
        Assert.Equal(permissionPrefix + ".update", edit.PermissionKey);
        Assert.True(edit.IsRowAction);

        var archive = Assert.Single(page.Actions, action => action.ActionCode == "ARCHIVE");
        Assert.Equal(permissionPrefix + ".archive", archive.PermissionKey);
        Assert.True(archive.IsRowAction);
        Assert.True(archive.IsDangerous);
        Assert.DoesNotContain(page.Actions, action => action.ActionCode.Contains("DELETE", StringComparison.Ordinal));
    }

    [Fact]
    public void Program_registers_the_dedicated_provider_exactly_once()
    {
        var source = ReadRepoFile("services/Diten.MdmService/src/Diten.MdmService.Api/Program.cs");
        const string registration = "AddSingleton<IModuleManifestProvider, BrandProductMasterManifestProvider>()";

        Assert.Equal(1, CountOrdinal(source, registration));
    }

    private static int CountOrdinal(string value, string fragment)
    {
        var count = 0;
        var offset = 0;
        while ((offset = value.IndexOf(fragment, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += fragment.Length;
        }
        return count;
    }

    private static string ReadRepoFile(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            {
                return File.ReadAllText(Path.Combine(
                    directory.FullName,
                    relativePath.Replace('/', Path.DirectorySeparatorChar)));
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("REPO_ROOT_NOT_FOUND");
    }
}
