using System.Text.RegularExpressions;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// Merge WP-CT-LANE-BL521-MERGE-01 FIX1 (small 5) — ONLY THE DECORATOR TAKES THE CONCRETE UserRepository. Every handler gets
/// <c>IUserRepository</c>, which is <c>PlatformAccountScopedUserRepository</c> (BL-521 FIX6): a tenant actor's view and writes
/// lose the platform accounts there. The concrete Mongo repository underneath has no such rule; a class that took it directly
/// would bypass the decorator silently. Read from the production source (comments removed), the concrete type may appear in
/// exactly these places:
/// <list type="bullet">
/// <item>the decorator's field and constructor (<c>PlatformAccountScopedUserRepository.cs</c>);</item>
/// <item>its DI registration as itself, so the decorator can be built (<c>Persistence/DependencyInjection.cs</c>);</item>
/// <item>the seed's own hand-built instance (<c>Seed/DataSeeder.cs</c>: runs at startup, before any request, as the system);</item>
/// <item>the one-time service-client CLI's own persistence (<c>ServiceClientOperationalPersistenceRegistration.cs</c>: an
/// operator command outside the web host, no request and no tenant actor — nothing to hide from).</item>
/// </list>
/// Anything else is red: add it here only with a reason it may skip the platform-account rule.
/// </summary>
public sealed class ConcreteUserRepositoryConsumersTests
{
    private static readonly Dictionary<string, int> Allowed = new(StringComparer.Ordinal)
    {
        ["services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/PlatformAccountScopedUserRepository.cs"] = 2,
        ["services/Diten.AuthService/src/Diten.AuthService.Persistence/DependencyInjection.cs"] = 1,
        ["services/Diten.AuthService/src/Diten.AuthService.Persistence/Seed/DataSeeder.cs"] = 1,
        ["services/Diten.AuthService/src/Diten.AuthService.Persistence/ServiceClientOperationalPersistenceRegistration.cs"] = 1
    };

    private const string OwnDeclaration = "services/Diten.AuthService/src/Diten.AuthService.Persistence/Repositories/UserRepository.cs";
    private static readonly Regex Concrete = new(@"(?<![A-Za-z0-9_])UserRepository(?![A-Za-z0-9_])", RegexOptions.Compiled);
    private static readonly Regex Comments = new(@"//[^\r\n]*|/\*[\s\S]*?\*/", RegexOptions.Compiled);

    [Fact]
    public void Only_the_decorator_its_registration_and_the_seed_take_the_concrete_UserRepository()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (!File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent ?? throw new InvalidOperationException("Checkout required");
        var src = Path.Combine(root.FullName, "services", "Diten.AuthService", "src");
        Assert.True(File.Exists(Path.Combine(root.FullName, OwnDeclaration)), "the exempted declaration moved: " + OwnDeclaration);

        var found = Directory.EnumerateFiles(src, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin"))
            .Select(path => (Path: path, File: Path.GetRelativePath(root.FullName, path).Replace('\\', '/')))
            // WP-USERS-ROLES-CLOSE-FIX1 item 6 (e) — the type's own declaration, by its FULL relative path: any other file that
            // happens to be named UserRepository.cs is measured like every other file.
            .Where(item => item.File != OwnDeclaration)
            .Select(item => (item.File,
                Count: Concrete.Matches(Comments.Replace(File.ReadAllText(item.Path), string.Empty)).Count))
            .Where(item => item.Count > 0)
            .ToDictionary(item => item.File, item => item.Count, StringComparer.Ordinal);

        var unexpected = found.Where(item => !Allowed.TryGetValue(item.Key, out var allowed) || item.Value > allowed)
            .Select(item => $"{item.Key} ({item.Value})").ToArray();
        Assert.True(unexpected.Length == 0,
            "the concrete UserRepository is taken outside the decorator (it would skip the platform-account rule):\n"
            + string.Join("\n", unexpected));
        Assert.Equal(Allowed.OrderBy(x => x.Key, StringComparer.Ordinal), found.OrderBy(x => x.Key, StringComparer.Ordinal));
    }
}
