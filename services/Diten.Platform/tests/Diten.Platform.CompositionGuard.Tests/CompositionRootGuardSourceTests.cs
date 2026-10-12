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
}
