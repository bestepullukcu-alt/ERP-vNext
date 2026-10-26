using Diten.Web.Services.Proxy;
using Xunit;

namespace Diten.Web.Tests.Security;

/// <summary>WP-MVP1-SEC-PROXY1 — unit tests of the boundary itself (<see cref="ProxyPath.TryBuild(string, string?, out string, out string?)"/>).</summary>
public sealed class ProxyPathTests
{
    private const string Prefix = "/api/expiry";

    [Theory]
    [InlineData("exceptions", "/api/expiry/exceptions")]
    [InlineData("/exceptions", "/api/expiry/exceptions")]
    [InlineData("exceptions/", "/api/expiry/exceptions/")]
    [InlineData("exceptions/0b6c2f1e-3d4a-4c5b-9e8f-7a6b5c4d3e2f/take", "/api/expiry/exceptions/0b6c2f1e-3d4a-4c5b-9e8f-7a6b5c4d3e2f/take")]
    [InlineData("evidence/exports/E-1/file", "/api/expiry/evidence/exports/E-1/file")]
    [InlineData("sets/uom.v2/values", "/api/expiry/sets/uom.v2/values")]
    [InlineData("a_b~c-d", "/api/expiry/a_b~c-d")]
    [InlineData("...", "/api/expiry/...")]
    public void A_plain_path_is_built_under_the_prefix(string everything, string expected)
    {
        Assert.True(ProxyPath.TryBuild(Prefix, everything, out var path, out var reason), reason);
        Assert.Equal(expected, path);
        Assert.Null(reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void An_empty_rest_is_the_prefix(string? everything)
    {
        Assert.True(ProxyPath.TryBuild(Prefix, everything, out var path));
        Assert.Equal(Prefix, path);
    }

    [Theory]
    [InlineData("../locations", ProxyPath.Reasons.DotSegment)]
    [InlineData("a/../b", ProxyPath.Reasons.DotSegment)]
    [InlineData("a/./b", ProxyPath.Reasons.DotSegment)]
    [InlineData("..", ProxyPath.Reasons.DotSegment)]
    [InlineData("%2E%2E/x", ProxyPath.Reasons.DotSegment)]
    [InlineData("%252E%252E/x", ProxyPath.Reasons.DotSegment)]
    [InlineData("..%5Clocations", ProxyPath.Reasons.Backslash)]
    [InlineData("..\\locations", ProxyPath.Reasons.Backslash)]
    [InlineData("a\\b", ProxyPath.Reasons.Backslash)]
    [InlineData("a%00b", ProxyPath.Reasons.ControlCharacter)]
    [InlineData("a\u0001b", ProxyPath.Reasons.ControlCharacter)]
    [InlineData("a\u007Fb", ProxyPath.Reasons.ControlCharacter)]
    [InlineData("//evil/x", ProxyPath.Reasons.SchemeOrHost)]
    [InlineData("http://evil/x", ProxyPath.Reasons.SchemeOrHost)]
    [InlineData("@evil", ProxyPath.Reasons.SchemeOrHost)]
    [InlineData("a@evil", ProxyPath.Reasons.SchemeOrHost)]
    [InlineData("javascript:x", ProxyPath.Reasons.SchemeOrHost)]
    [InlineData("a%2Fb", ProxyPath.Reasons.EncodedSeparator)]
    [InlineData("a%5cb", ProxyPath.Reasons.Backslash)]
    [InlineData("a%2eb", ProxyPath.Reasons.EncodedSeparator)]
    [InlineData("a//b", ProxyPath.Reasons.EmptySegment)]
    [InlineData("a b", ProxyPath.Reasons.DisallowedCharacter)]
    [InlineData("a;b", ProxyPath.Reasons.DisallowedCharacter)]
    [InlineData("a?b=1", ProxyPath.Reasons.DisallowedCharacter)]
    [InlineData("a#b", ProxyPath.Reasons.DisallowedCharacter)]
    [InlineData("a/b:c", ProxyPath.Reasons.DisallowedCharacter)]
    [InlineData("．．/x", ProxyPath.Reasons.DisallowedCharacter)]
    [InlineData("%C0%AE%C0%AE/x", ProxyPath.Reasons.DisallowedCharacter)]
    [InlineData("ı", ProxyPath.Reasons.DisallowedCharacter)]
    public void An_unsafe_rest_is_refused_with_its_reason(string everything, string expected)
    {
        Assert.False(ProxyPath.TryBuild(Prefix, everything, out var path, out var reason));
        Assert.Equal(expected, reason);
        Assert.Equal(Prefix, path);
    }

    [Fact]
    public void The_decode_loop_peels_up_to_the_bound_and_refuses_beyond_it()
    {
        Assert.Equal(5, ProxyPath.MaxDecodeRounds);
        for (var layers = 1; layers <= ProxyPath.MaxDecodeRounds; layers++)
        {
            Assert.False(ProxyPath.TryBuild(Prefix, ProxyBoundaryTests.Layered(layers) + "/x", out _, out var reason));
            Assert.Equal(ProxyPath.Reasons.DotSegment, reason);
        }

        Assert.False(ProxyPath.TryBuild(Prefix, ProxyBoundaryTests.Layered(ProxyPath.MaxDecodeRounds + 1) + "/x", out _, out var deep));
        Assert.Equal(ProxyPath.Reasons.TooDeeplyEncoded, deep);
    }

    [Fact]
    public void Every_accepted_path_stays_below_the_prefix_after_Uri_normalization()
    {
        foreach (var input in ProxyBoundaryTests.HandoffTraversals.Concat(ProxyBoundaryTests.MoreAttacks).Concat(["exceptions", "a/b/c", "x.y"]))
        {
            if (!ProxyPath.TryBuild(Prefix, input, out var path)) continue;
            var absolute = new Uri(new Uri("http://gateway.test"), path).AbsolutePath;
            Assert.StartsWith(Prefix + "/", absolute);
        }
    }

    [Fact]
    public void The_refusal_is_400_INVALID_PROXY_PATH_with_the_reason()
    {
        var result = ProxyPath.Refusal(ProxyPath.Reasons.Backslash);
        Assert.Equal(400, ProxyHarness.Status(result));
        Assert.Equal((ProxyPath.InvalidProxyPath, ProxyPath.Reasons.Backslash), ProxyHarness.Refusal(result));
    }
}
