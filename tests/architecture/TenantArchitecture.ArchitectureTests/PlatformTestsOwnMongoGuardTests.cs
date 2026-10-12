using System.Text.RegularExpressions;

namespace TenantArchitecture.ArchitectureTests;

/*
 * THE GUARD — A PLATFORM TEST NEVER ADDRESSES THE SHARED DEV MONGO (BL-595, WP-PLATFORM-OWNED-MONGO-01, 2026-10-11).
 *
 * WHY. The Platform tests used to connect to mongodb://localhost:27017 — the developer's shared Mongo: local runs left
 * test databases there (79 measured), a run after the machine slept timed out, and CI's platform-app row (nothing on
 * 27017) could not have been green. Main's Q131a (PR #134) moved them to DITEN_PLATFORM_TEST_MONGO_URI; the CT lane's
 * BL-595 to a mongod the run starts. CT decision C keeps both in ONE source (services/Diten.Platform/tests/Shared/
 * PlatformTestMongo.cs): the lane URI, else DITEN_TEST_MONGOD. This guard keeps the shared address from coming back.
 *
 * THE RULE (fail-closed, reads text, needs no Mongo). In every .cs file under services/Diten.Platform/tests, after the
 * FULL-LINE comments are dropped (`//`, `///`, `/*`, ` *` lines — prose about the rule is not the rule), each occurrence
 * of `27017` or `mongodb://localhost` (except the closed-port sentinel `mongodb://localhost:1`) is counted. A file may
 * hold such an occurrence ONLY if it is named below with its exact count and the reason — a test that asserts a port is
 * NOT 27017, a fake endpoint that opens no connection, a regex inside another guard. A new file, a higher count or a
 * lower count (update the list) is red. An inline comment is counted too: a false positive is red, never a pass.
 *
 * No "not yet" entries. After the main sync every Platform test reaches Mongo through PlatformTestMongo, except main's
 * CompositionGuard.Tests: it fails closed on its own PLATFORM_COMPOSITION_GUARD_MONGO_URI (never a fallback), and is on
 * the CI guard's "not yet" list until its owner wires it.
 */
public class PlatformTestsOwnMongoGuardTests
{
    private const string Root = "services/Diten.Platform/tests";

    private static readonly Regex SharedMongo = new(@"27017|mongodb://localhost(?!:1\b)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Dictionary<string, (int Count, string Reason)> Allowed = new(StringComparer.Ordinal)
    {
        ["Diten.Platform.Application.Tests/Audit/PpmAuditRetentionPolicySeedMongoTests.cs"] = (1, "the protected dev ports its own mongod must avoid"),
        ["Diten.Platform.Application.Tests/Workflow/WorkflowEscalationRestTests.cs"] = (1, "a fake ConnectionId endpoint for an exception; no connection"),
        ["Diten.Platform.Application.Tests/Persistence/DisposableMongoPortPolicyTests.cs"] = (4, "the port policy refuses 27017"),
        ["Diten.Platform.Application.Tests/Persistence/DisposableMongoReplicaSetTests.cs"] = (1, "asserts the owned port is not 27017"),
        ["Diten.Platform.Application.Tests/Persistence/DisposableStandaloneMongo.cs"] = (1, "its port choice skips 27017"),
        ["Diten.Platform.Application.Tests/Persistence/MongoSchemaResidueHealerTests.cs"] = (1, "a fake ConnectionId endpoint for an exception; no connection"),
        ["Diten.Platform.Application.Tests/Persistence/PlatformMongoTestConnectionTests.cs"] = (4, "main's Q131a tests: URIs on 27017 / localhost are refused"),
        ["Diten.Platform.Application.Tests/Persistence/PlatformMongoTestLockTests.cs"] = (1, "a regex that refuses the shared address"),
        ["Diten.Platform.Application.Tests/Persistence/PlatformTestMongoTests.cs"] = (3, "URIs on 27017 / localhost are refused; the live port is outside 27017–27021"),
        ["Diten.Platform.Application.Tests/Persistence/StandaloneMongoTransactionFailClosedTests.cs"] = (1, "asserts the owned port is not 27017"),
        ["Diten.Platform.Application.Tests/Persistence/SubscriptionSessionAndStandaloneFailureTests.cs"] = (1, "asserts the owned port is not 27017"),
        ["Diten.Platform.Application.Tests/BusinessReferenceData/BusinessReferenceDataTestHarnessScopeTests.cs"] = (1, "a regex that refuses the shared address"),
        ["Diten.Platform.Application.Tests/Authorization/PhysicalEntitlementTransactionalArchitectureTests.cs"] = (2, "asserts a harness does not use port 27017"),
        ["Shared/PlatformTestMongo.cs"] = (2, "the protected port band, and the refusal message naming the shared instance it never uses"),
        ["Diten.Platform.CompositionGuard.Tests/CompositionRootGuardTests.cs"] = (1, "main (MVP-6): its refusal message says 'never 27017'")
    };

    [Fact]
    public void No_platform_test_addresses_the_shared_dev_mongo()
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
