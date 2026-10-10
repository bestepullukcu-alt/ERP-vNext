using System.ComponentModel.DataAnnotations;
using System.Xml.Linq;
using Diten.Web.Models.SupplyChain.Carriers;
using Xunit;

namespace Diten.Web.Tests.Forms;

public sealed class CarrierFormContractTests
{
    [Fact]
    public void CreateModel_PreservesContractValidWhitespaceDuplicatesAndEmptyReference()
    {
        var model = new CreateCarrierViewModel
        {
            CarrierCode = " ",
            DisplayName = "  ",
            SupportedModes = ["Road", "Road"],
            ExternalReference = string.Empty
        };

        Assert.Empty(Validate(model));
        Assert.Equal(["Road", "Road"], model.SupportedModes);
    }

    [Fact]
    public void StatusModel_AllowsRequiredEmptyReason()
    {
        var model = new ChangeCarrierStatusViewModel { TargetStatus = "Retired", ReasonCode = string.Empty };
        Assert.Empty(Validate(model));
    }

    [Fact]
    public void CreateRazor_HasExactlyFourBoundFieldsAndUasGatePrecedesPageSurface()
    {
        var root = FindRepoRoot();
        var form = File.ReadAllText(Path.Combine(root, "frontend/Diten.Web/Views/SupplyChain/Carriers/_CreateEditOffcanvas.cshtml"));
        var index = File.ReadAllText(Path.Combine(root, "frontend/Diten.Web/Views/SupplyChain/Carriers/Index.cshtml"));
        var fields = new[] { "CarrierCode", "DisplayName", "SupportedModes", "ExternalReference" };

        foreach (var field in fields) Assert.Contains($"asp-for=\"{field}\"", form);
        Assert.Equal(1, Count(form, "<input asp-for=\"CarrierCode\""));
        Assert.Equal(1, Count(form, "<input asp-for=\"DisplayName\""));
        Assert.Equal(1, Count(form, "<select asp-for=\"SupportedModes\""));
        Assert.Equal(1, Count(form, "<input asp-for=\"ExternalReference\""));
        Assert.DoesNotContain("TenantId", form);
        Assert.DoesNotContain("LegalEntityId", form);
        Assert.DoesNotContain("asp-for=\"Status\"", form);
        Assert.True(index.IndexOf("Perms.Has(\"supplychain.carriers.read\")", StringComparison.Ordinal)
            < index.IndexOf("<partial name=\"_Filter\"", StringComparison.Ordinal));
        Assert.Contains("<partial name=\"_AccessDenied\"", index);
        Assert.Contains("return;", index);
    }

    [Fact]
    public void AllSevenResourcesHaveIdenticalNonEmptyKeySets()
    {
        var directory = Path.Combine(FindRepoRoot(), "frontend/Diten.Web/Resources/Views/SupplyChain/Carriers");
        var files = new[] { "en", "tr", "fr", "es", "zh", "ar", "ru" }
            .Select(language => Path.Combine(directory, $"CarriersIndex.{language}.resx")).ToArray();
        var keySets = files.Select(file => XDocument.Load(file).Root!.Elements("data")
            .ToDictionary(element => element.Attribute("name")!.Value,
                element => element.Element("value")?.Value ?? string.Empty)).ToArray();

        Assert.All(keySets, values => Assert.All(values.Values, value => Assert.False(string.IsNullOrWhiteSpace(value))));
        Assert.All(keySets.Skip(1), values => Assert.Equal(keySets[0].Keys.Order(), values.Keys.Order()));
    }

    private static List<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results;
    }

    private static int Count(string text, string value) =>
        text.Split(value, StringSplitOptions.None).Length - 1;

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
