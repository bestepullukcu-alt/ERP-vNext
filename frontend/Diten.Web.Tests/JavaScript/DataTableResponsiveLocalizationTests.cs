using System.Xml.Linq;
using Xunit;

namespace Diten.Web.Tests.JavaScript;

public sealed class DataTableResponsiveLocalizationTests
{
    [Fact]
    public void Responsive_modal_chrome_is_shared_and_complete_in_all_tenant_languages()
    {
        var root = Root();
        var partial = File.ReadAllText(Path.Combine(root, "frontend/Diten.Web/Views/Shared/_DataTableL10n.cshtml"));
        var script = File.ReadAllText(Path.Combine(root, "frontend/Diten.Web/wwwroot/assets/js/dt-defaults.js"));

        Assert.Contains("\"Details\", \"Close\"", partial);
        Assert.Contains("dtText('Details')", script);
        Assert.Contains("dtText('Close')", script);
        Assert.Contains(".btn-close[data-bs-dismiss=\"modal\"]", script);

        foreach (var culture in new[] { "en", "tr", "fr", "es", "zh", "ar", "ru" })
        {
            var resource = XDocument.Load(Path.Combine(root, $"frontend/Diten.Web/Resources/SharedResource.{culture}.resx"));
            foreach (var key in new[] { "Details", "Close" })
            {
                var value = resource.Root?.Elements("data")
                    .SingleOrDefault(node => string.Equals((string?)node.Attribute("name"), key, StringComparison.Ordinal))
                    ?.Element("value")?.Value;
                Assert.False(string.IsNullOrWhiteSpace(value), $"{culture}:{key} must be present and non-empty");
            }
        }
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
