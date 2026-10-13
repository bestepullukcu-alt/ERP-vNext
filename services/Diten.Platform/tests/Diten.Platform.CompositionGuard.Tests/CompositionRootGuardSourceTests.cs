using System.Text.RegularExpressions;
using Diten.Platform.Tests.Shared;
using Xunit;

namespace Diten.Platform.CompositionGuard.Tests;

/// <summary>
/// WP-PLATFORM-COMPOSITION-GUARD-OWNED-01 — where the composition guard's Mongo comes from, on fakes (no host, no mongod):
/// a given PLATFORM_COMPOSITION_GUARD_MONGO_URI through main's Q131a check, else the owned source, else red by name.
/// </summary>
public sealed class CompositionRootGuardSourceTests
{
    private const string Owned = "mongodb://127.0.0.1:40123/?replicaSet=rs0&directConnection=true";

    [Fact]
    public void A_given_valid_uri_is_used_and_the_owned_source_is_not_asked()
    {
        var asked = false;
        var address = CompositionRootGuardTests.MongoAddress("mongodb://127.0.0.1:38994/?directConnection=true",
            () => { asked = true; return Task.FromResult(Owned); });

        Assert.Equal("mongodb://127.0.0.1:38994/?directConnection=true", address);
        Assert.False(asked);
    }

    // Main's Q131a rule decides a given URI: the protected band (an omitted port counts as the first one), a remote
    // host, credentials, an empty value — red, and the owned source is not asked instead.
    [Theory]
    [InlineData("mongodb://127.0.0.1:27017")]
    [InlineData("mongodb://127.0.0.1:27019/?directConnection=true")]
    [InlineData("mongodb://localhost")]
    [InlineData("mongodb://10.0.0.5:38994")]
    [InlineData("mongodb://user:pass@127.0.0.1:38994")]
    [InlineData("")]
    public void A_given_uri_that_breaks_mains_rule_is_red(string given)
    {
        var asked = false;
        var failure = Assert.Throws<InvalidOperationException>(() => CompositionRootGuardTests.MongoAddress(given,
            () => { asked = true; return Task.FromResult(Owned); }));

        Assert.Contains(CompositionRootGuardTests.MongoVariable, failure.Message);
        Assert.False(asked);
    }

    [Fact]
    public void Without_a_uri_the_owned_source_answers()
    {
        Assert.Equal(Owned, CompositionRootGuardTests.MongoAddress(null, () => Task.FromResult(Owned)));
    }

    // No URI and no mongod: the owned source's own refusal, by name — never an address, least of all the shared one.
    [Fact]
    public void No_uri_and_no_owned_mongod_is_red_by_name()
    {
        string? address = null;
        var failure = Record.Exception(() => address = CompositionRootGuardTests.MongoAddress(null,
            () => PlatformTestMongo.AddressAsync(PlatformTestMongo.Choose(null, null, windows: false, _ => false),
                _ => Task.FromResult(Owned))));

        Assert.Null(address);
        Assert.StartsWith(PlatformTestMongo.Unavailable + ":", Assert.IsType<InvalidOperationException>(failure).Message);
    }

    // FIX1 (independent review): the tests above prove MongoAddress, not that Host() uses it. Host() cannot be built
    // here without a mongod (and only once per process), so its source text is pinned instead: the ONE address line is
    // MongoAddress over the guard's variable with PlatformTestMongo.ConnectionStringAsync, and that address — nothing
    // else — is the host's MongoDbSettings:ConnectionString. Skipping MongoAddress, another owned source, a second read
    // of the variable or another connection string is red here.
    [Fact]
    public void Host_takes_its_mongo_from_MongoAddress_with_the_owned_platform_source()
    {
        var host = HostConstructor(File.ReadAllText(GuardSource()));

        Assert.Single(Regex.Matches(host,
            @"(?m)^\s*var mongo = MongoAddress\(Environment\.GetEnvironmentVariable\(MongoVariable\), PlatformTestMongo\.ConnectionStringAsync\);\s*$"));
        Assert.Single(Regex.Matches(host, @"MongoAddress\("));
        Assert.Single(Regex.Matches(host, @"ConnectionStringAsync"));
        Assert.Single(Regex.Matches(host, @"GetEnvironmentVariable\(MongoVariable\)"));
        Assert.Single(Regex.Matches(host, @"\[""MongoDbSettings:ConnectionString""\] = mongo,"));
        Assert.Single(Regex.Matches(host, @"MongoDbSettings:ConnectionString"));
    }

    // The body of `public Host()` — from its signature to the matching closing brace.
    private static string HostConstructor(string source)
    {
        var start = source.IndexOf("        public Host()\n", StringComparison.Ordinal);
        Assert.True(start >= 0, "public Host() not found — the pin is blind");
        var open = source.IndexOf('{', start);
        var depth = 0;
        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            else if (source[i] == '}' && --depth == 0) return source[start..(i + 1)];
        }

        throw new InvalidOperationException("public Host() has no closing brace.");
    }

    private static string GuardSource()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")))
                return Path.Combine(directory.FullName,
                    "services/Diten.Platform/tests/Diten.Platform.CompositionGuard.Tests/CompositionRootGuardTests.cs");
        }

        throw new InvalidOperationException("Repo root not found.");
    }
}
