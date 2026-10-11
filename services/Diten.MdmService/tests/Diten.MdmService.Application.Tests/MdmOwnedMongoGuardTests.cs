using System.Text.RegularExpressions;
using MongoDB.Driver;
using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// BL-595 (WP-MDM-OWNED-MONGO-01) — the guard: no MDM test source names the shared developer Mongo (its port, see
/// <see cref="MdmTestMongo.SharedPort"/>, or a loopback mongodb URI without a port). Every Mongo test connects
/// through <see cref="MdmTestMongo"/>; the shared port is named once, in that helper, so tests can refuse it.
/// It reads source text (comments included) and needs no Mongo.
/// </summary>
public sealed class MdmOwnedMongoGuardTests
{
    private const string TestsRoot = "services/Diten.MdmService/tests";

    // The first round left these (they change on open MDM branches); the second round, after those merge, empties the
    // list. A file listed here that no longer names the shared Mongo is red too — take it off.
    private static readonly string[] NotYetMoved =
    [
        TestsRoot + "/Diten.MdmService.Api.Tests/MdmHttpHost.cs",
        TestsRoot + "/Diten.MdmService.Application.Tests/FinishedGoodDraftFoundationMongoTests.cs",
        TestsRoot + "/Diten.MdmService.Application.Tests/GskuRegisterMongoTests.cs",
        TestsRoot + "/Diten.MdmService.Application.Tests/LskuRegisterMongoTests.cs",
        TestsRoot + "/Diten.MdmService.Application.Tests/MdmDocumentClassMapsTests.cs",
        TestsRoot + "/Diten.MdmService.Application.Tests/StockBehaviourMongoTests.cs"
    ];

    private const string Helper = TestsRoot + "/Diten.MdmService.Application.Tests/MdmTestMongo.cs";

    // Built from the helper's constant, so this file never spells the port itself.
    private static readonly Regex SharedMongo = new(MdmTestMongo.SharedPort + @"|mongodb:/{2}(?:localhost|127\.0\.0\.1)(?!:)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [Fact]
    public void BL595_No_mdm_test_names_the_shared_mongo()
    {
        var root = RepoRoot();
        var sources = Directory.EnumerateFiles(Path.Combine(root, TestsRoot), "*.cs", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace('\\', '/'))
            .Where(path => !path.Contains("/bin/", StringComparison.Ordinal) && !path.Contains("/obj/", StringComparison.Ordinal))
            .ToArray();
        Assert.True(sources.Length > 100, $"only {sources.Length} MDM test sources read — the guard is blind");

        var naming = sources.Where(path => SharedMongo.IsMatch(File.ReadAllText(Path.Combine(root, path)))).ToHashSet();
        var offenders = naming.Except(NotYetMoved).Where(path => path != Helper).Order(StringComparer.Ordinal).ToArray();
        Assert.True(offenders.Length == 0,
            "an MDM test names the shared Mongo (its port or a portless loopback URI) — connect through MdmTestMongo.ConnectionString "
            + "and refuse the port with MdmTestMongo.SharedPort:\n" + string.Join("\n", offenders));

        var stale = NotYetMoved.Where(path => !naming.Contains(path)).ToArray();
        Assert.True(stale.Length == 0, "no longer names the shared Mongo — take it off NotYetMoved:\n" + string.Join("\n", stale));
    }

    private static string RepoRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) return directory.FullName;
        throw new InvalidOperationException("Repository root (AGENTS.md) not found.");
    }
}

/// <summary>BL-595 — the helper's own rules (Auth's resolution) and the server it gives.</summary>
public sealed class MdmTestMongoTests
{
    private const string Mongod = "/opt/test/bin/mongod";

    [Theory]
    [InlineData("absent", false, false, null, MdmTestMongo.Unavailable)]
    [InlineData("absent", true, false, null, MdmTestMongo.RequiredButUnavailable)]
    [InlineData("", false, false, null, MdmTestMongo.InvalidVariable)]
    [InlineData("", true, false, null, MdmTestMongo.InvalidVariable)]
    [InlineData("mongod", false, false, null, MdmTestMongo.InvalidVariable)]
    [InlineData("/opt/missing/mongod", false, false, null, MdmTestMongo.InvalidVariable)]
    [InlineData(Mongod, false, false, Mongod, null)]
    [InlineData("absent", false, true, MdmTestMongo.WindowsDefaultMongod, null)]
    public void BL595_The_mongod_is_resolved_by_auths_rule(string variable, bool required, bool windows, string? executable,
        string? failure)
    {
        var resolution = MdmTestMongo.Resolve(variable == "absent" ? null : variable, required, windows,
            path => path is Mongod or MdmTestMongo.WindowsDefaultMongod);

        Assert.Equal(executable, resolution.Executable);
        if (failure is null) Assert.Null(resolution.Failure);
        else Assert.StartsWith(failure + ":", resolution.Failure);
    }

    // Nothing resolved → the named failure; never a connection, never the shared server.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BL595_Without_an_owned_mongod_there_is_no_connection_only_the_named_failure(bool required)
    {
        var started = false;
        var resolution = MdmTestMongo.Resolve(null, required, windows: false, _ => false);

        var error = Assert.Throws<InvalidOperationException>(() =>
            MdmTestMongo.ConnectionFor(resolution, _ => { started = true; return "unreachable"; }));

        Assert.False(started);
        Assert.Equal(resolution.Failure, error.Message);
    }

    // The in-process redirect of the six classes that own a replica set: only another test-owned loopback mongod.
    [Theory]
    [InlineData(null, false, null)]
    [InlineData("", false, null)]
    [InlineData("mongodb://127.0.0.1:" + MdmTestMongo.SharedPortText, false, null)]
    [InlineData("mongodb://localhost:" + MdmTestMongo.SharedPortText + "/?directConnection=true", false, null)]
    [InlineData("mongodb://127.0.0.1:41234/?replicaSet=x&directConnection=true", false, "mongodb://127.0.0.1:41234/?replicaSet=x&directConnection=true")]
    [InlineData("mongodb://db.example.com:41234", true, null)]
    [InlineData("not a uri", true, null)]
    public void BL595_A_redirect_is_only_another_test_owned_loopback_mongod_never_the_shared_one(string? value, bool red,
        string? expected)
    {
        if (red)
            Assert.StartsWith(MdmTestMongo.RedirectInvalid + ":",
                Assert.Throws<InvalidOperationException>(() => MdmTestMongo.RedirectFor(value)).Message);
        else
            Assert.Equal(expected, MdmTestMongo.RedirectFor(value));
    }

    // The server every MDM Mongo test gets: this process's own replica set on loopback, never the shared port.
    [Fact]
    public async Task BL595_The_owned_mongo_is_a_test_owned_replica_set_never_the_shared_port()
    {
        var settings = MongoClientSettings.FromConnectionString(MdmTestMongo.ConnectionString);
        var server = Assert.Single(settings.Servers);

        Assert.Equal("127.0.0.1", server.Host);
        Assert.NotEqual(MdmTestMongo.SharedPort, server.Port);
        Assert.Equal("mdm-owned", settings.ReplicaSetName);
        using var session = await new MongoClient(settings).StartSessionAsync();
        session.StartTransaction();
        await session.AbortTransactionAsync();
    }
}
