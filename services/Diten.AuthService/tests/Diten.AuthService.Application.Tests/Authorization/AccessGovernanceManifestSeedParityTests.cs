using System.Text.RegularExpressions;
using Diten.AuthService.Persistence.Seed;

namespace Diten.AuthService.Application.Tests.Authorization;

/// <summary>
/// BL-458 — AuthService's Access Governance manifest is declared INSIDE Platform
/// (<c>AccessGovernanceManifestProvider.cs</c>), while its permission keys are seeded HERE (<see cref="DataSeeder"/>).
/// Nothing but this test ties the two: the catalog→Auth sync can only manage the keys the manifest names, and the Role
/// Permissions screen can only group the keys the catalog knows. The provider's source is read from the repository (the
/// PasswordErrorCodeContractTests shape) — AuthService does not reference Platform.
///
/// <list type="bullet">
/// <item>Every key the manifest declares (page permission or action) is seeded here.</item>
/// <item>Every seeded Access Governance key is declared in the manifest, except the API-only keys that have no screen
/// action — listed below, by name, so adding a new one is a decision, not a silent gap.</item>
/// </list>
/// </summary>
public sealed class AccessGovernanceManifestSeedParityTests
{
    /// <summary>Seeded, used by endpoints only (reference pickers / S2S validation), deliberately not page actions.</summary>
    private static readonly IReadOnlySet<string> ApiOnlyKeys = new HashSet<string>(StringComparer.Ordinal)
    {
        "auth.users.lookup",
        "auth.users.lookup-validation"
    };

    [Fact]
    public void Every_manifest_key_is_seeded_and_every_seeded_key_is_declared()
    {
        var declared = ManifestKeys();
        var seeded = DataSeeder.BuildCanonicalPermissions()
            .Where(p => p.Module == "access-governance")
            .Select(p => p.Key)
            .ToHashSet(StringComparer.Ordinal);

        var notSeeded = declared.Except(seeded).OrderBy(x => x).ToList();
        var undeclared = seeded.Except(declared).Except(ApiOnlyKeys).OrderBy(x => x).ToList();

        Assert.True(notSeeded.Count == 0,
            "The Access Governance manifest declares keys AuthService does not seed (the sync would create them as "
            + "catalog-owned, unprotected keys): " + string.Join(", ", notSeeded));
        Assert.True(undeclared.Count == 0,
            "AuthService seeds Access Governance keys the manifest does not declare (the catalog and the Role "
            + "Permissions screen will not know them): " + string.Join(", ", undeclared));
    }

    [Fact]
    public void The_export_key_is_declared_as_an_action_of_the_Users_page()
    {
        var source = ProviderSource();
        var users = Regex.Match(source, @"new ModuleManifestPage\(""USERS"".*?\]\)", RegexOptions.Singleline);

        Assert.True(users.Success, "USERS page not found in AccessGovernanceManifestProvider.cs");
        Assert.Contains("UsersExport", users.Value, StringComparison.Ordinal);
    }

    /// <summary>The page RequiredPermission and every action PermissionKey, constants resolved.</summary>
    private static HashSet<string> ManifestKeys()
    {
        var source = ProviderSource();
        var constants = Regex.Matches(source, @"private const string (?<name>\w+) = ""(?<value>[^""]+)"";")
            .ToDictionary(m => m.Groups["name"].Value, m => m.Groups["value"].Value, StringComparer.Ordinal);

        string Resolve(string token) => token.StartsWith('"')
            ? token.Trim('"')
            : constants.TryGetValue(token, out var value) ? value : throw new InvalidOperationException($"unresolved manifest constant {token}");

        var pageKeys = Regex.Matches(source, @"new ModuleManifestPage\(""\w+"",\s*""[^""]*"",\s*""[^""]*"",\s*(?<key>\w+|""[^""]+"")")
            .Select(m => Resolve(m.Groups["key"].Value));
        var actionKeys = Regex.Matches(source, @"new ModuleManifestAction\(""\w+"",\s*""[^""]*"",\s*(?<key>\w+|""[^""]+"")")
            .Select(m => Resolve(m.Groups["key"].Value));

        var keys = pageKeys.Concat(actionKeys).ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(keys); // a parser that finds nothing must not pass as "no difference"
        return keys;
    }

    private static string ProviderSource()
        => File.ReadAllText(Path.Combine(RepoRoot(), "services", "Diten.Platform", "src", "Diten.Platform.Application",
            "Features", "AccessGovernance", "SelfRegistration", "AccessGovernanceManifestProvider.cs"));

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "services", "Diten.Platform", "src")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repo root (services/Diten.Platform/src) from the test output directory.");
    }
}
