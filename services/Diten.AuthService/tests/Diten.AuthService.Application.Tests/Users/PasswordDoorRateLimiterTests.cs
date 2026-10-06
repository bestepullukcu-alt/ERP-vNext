using System.Net;
using Diten.AuthService.Application.Tests.Testing;
using Diten.AuthService.Infrastructure.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// BL-529 FIX2–FIX5 — the anonymous platform password doors' limiter and the client identity it keys on, measured on their
/// own with small numbers. The HTTP tests prove the 429s on the wire.
/// </summary>
public sealed class PasswordDoorRateLimiterTests
{
    [Fact]
    public void Forgot_password_past_the_per_address_limit_refuses_the_address_from_any_client()
    {
        using var limiter = new PasswordDoorRateLimiter(perAddressLimit: 2, perClientLimit: 100, TimeSpan.FromMinutes(5));

        Assert.True(limiter.TryAcquireForgotPassword("10.0.0.1", "a@x.test", countPerClient: true));
        Assert.True(limiter.TryAcquireForgotPassword("10.0.0.2", "A@X.test ", countPerClient: true)); // the address, not its spelling
        Assert.False(limiter.TryAcquireForgotPassword("10.0.0.3", "a@x.test", countPerClient: true));
        Assert.True(limiter.TryAcquireForgotPassword("10.0.0.1", "b@x.test", countPerClient: true));
    }

    [Fact]
    public void Past_the_per_client_limit_the_client_is_refused_whatever_the_address()
    {
        using var limiter = new PasswordDoorRateLimiter(perAddressLimit: 100, perClientLimit: 2, TimeSpan.FromMinutes(5));

        Assert.True(limiter.TryAcquireForgotPassword("10.0.0.9", "a@x.test", countPerClient: true));
        Assert.True(limiter.TryAcquireForgotPassword("10.0.0.9", "b@x.test", countPerClient: true));
        Assert.False(limiter.TryAcquireForgotPassword("10.0.0.9", "c@x.test", countPerClient: true));
        Assert.True(limiter.TryAcquireForgotPassword("10.0.0.8", "c@x.test", countPerClient: true));
    }

    [Fact]
    public void A_client_that_cannot_be_told_apart_has_no_per_client_count()
    {
        using var limiter = new PasswordDoorRateLimiter(perAddressLimit: 100, perClientLimit: 2, TimeSpan.FromMinutes(5));

        for (var i = 0; i < 10; i++)
        {
            Assert.True(limiter.TryAcquireForgotPassword("the-gateway", $"a{i}@x.test", countPerClient: false));
            Assert.True(limiter.TryCountInvalidLinkAttempt("the-gateway", $"a{i}@x.test", countPerClient: false));
        }
    }

    [Fact]
    public void Invalid_link_attempts_are_counted_per_client_and_per_client_and_address()
    {
        using var limiter = new PasswordDoorRateLimiter(perAddressLimit: 2, perClientLimit: 3, TimeSpan.FromMinutes(5));

        Assert.True(limiter.TryCountInvalidLinkAttempt("10.0.0.66", "owner@x.test", countPerClient: true));
        Assert.True(limiter.TryCountInvalidLinkAttempt("10.0.0.66", "owner@x.test", countPerClient: true));
        Assert.False(limiter.TryCountInvalidLinkAttempt("10.0.0.66", "owner@x.test", countPerClient: true)); // (client, address)
        Assert.True(limiter.TryCountInvalidLinkAttempt("10.0.0.7", "owner@x.test", countPerClient: true));   // another client
        Assert.False(limiter.TryCountInvalidLinkAttempt("10.0.0.66", "other@x.test", countPerClient: true)); // per client (3 used)
    }

    [Fact]
    public void Unidentified_invalid_link_attempts_are_counted_per_address()
    {
        using var limiter = new PasswordDoorRateLimiter(perAddressLimit: 2, perClientLimit: 100, TimeSpan.FromMinutes(5));

        Assert.True(limiter.TryCountInvalidLinkAttempt("the-gateway", "owner@x.test", countPerClient: false));
        Assert.True(limiter.TryCountInvalidLinkAttempt("the-gateway", "owner@x.test", countPerClient: false));
        Assert.False(limiter.TryCountInvalidLinkAttempt("the-gateway", "owner@x.test", countPerClient: false));
    }

    [Fact]
    public void The_two_doors_are_counted_apart()
    {
        using var limiter = new PasswordDoorRateLimiter(perAddressLimit: 1, perClientLimit: 100, TimeSpan.FromMinutes(5));

        Assert.True(limiter.TryAcquireForgotPassword("10.0.0.1", "a@x.test", countPerClient: true));
        Assert.False(limiter.TryAcquireForgotPassword("10.0.0.1", "a@x.test", countPerClient: true));
        Assert.True(limiter.TryCountInvalidLinkAttempt("10.0.0.1", "a@x.test", countPerClient: true));
    }

    // ── the client identity ─────────────────────────────────────────────────────────────────────────────────

    private static readonly IPAddress Proxy = IPAddress.Parse("10.1.1.1");

    [Fact]
    public void An_untrusted_peer_is_the_client_whatever_forwarded_header_it_writes()
    {
        var resolver = new ClientAddressResolver([Proxy]);

        Assert.Equal("198.51.100.7", resolver.Resolve(Request("198.51.100.7", "203.0.113.1")));
    }

    [Fact]
    public void Behind_a_trusted_proxy_the_spoofed_leftmost_entry_is_not_believed()
    {
        var resolver = new ClientAddressResolver([Proxy]);

        Assert.Equal("198.51.100.7", resolver.Resolve(Request("10.1.1.1", "1.2.3.4, 198.51.100.7")));
    }

    [Fact]
    public void Two_clients_behind_the_same_trusted_proxy_are_two_clients()
    {
        var resolver = new ClientAddressResolver([Proxy, IPAddress.Parse("10.1.1.2")]);

        Assert.Equal("198.51.100.7", resolver.Resolve(Request("10.1.1.1", "198.51.100.7, 10.1.1.2")));
        Assert.Equal("198.51.100.8", resolver.Resolve(Request("10.1.1.1", "198.51.100.8, 10.1.1.2")));
    }

    [Fact]
    public void Loopback_is_trusted_only_when_it_is_listed()
    {
        Assert.Equal("198.51.100.9", new ClientAddressResolver([IPAddress.Loopback]).Resolve(Request("127.0.0.1", "198.51.100.9")));
        Assert.Equal("127.0.0.1", new ClientAddressResolver([Proxy]).Resolve(Request("127.0.0.1", "198.51.100.9")));
    }

    [Fact]
    public void A_request_without_a_peer_address_is_not_told_apart()
    {
        var context = new DefaultHttpContext();

        Assert.False(new ClientAddressResolver([Proxy]).Identify(context).Identified);
        Assert.True(new ClientAddressResolver([Proxy]).Identify(Request("198.51.100.7", null)).Identified);
        Assert.False(new ClientAddressResolver([]).Identify(Request("198.51.100.7", null)).Identified);
    }

    [Fact]
    public void An_unlisted_peer_that_forwards_for_others_is_warned_about_once()
    {
        var logs = new CapturingLoggerProvider();
        var resolver = new ClientAddressResolver([Proxy], logs.CreateLogger<ClientAddressResolver>());

        resolver.Resolve(Request("10.9.9.9", "198.51.100.1"));
        resolver.Resolve(Request("10.9.9.9", "198.51.100.2"));
        resolver.Resolve(Request("10.9.9.8", "198.51.100.3"));
        resolver.Resolve(Request("10.9.9.7", null)); // not forwarding: nothing to say

        Assert.Equal(2, logs.Entries.Count(e => e.Level == LogLevel.Warning));
    }

    private static HttpContext Request(string peer, string? forwardedFor)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(peer);
        if (forwardedFor is not null) context.Request.Headers["X-Forwarded-For"] = forwardedFor;
        return context;
    }
}
