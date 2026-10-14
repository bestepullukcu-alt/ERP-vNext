using System.Xml.Linq;
using Xunit;

namespace Diten.Web.Tests.Controllers;

/// <summary>
/// WP-VW-W3E · W3E-BE-a (§2.7) — the CRM manifest page <c>CRM_VISIT_QR_CODES</c> (/CRM/VisitQrCodes) has its menu label
/// in all seven languages (the nav key strips the underscores: <c>Nav.Page.CRMVISITQRCODES</c>), never an echo of the key,
/// and the Turkish label is Turkish. The page itself comes with W3E-WEB-a.
/// </summary>
public sealed class VwW3EVisitQrNavWebTests
{
    private const string Key = "Nav.Page.CRMVISITQRCODES";

    [Fact]
    public void The_visit_qr_page_label_exists_in_seven_languages_without_echo()
    {
        var labels = new Dictionary<string, string>();
        foreach (var culture in new[] { "en", "tr", "fr", "es", "zh", "ar", "ru" })
        {
            var shared = XDocument.Load(Path.Combine(WebRoot(), "Resources", $"SharedResource.{culture}.resx")).Root!
                .Elements("data").Where(d => (string)d.Attribute("name")! == Key).ToList();
            var entry = Assert.Single(shared);
            var value = entry.Element("value")?.Value ?? string.Empty;
            Assert.False(string.IsNullOrWhiteSpace(value), culture);
            Assert.NotEqual(Key, value);
            Assert.DoesNotContain("CRMVISITQRCODES", value);
            labels[culture] = value;
        }

        Assert.Equal("Ziyaret QR Kodları", labels["tr"]);
        Assert.Equal("Visit QR Codes", labels["en"]);
        Assert.Equal(7, labels.Values.Distinct().Count());

        var manifest = File.ReadAllText(Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(WebRoot())!)!,
            "services", "Diten.Platform", "src", "Diten.Platform.Application", "Features", "Crm", "SelfRegistration", "CrmManifestProvider.cs"));
        Assert.Contains("new ModuleManifestPage(\"CRM_VISIT_QR_CODES\", \"Visit QR Codes\", \"/CRM/VisitQrCodes\", \"crm.visit-qr.read\"", manifest);
    }

    private static string WebRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Diten.Web.csproj"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("repo root not found"), "frontend", "Diten.Web");
    }
}
