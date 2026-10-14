using System.Globalization;
using System.Resources;
using System.Xml.Linq;
using Diten.MdmService.Api.Controllers;
using Xunit;

namespace Diten.MdmService.Api.Tests;

/// <summary>
/// WP-MDM-BRAND-ACCEPT-01 FIX2 (item 1, BL-452) — THE FILE IS THE SCREEN: every word the brand export writes (headers,
/// statuses, yes / no, "could not be resolved") is the word the brand register shows, in each of the seven tenant languages.
/// The export's resx (MDM) is compared with the screen's resx (web) value by value.
/// </summary>
public sealed class BrandExportWordsGuardTests
{
    private static readonly string[] Keys =
    [
        "BrandCode", "BrandName", "BrandStatus", "BusinessUnitId", "TherapeuticAreaId", "EffectiveFrom", "EffectiveTo",
        "Archived", "UpdatedAt", "Status_draft", "Status_active", "Status_inactive", "Status_archived", "Yes", "No", "Unresolved"
    ];

    private static readonly ResourceManager Export =
        new("Diten.MdmService.Api.Resources.BrandExport", typeof(BrandsController).Assembly);

    [Theory]
    [InlineData("en")]
    [InlineData("tr")]
    [InlineData("fr")]
    [InlineData("es")]
    [InlineData("zh")]
    [InlineData("ar")]
    [InlineData("ru")]
    public void Every_export_word_is_the_word_the_brand_screen_shows(string language)
    {
        var screen = XDocument.Load(Path.Combine(RepositoryRoot(),
                "frontend", "Diten.Web", "Resources", "Views", "MasterData", "Brands", $"BrandsIndex.{language}.resx"))
            .Root!.Elements("data")
            .ToDictionary(x => (string)x.Attribute("name")!, x => (string)x.Element("value")!);
        var culture = CultureInfo.GetCultureInfo(language);

        Assert.All(Keys, key => Assert.Equal(screen[key], Export.GetString(key, culture)));
        Assert.False(string.IsNullOrWhiteSpace(Export.GetString("Title", culture)));
        if (language != "en")
        {
            Assert.NotEqual(Export.GetString("Title", CultureInfo.InvariantCulture), Export.GetString("Title", culture));
        }
    }

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web")) && Directory.Exists(Path.Combine(dir.FullName, "services")))
                return dir.FullName;
        }

        throw new InvalidOperationException("Repository root not found above the test output.");
    }
}
