using System.Text.RegularExpressions;

namespace TenantArchitecture.ArchitectureTests;

/*
 * THE GUARD — AN AUTH TEST NEVER ADDRESSES THE SHARED DEV MONGO (main sync FIX1, CT decision C — BL-595's Auth half, 2026-10-12).
 *
 * WHY (measured): one full Auth App run held 2250 ESTABLISHED connections to 127.0.0.1:27017 — four test classes built a
 * MongoClient from the literal "mongodb://localhost:27017" and wrote to the developer's shared Mongo
 * (WorkflowTaskDecisionPermissionScopeReconciliationTests, ProductLegalEntityScopePermissionOnboardingMongoTests,
 * IntegrationEventInboxRepositoryMongoTests, ServiceIdentityTokenMongoTests). They now run on the owned disposable mongod
 * (AuthReconciliationMongoCollection, OwnedMongoFact). This guard keeps the literal from coming back; the host side is
 * held by AccountKindEndpointTests.The_host_resolved_configuration_and_clients_name_no_shared_mongo.
 *
 * THE RULE is the Platform guard's (PlatformTestsOwnMongoGuardTests): full-line comments dropped, every other `27017` /
 * `mongodb://localhost` counted; a file may hold one only if it is named here with its exact count and reason.
 */
public class AuthTestsOwnMongoGuardTests
{
    private const string Root = "services/Diten.AuthService/tests";

    private static readonly Regex SharedMongo = new(@"27017|mongodb://localhost(?!:1\b)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Dictionary<string, (int Count, string Reason)> Allowed = new(StringComparer.Ordinal)
    {
        ["Diten.AuthService.Application.Tests/CompositionRootGuardTests.cs"] = (1, "refuses a given URI on the shared port band"),
        ["Diten.AuthService.Application.Tests/CompositionRootGuardSourceTests.cs"] = (3, "the refused given URIs (no connection)"),
        ["Diten.AuthService.Application.Tests/Roles/ProductAbbreviationPermissionOnboardingMongoTests.cs"] = (1, "refuses the shared port"),
        ["Diten.AuthService.Application.Tests/ServiceIdentityTokens/ServiceClientOperationalProvisioningMongoTests.cs"] = (1, "refuses the shared port"),
        ["Diten.AuthService.Application.Tests/Testing/AccountKindAcceptance.cs"] = (2, "messages that say the shared server is never used"),
        ["Diten.AuthService.Application.Tests/Testing/AccountKindAcceptanceGuard.cs"] = (2, "the runner guard refuses the shared port, and says so"),
        ["Diten.AuthService.Application.Tests/Testing/AccountKindAcceptanceGuardTests.cs"] = (5, "the runner guard's own refusal cases (no connection)"),
        ["Diten.AuthService.Application.Tests/Behaviors/GlobalExceptionHandlerServerErrorTests.cs"] = (1, "a leaked exception text the handler must hide (no connection)"),
        ["Diten.AuthService.Application.Tests/Users/AccountKindEndpointTests.cs"] = (3, "asserts the host and its clients are NOT on the shared Mongo"),
        ["Diten.AuthService.Application.Tests/Persistence/DisposableAuthMongoReplicaSet.cs"] = (2, "asserts the owned port is not 27017"),
        ["Diten.AuthService.Application.Tests/Operational/OperationalReasonTests.cs"] = (1, "a credential-redaction input string (no connection)"),
        ["Diten.AuthService.ServiceClientRegistry.Tests/Support/OwnedMongo.cs"] = (1, "refuses the shared port"),
        ["Diten.AuthService.AccountKindAcceptanceHost.Tests/NetworkIsolationTests.cs"] = (1, "the ports the isolated host must not reach"),
        ["Diten.AuthService.AccountKindAcceptanceHost.Tests/PoisonedEnvironmentTests.cs"] = (1, "a poisoned environment the host must refuse (no connection)")
    };

    [Fact]
    public void No_auth_test_addresses_the_shared_dev_mongo()
    {
        var root = Path.Combine(RepoRoot(), Root);
        var found = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                           && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Select(path => (Path: Path.GetRelativePath(root, path).Replace('\\', '/'), Count: Count(File.ReadAllText(path))))
            .Where(file => file.Count > 0)
            .ToDictionary(file => file.Path, file => file.Count, StringComparer.Ordinal);

        Assert.True(found.Count >= 10, $"only {found.Count} files with a 27017 mention found — the scan is blind");
        var unexpected = found.Where(file => !Allowed.TryGetValue(file.Key, out var allowed) || allowed.Count != file.Value)
            .Select(file => $"{file.Key}: {file.Value} (allowed {(Allowed.TryGetValue(file.Key, out var a) ? a.Count : 0)})")
            .ToArray();
        var stale = Allowed.Keys.Where(path => !found.ContainsKey(path)).ToArray();
        Assert.True(unexpected.Length == 0,
            "Platform test files that address the shared dev Mongo (27017 / mongodb://localhost) — use PlatformOwnedMongo "
            + "(MongoIntegrationHarness.ConnectionStringAsync), or name the file with its reason:\n" + string.Join("\n", unexpected));
        Assert.True(stale.Length == 0, "Allowed entries with no occurrence left — remove them: " + string.Join(", ", stale));
        Assert.All(Allowed, entry => Assert.False(string.IsNullOrWhiteSpace(entry.Value.Reason), entry.Key));
    }

    [Theory]
    [InlineData("var c = new MongoClient(\"mongodb://localhost:27017\");", 2)]
    [InlineData("var c = new MongoClient(\"mongodb://LOCALHOST\");", 1)]
    [InlineData("var c = new MongoClient(\"mongodb://127.0.0.1:27017\"); // fine?", 1)]
    [InlineData("var c = new MongoClient(\"mongodb://localhost:1/?serverSelectionTimeoutMS=10\");", 0)]
    [InlineData("// never the shared 27017", 0)]
    [InlineData("/// <summary>never mongodb://localhost:27017</summary>", 0)]
    [InlineData(" * on 27017 in a block comment", 0)]
    public void Only_full_line_comments_are_ignored(string line, int count)
    {
        Assert.Equal(count, Count(line));
    }

    private static int Count(string text)
        => SharedMongo.Matches(string.Join("\n", text.Replace("\r\n", "\n").Split('\n')
            .Where(line => !Regex.IsMatch(line, @"^\s*(?://|\*|/\*)")))).Count;

    private static string RepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "AGENTS.md"))) return current.FullName;
            current = current.Parent;
        }

        throw new InvalidOperationException("Repo root not found.");
    }
}
