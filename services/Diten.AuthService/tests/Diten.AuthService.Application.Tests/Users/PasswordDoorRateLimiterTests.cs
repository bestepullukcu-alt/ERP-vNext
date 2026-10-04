using Diten.AuthService.Infrastructure.Security;

namespace Diten.AuthService.Application.Tests.Users;

/// <summary>
/// BL-529 FIX2 — the two anonymous platform password doors' limiter, measured on its own with small numbers: the per-address
/// window, the per-client window, and the doors counted apart. The HTTP test proves the 429 on the wire.
/// </summary>
public sealed class PasswordDoorRateLimiterTests
{
    [Fact]
    public void Past_the_per_address_limit_the_address_is_refused_and_another_address_is_not()
    {
        using var limiter = new PasswordDoorRateLimiter(perAddressLimit: 2, perClientLimit: 100, TimeSpan.FromMinutes(5));

        Assert.True(limiter.TryAcquire("forgot-password", "10.0.0.1", "a@x.test"));
        Assert.True(limiter.TryAcquire("forgot-password", "10.0.0.2", "A@X.test ")); // the address, not its spelling
        Assert.False(limiter.TryAcquire("forgot-password", "10.0.0.3", "a@x.test")); // another client does not help
        Assert.True(limiter.TryAcquire("forgot-password", "10.0.0.1", "b@x.test"));
    }

    [Fact]
    public void Past_the_per_client_limit_the_client_is_refused_whatever_the_address()
    {
        using var limiter = new PasswordDoorRateLimiter(perAddressLimit: 100, perClientLimit: 2, TimeSpan.FromMinutes(5));

        Assert.True(limiter.TryAcquire("reset-password", "10.0.0.9", "a@x.test"));
        Assert.True(limiter.TryAcquire("reset-password", "10.0.0.9", "b@x.test"));
        Assert.False(limiter.TryAcquire("reset-password", "10.0.0.9", "c@x.test"));
        Assert.True(limiter.TryAcquire("reset-password", "10.0.0.8", "c@x.test"));
    }

    [Fact]
    public void The_two_doors_are_counted_apart()
    {
        using var limiter = new PasswordDoorRateLimiter(perAddressLimit: 1, perClientLimit: 100, TimeSpan.FromMinutes(5));

        Assert.True(limiter.TryAcquire("forgot-password", "10.0.0.1", "a@x.test"));
        Assert.False(limiter.TryAcquire("forgot-password", "10.0.0.1", "a@x.test"));
        Assert.True(limiter.TryAcquire("reset-password", "10.0.0.1", "a@x.test"));
    }
}
