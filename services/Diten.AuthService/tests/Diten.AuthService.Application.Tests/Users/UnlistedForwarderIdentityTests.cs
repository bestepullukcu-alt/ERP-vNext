using System.Net;
using Diten.AuthService.Infrastructure.Security;
using Microsoft.AspNetCore.Http;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// WP-USERS-ROLES-CLOSE-FIX1 FIX4 item 2 — a peer OUTSIDE the trusted list that forwards for others (a gateway whose address
/// changed, a new load balancer) is a proxy nobody listed: every client behind it would be one "identified" client sharing one
/// per-client count. It is never identified; a listed proxy's forwarded client is, and so is an unlisted peer that forwards
/// for nobody (a client that connects directly).
/// </summary>
public sealed class UnlistedForwarderIdentityTests
{
    private static readonly IPAddress Gateway = IPAddress.Parse("10.0.0.9");
    private readonly ClientAddressResolver _resolver = new([Gateway]);

    [Fact]
    public void An_unlisted_peer_that_forwards_is_not_identified()
    {
        var identity = _resolver.Identify(Request("10.0.0.77", forwardedFor: "203.0.113.5"));

        Assert.False(identity.Identified);
    }

    [Fact]
    public void A_listed_proxy_identifies_the_client_it_forwards_and_a_direct_client_is_itself()
    {
        var forwarded = _resolver.Identify(Request("10.0.0.9", forwardedFor: "203.0.113.5"));
        Assert.True(forwarded.Identified);
        Assert.Equal("203.0.113.5", forwarded.Key);

        var direct = _resolver.Identify(Request("198.51.100.4", forwardedFor: null));
        Assert.True(direct.Identified);
        Assert.Equal("198.51.100.4", direct.Key);
    }

    private static HttpContext Request(string peer, string? forwardedFor)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
        if (forwardedFor is not null) context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        return context;
    }
}
