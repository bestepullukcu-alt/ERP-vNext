using System.Xml.Linq;
using Xunit;

namespace Diten.Platform.Application.Tests.Navigation;

// FEAT-NAV-L10N — the tenant nav is localized generically by stable code (Nav.Domain.{code}/Nav.Module.{code}/
// Nav.Page.{code}) in the frontend. This guard keeps the CURRENT nav set's keys complete across all 7 tenant
// languages (a missing key fails the build) and pins the frontend localizer's precedence contract. The lookup
// itself stays generic — this only asserts today's shipped data is present. Future self-registered modules add
// their own keys via the same l10n gate (see module-self-registration-standard.md).
public sealed class NavL10nContractTests
{
    private static readonly string[] SupportedLanguages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    // The current nav-visible set, written in a REALISTIC emitted form (manifest domain is PascalCase; the running
    // catalog normalizes module codes to UPPERCASE-with-dashes via ModuleCatalogCodeNormalizer; page codes carry
    // underscores). The lookup is casing/separator-immune, so the test derives keys through the SAME Normalize()
    // the runtime uses — a future casing/separator drift can't pass the test while failing at runtime.
    private static readonly string[] DomainCodes =
    [
        "Administration", "DevEnablement", "DocumentManagement", "MasterDataManagement", "PlatformSharedServices"
    ];

    private static readonly string[] ModuleCodes =
    [
        "ACCESS-GOVERNANCE", "TENANT-SETTINGS", "GOLDENSLIM", "GOLDENCOMPACT",
        "DOCUMENT-MANAGEMENT", "LEGAL-ENTITY", "REFERENCE-DATA", "ORGANIZATION", "WORKFLOW"
    ];

    private static readonly string[] PageCodes =
    [
        "USERS", "ROLES", "ROLE_PERMISSIONS", "USER_ROLES", "SECURITY_SETTINGS", "MENU_SETTINGS", "RECORDS",
        "QMS_BASELINES", "INSTANCES", "CONTROLLED_DOCUMENTS", "TEMPLATE_MASTERS", "TEMPLATE_VARIANTS",
        "ACCESS_MATRIX", "LEGAL_ENTITIES", "RD_SETS", "ORGANIZATION_UNITS", "POSITIONS", "POSITION_ASSIGNMENTS",
        "DEFINITIONS"
    ];

    private static IEnumerable<string> AllExpectedKeys() =>
        DomainCodes.Select(c => "Nav.Domain." + Normalize(c))
            .Concat(ModuleCodes.Select(c => "Nav.Module." + Normalize(c)))
            .Concat(PageCodes.Select(c => "Nav.Page." + Normalize(c)));

    // Must stay identical to NavNameLocalizer.Normalize (frontend): uppercase + strip every non-alphanumeric char.
    private static string Normalize(string code) =>
        new string(code.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    [Fact]
    public void Every_current_nav_code_has_a_key_in_all_seven_resx_files()
    {
        var resourcesDir = Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources");

        foreach (var language in SupportedLanguages)
        {
            var keys = ResxKeys(Path.Combine(resourcesDir, $"SharedResource.{language}.resx"));
            foreach (var expected in AllExpectedKeys())
            {
                Assert.True(keys.Contains(expected), $"SharedResource.{language}.resx is missing nav key: {expected}");
            }
        }
    }

    [Fact]
    public void Nav_values_are_non_empty_in_all_seven_languages()
    {
        var resourcesDir = Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources");

        foreach (var language in SupportedLanguages)
        {
            var values = ResxValues(Path.Combine(resourcesDir, $"SharedResource.{language}.resx"));
            foreach (var expected in AllExpectedKeys())
            {
                Assert.True(values.TryGetValue(expected, out var value) && !string.IsNullOrWhiteSpace(value),
                    $"SharedResource.{language}.resx has an empty value for {expected}");
            }
        }
    }

    // Pin the frontend localizer's precedence contract (it lives in the web app, not referenceable from here, so we
    // assert against its source — same approach as the password-bridge AuthGateway guard):
    //   • an OVERRIDE is rendered as-typed (isOverride short-circuits before any lookup);
    //   • a missing key (ResourceNotFound) falls back to the server default name — never the raw key.
    [Fact]
    public void NavNameLocalizer_encodes_the_override_and_fallback_precedence()
    {
        var source = File.ReadAllText(Path.Combine(
            RepoRoot(), "frontend", "Diten.Web", "Services", "Navigation", "NavNameLocalizer.cs"));

        Assert.Contains("isOverride ? serverName", source);       // override → as-typed, no localization
        Assert.Contains("ResourceNotFound", source);              // missing key detected
        Assert.Contains("? serverName", source);                  // ...and falls back to the server default
        Assert.Contains("Nav.Domain.", source);
        Assert.Contains("Nav.Module.", source);
        Assert.Contains("Nav.Page.", source);
        // The lookup MUST normalize the code before building the key (else casing/separator drift silently misses).
        Assert.Contains("Normalize(code)", source);
        Assert.Contains("char.IsLetterOrDigit", source);
        Assert.Contains("ToUpperInvariant", source);
    }

    [Fact]
    public void Normalize_is_casing_and_separator_immune()
    {
        Assert.Equal("ACCESSGOVERNANCE", Normalize("access-governance"));
        Assert.Equal("ACCESSGOVERNANCE", Normalize("ACCESS-GOVERNANCE"));      // catalog UPPERCASE-dash form
        Assert.Equal("PLATFORMSHAREDSERVICES", Normalize("PLATFORM-SHARED-SERVICES"));
        Assert.Equal(Normalize("Platform Shared Services"), Normalize("PLATFORM-SHARED-SERVICES"));
        Assert.Equal(Normalize("PlatformSharedServices"), Normalize("PLATFORM-SHARED-SERVICES"));
        Assert.Equal("ROLEPERMISSIONS", Normalize("ROLE_PERMISSIONS"));
        Assert.Equal("DEVENABLEMENT", Normalize("DevEnablement"));
    }

    [Fact]
    public void Normalized_key_exists_for_every_real_emitted_code_variant()
    {
        var resourcesDir = Path.Combine(RepoRoot(), "frontend", "Diten.Web", "Resources");
        var keys = ResxKeys(Path.Combine(resourcesDir, "SharedResource.en.resx"));

        // Prove the runtime path: whatever casing/separators the catalog emits, normalize lands on a present key.
        foreach (var variant in new[] { "access-governance", "ACCESS-GOVERNANCE", "Access Governance" })
        {
            Assert.True(keys.Contains("Nav.Module." + Normalize(variant)), $"no key for module variant '{variant}'");
        }

        foreach (var variant in new[] { "DevEnablement", "DEVENABLEMENT", "Dev Enablement" })
        {
            Assert.True(keys.Contains("Nav.Domain." + Normalize(variant)), $"no key for domain variant '{variant}'");
        }
    }

    private static HashSet<string> ResxKeys(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .Select(d => (string?)d.Attribute("name"))
            .Where(n => n is not null)
            .Select(n => n!)
            .ToHashSet(StringComparer.Ordinal);

    private static Dictionary<string, string> ResxValues(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .Where(d => d.Attribute("name") is not null)
            .ToDictionary(d => (string)d.Attribute("name")!, d => d.Element("value")?.Value ?? string.Empty, StringComparer.Ordinal);

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "frontend", "Diten.Web", "Resources")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repo root (frontend/Diten.Web/Resources) from the test output directory.");
    }
}
