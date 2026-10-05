using System.Net;
using Diten.AuthService.Infrastructure.Security;
using Microsoft.AspNetCore.Http;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// BL-529 FIX2/FIX3 — the anonymous platform password doors' limiter and the client address it keys on, measured on their
/// own with small numbers. The HTTP tests prove the 429s on the wire.
/// </summary>
public sealed class PasswordDoorRateLimiterTests
{
    [Fact]
    public void Forgot_password_past_the_per_address_limit_refuses_the_address_from_any_client()
    {
        using var limiter = new PasswordDoorRateLimiter(perAddressLimit: 2, perClientLimit: 100, TimeSpan.FromMinutes(5));

        Assert.True(limiter.TryAcquireForgotPassword("10.0.0.1", "a@x.test"));
        Assert.True(limiter.TryAcquireForgotPassword("10.0.0.2", "A@X.test ")); // the address, not its spelling
        Assert.False(limiter.TryAcquireForgotPassword("10.0.0.3", "a@x.test"));
        Assert.True(limiter.TryAcquireForgotPassword("10.0.0.1", "b@x.test"));
    }

    [Fact]
    public void Past_the_per_client_limit_the_client_is_refused_whatever_the_address()
    {
        using var limiter = new PasswordDoorRateLimiter(perAddressLimit: 100, perClientLimit: 2, TimeSpan.FromMinutes(5));

        Assert.True(limiter.TryAcquireForgotPassword("10.0.0.9", "a@x.test"));
        Assert.True(limiter.TryAcquireForgotPassword("10.0.0.9", "b@x.test"));
        Assert.False(limiter.TryAcquireForgotPassword("10.0.0.9", "c@x.test"));
        Assert.True(limiter.TryAcquireForgotPassword("10.0.0.8", "c@x.test"));
    }

    [Fact]
    public void The_link_door_never_counts_by_address_alone()
    {
        using var limiter = new PasswordDoorRateLimiter(perAddressLimit: 2, perClientLimit: 100, TimeSpan.FromMinutes(5));

        Assert.True(limiter.TryAcquireLinkRedemption("10.0.0.66", "owner@x.test"));
        Assert.True(limiter.TryAcquireLinkRedemption("10.0.0.66", "owner@x.test"));
        Assert.False(limiter.TryAcquireLinkRedemption("10.0.0.66", "owner@x.test")); // the flooding client is stopped
        Assert.True(limiter.TryAcquireLinkRedemption("10.0.0.7", "owner@x.test"));  // the owner, elsewhere, is not
    }

    [Fact]
    public void The_two_doors_are_counted_apart()
    {
        using var limiter = new PasswordDoorRateLimiter(perAddressLimit: 1, perClientLimit: 100, TimeSpan.FromMinutes(5));

        Assert.True(limiter.TryAcquireForgotPassword("10.0.0.1", "a@x.test"));
        Assert.False(limiter.TryAcquireForgotPassword("10.0.0.1", "a@x.test"));
        Assert.True(limiter.TryAcquireLinkRedemption("10.0.0.1", "a@x.test"));
    }

    // ── the client address ──────────────────────────────────────────────────────────────────────────────────

    private static readonly IPAddress Proxy = IPAddress.Parse("10.1.1.1");

    [Fact]
    public void An_untrusted_peer_is_the_client_whatever_forwarded_header_it_writes()
    {
        var resolver = new ClientAddressResolver([Proxy], trustLoopback: false);

        Assert.Equal("198.51.100.7", resolver.Resolve(Request("198.51.100.7", "203.0.113.1")));
    }

    [Fact]
    public void Behind_a_trusted_proxy_the_spoofed_leftmost_entry_is_not_believed()
    {
        var resolver = new ClientAddressResolver([Proxy], trustLoopback: false);

        // The client wrote "1.2.3.4" itself; the trusted proxy appended the address it really saw.
        Assert.Equal("198.51.100.7", resolver.Resolve(Request("10.1.1.1", "1.2.3.4, 198.51.100.7")));
    }

    [Fact]
    public void Two_clients_behind_the_same_trusted_proxy_are_two_clients()
    {
        var resolver = new ClientAddressResolver([Proxy, IPAddress.Parse("10.1.1.2")], trustLoopback: false);

        var first = resolver.Resolve(Request("10.1.1.1", "198.51.100.7, 10.1.1.2"));
        var second = resolver.Resolve(Request("10.1.1.1", "198.51.100.8, 10.1.1.2"));

        Assert.Equal("198.51.100.7", first);
        Assert.Equal("198.51.100.8", second);
    }

    [Fact]
    public void Loopback_is_trusted_only_where_it_was_asked_for()
    {
        Assert.Equal("198.51.100.9", new ClientAddressResolver([], trustLoopback: true).Resolve(Request("127.0.0.1", "198.51.100.9")));
        Assert.Equal("127.0.0.1", new ClientAddressResolver([], trustLoopback: false).Resolve(Request("127.0.0.1", "198.51.100.9")));
    }

    private static HttpContext Request(string peer, string? forwardedFor)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
        if (forwardedFor is not null) context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        return context;
    }
}
