using System.Xml.Linq;
using Diten.AuthService.Application.Features.Users.Services;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// CT guard (2026-09-23) for the WP-AUTH-INVITED-LIFECYCLE-01 error-code bridge, in the shape of
/// <see cref="Password.PasswordErrorCodeContractTests"/>: AuthService emits a stable code, the Users proxy hands it
/// over untouched, and the screen's index.js turns it into a UsersIndex resx key in all seven languages. Measured with
/// a sabotage: renaming <see cref="UserLifecycle.EmailTakenCode"/> on the Auth side alone left every existing test
/// green — the JS map, the proxy tests and the vitest guard all pinned the LITERAL, nothing tied them to the constant.
/// </summary>
public sealed class UserLifecycleErrorCodeContractTests
{
    // code (the Auth constant) ⇔ UsersIndex resx key (what index.js ERROR_CODE_KEYS maps it to)
    private static readonly IReadOnlyDictionary<string, string> ExpectedCodeToResourceKey = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        [UserLifecycle.EmailTakenCode] = "ErrorUserEmailTaken",
        [UserLifecycle.InvitationPendingCode] = "ErrorUserInvitationPending",
    };

    private static readonly string[] SupportedLanguages = ["en", "tr", "fr", "es", "zh", "ar", "ru"];

    [Fact]
    public void The_screen_maps_every_lifecycle_code_to_its_key()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot(), "frontend", "Diten.Web", "wwwroot", "assets", "js", "Governance", "Users", "index.js"));

        foreach (var (code, key) in ExpectedCodeToResourceKey)
        {
            Assert.True(source.Contains($"{code}: '{key}'", StringComparison.Ordinal),
                $"index.js ERROR_CODE_KEYS does not map {code} to {key}; the refusal would fall back to the gateway's English text.");
        }
    }

    [Fact]
    public void Every_lifecycle_key_exists_in_all_seven_UsersIndex_resx_files_and_reaches_the_bridge()
    {
        var root = RepoRoot();
        var resourcesDir = Path.Combine(root, "frontend", "Diten.Web", "Resources", "Views", "Governance", "Users");
        var bridge = File.ReadAllText(Path.Combine(root, "frontend", "Diten.Web", "Views", "Governance", "Users", "_IndexL10n.cshtml"));

        foreach (var key in ExpectedCodeToResourceKey.Values)
        {
            Assert.True(bridge.Contains($"{key} = Localizer[\"{key}\"].Value", StringComparison.Ordinal),
                $"_IndexL10n.cshtml does not publish {key} to the screen.");
            foreach (var language in SupportedLanguages)
            {
                var keys = ResxKeys(Path.Combine(resourcesDir, $"UsersIndex.{language}.resx"));
                Assert.True(keys.Contains(key), $"UsersIndex.{language}.resx is missing key: {key}");
            }
        }
    }

    private static HashSet<string> ResxKeys(string path) =>
        XDocument.Load(path).Root!.Elements("data")
            .Select(d => (string?)d.Attribute("name"))
            .Where(n => n is not null)
            .Select(n => n!)
            .ToHashSet(StringComparer.Ordinal);

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
