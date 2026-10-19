using Xunit;

namespace Diten.AuthService.Application.Tests;

/// <summary>
/// Main sync FIX1 (CT decision B) — where main's Auth composition-root guard takes its Mongo from, on plain values (no host
/// is built, no mongod is started): a given AUTH_COMPOSITION_GUARD_MONGO_URI is used unless it is blank or names the
/// shared port band (or no port); without it, Auth's owned mongod — and with none of those, red BY NAME, never 27017.
/// </summary>
public sealed class CompositionRootGuardSourceTests
{
    [Theory]
    [InlineData("mongodb://127.0.0.1:27118/?directConnection=true")]
    [InlineData("mongodb://127.0.0.1:40123")]
    public void A_given_disposable_uri_is_used_as_given(string uri)
    {
        Assert.Equal(uri, CompositionRootGuardTests.Host.ValidGivenUri(uri));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("mongodb://127.0.0.1:27017")]
    [InlineData("mongodb://localhost:27019/?directConnection=true")]
    [InlineData("mongodb://localhost")]
    public void A_given_uri_that_is_blank_or_names_the_shared_port_band_is_red(string uri)
    {
        var refused = Assert.Throws<InvalidOperationException>(() => CompositionRootGuardTests.Host.ValidGivenUri(uri));
        Assert.Contains(CompositionRootGuardTests.MongoVariable, refused.Message);
    }

    [Fact]
    public void No_uri_and_no_owned_mongod_is_red_by_name()
    {
        var refused = Assert.Throws<InvalidOperationException>(() => CompositionRootGuardTests.Host.RequireOwnedMongod(null));
        Assert.StartsWith(Persistence.DisposableAuthMongoReplicaSet.RequiredButUnavailable + ":", refused.Message);
    }

    [Fact]
    public void An_owned_mongod_is_accepted()
    {
        CompositionRootGuardTests.Host.RequireOwnedMongod("/usr/test/bin/mongod");
    }
}
