using Diten.Platform.Application.Features.ModulePages;
using Diten.Platform.Application.Features.ModulePages.Commands;
using Diten.Platform.Application.Features.ModulePages.Validators;
using Xunit;

namespace Diten.Platform.Application.Tests.ModuleCatalog;

public sealed class ModulePageDescriptorRulesTests
{
    [Theory]
    [InlineData(" project list ", "PROJECT_LIST")]
    [InlineData("project__list", "PROJECT_LIST")]
    [InlineData("PROJECT @ LIST", "PROJECT_LIST")]
    public void Normalize_page_code_returns_canonical_code(string input, string expected)
    {
        Assert.Equal(expected, ModulePageDescriptorNormalizer.NormalizePageCode(input));
    }

    [Theory]
    [InlineData("/PPM//Projects/", "/PPM/Projects")]
    [InlineData(" /Platform/Tenants/ ", "/Platform/Tenants")]
    public void Normalize_route_path_cleans_slashes_and_trailing_slash(string input, string expected)
    {
        Assert.Equal(expected, ModulePageDescriptorNormalizer.NormalizeRoutePath(input));
    }

    [Theory]
    [InlineData(" PPM.Projects..VIEW ", "ppm.projects.view")]
    [InlineData("ppm . projects . create", "ppm.projects.create")]
    public void Normalize_permission_returns_canonical_permission(string input, string expected)
    {
        Assert.Equal(expected, ModulePageDescriptorNormalizer.NormalizePermission(input));
    }

    [Theory]
    [InlineData("/PPM/Projects", "PPM.Projects.VIEW")]
    [InlineData("/PPM//Projects/", "ppm.projects..view")]
    [InlineData("/MDM/Legal-Entities", "mdm.legal-entities.view")]
    public void Create_validator_accepts_canonical_route_and_permission(string routePath, string permission)
    {
        var result = Validator().Validate(Command(routePath, permission));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("ppm/projects", "ppm.projects.view")]
    [InlineData("/workcenter/list", "ppm.projects.view")]
    [InlineData("/ppm projects", "ppm.projects.view")]
    [InlineData("/PPM/Projects/List", "ppm.projects.view")]
    [InlineData("/PPM/Projects", "ppm.projects")]
    [InlineData("/PPM/Projects", "ppm.projects.view.all")]
    [InlineData("/PPM/Projects", "ppm.projects.view!")]
    public void Create_validator_rejects_invalid_route_or_permission(string routePath, string permission)
    {
        var result = Validator().Validate(Command(routePath, permission));

        Assert.False(result.IsValid);
        Assert.DoesNotContain(result.Errors, error => error.ErrorMessage.Contains("regex", StringComparison.OrdinalIgnoreCase));
    }

    private static CreateModulePageDescriptorCommandValidator Validator() => new();

    private static CreateModulePageDescriptorCommand Command(string routePath, string permission) =>
        new(new CreateModulePageDescriptorRequest(
            "PPM",
            "PROJECT_LIST",
            "Project List",
            routePath,
            permission,
            "List",
            "Draft",
            0,
            null));

    [Theory]
    [InlineData("01585")]
    [InlineData("12_34")]
    public void Create_validator_rejects_numeric_only_page_code(string pageCode)
    {
        var result = Validator().Validate(new CreateModulePageDescriptorCommand(new CreateModulePageDescriptorRequest(
            "PPM",
            pageCode,
            "Project List",
            "/PPM/Projects",
            "ppm.projects.view",
            "List",
            "Draft",
            0,
            null)));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage == "Sayfa kodu otomatik oluşturulamadı.");
    }
}
