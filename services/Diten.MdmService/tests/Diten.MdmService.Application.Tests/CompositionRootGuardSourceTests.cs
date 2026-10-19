using Xunit;

namespace Diten.MdmService.Application.Tests;

/// <summary>
/// Main sync FIX1 (CT decision B) — where main's MDM composition-root guard takes its Mongo from, on plain values (no host
/// is built): a given MDM_COMPOSITION_GUARD_MONGO_URI is used unless it is blank or names the shared port band (or no
/// port); without it, MdmTestMongo's owned mongod (whose own "nothing found is red by name" rule MdmOwnedMongoGuardTests
/// holds). Never the shared Mongo.
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
    [InlineData("mongodb://127.0.0.1:" + MdmTestMongo.SharedPortText)]
    public void A_given_uri_that_is_blank_or_names_the_shared_port_band_is_red(string uri)
    {
        var refused = Assert.Throws<InvalidOperationException>(() => CompositionRootGuardTests.Host.ValidGivenUri(uri));
        Assert.Contains(CompositionRootGuardTests.MongoVariable, refused.Message);
    }
}
