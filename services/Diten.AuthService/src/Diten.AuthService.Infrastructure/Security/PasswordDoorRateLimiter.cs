using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

namespace Diten.AuthService.Infrastructure.Security;

/// <summary>
/// BL-529 FIX2 — THE ANONYMOUS PLATFORM PASSWORD DOORS ARE RATE-LIMITED: <c>POST api/platform-auth/forgot-password</c>
/// and <c>POST api/platform-auth/reset-password</c> (the set-password link). Both open without a tenant since BL-529, and
/// both can be asked by anyone, endlessly — a flood of "forgot password" against one administrator is also how a stale
/// write would be aimed at a reset.
/// <para>Two fixed windows per door. The client is <see cref="ClientAddressResolver"/>'s answer (never a header the client
/// wrote on its own). "Forgot password" counts per client and per e-mail address; the set-password link per client and per
/// (client, address) — see the two methods. Every key is a SHA-256, never stored or logged in clear. The ASP.NET rate-limiting middleware is not used because its partition
/// key cannot read the request body; the same <see cref="PartitionedRateLimiter"/> primitives are used here instead.</para>
/// </summary>
public sealed class PasswordDoorRateLimiter : IDisposable
{
    /// <summary>The stable code a refused request carries (429).</summary>
    public const string TooManyRequestsCode = Application.Common.AuthRefusalCodes.TooManyRequests;

    public const int DefaultPerAddressLimit = 5;
    public const int DefaultPerClientLimit = 30;
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(15);

    private readonly PartitionedRateLimiter<string> _perAddress;
    private readonly PartitionedRateLimiter<string> _perClient;

    public PasswordDoorRateLimiter()
        : this(DefaultPerAddressLimit, DefaultPerClientLimit, DefaultWindow)
    {
    }

    public PasswordDoorRateLimiter(int perAddressLimit, int perClientLimit, TimeSpan window)
    {
        Window = window;
        _perAddress = Fixed(perAddressLimit, window);
        _perClient = Fixed(perClientLimit, window);
    }

    public TimeSpan Window { get; }

    /// <summary>
    /// "Forgot password": per client (when clients can be told apart), then per e-mail address — the target: a flood of
    /// mails at one administrator is capped whoever sends it. True = go on.
    /// <para>FIX5 — when clients can NOT be told apart there is no per-client count (it would be one bucket for everybody),
    /// and the per-address partitions then grow with the addresses asked, not with real clients; each key is a fixed-length
    /// hash and an idle partition is dropped by the limiter, so that growth is bounded by the window.</para>
    /// </summary>
    /// <param name="countPerClient">REQUIRED (FIX5) — the caller states whether the client is told apart
    /// (<see cref="ClientIdentity.Identified"/>); no default can quietly make a global bucket.</param>
    public bool TryAcquireForgotPassword(string client, string? email, bool countPerClient)
        => (!countPerClient || TryAcquire(_perClient, $"forgot-password|client|{client}"))
           && TryAcquire(_perAddress, $"forgot-password|address|{Normalize(email)}");

    /// <summary>
    /// BL-529 FIX5 — the set-password link counts ONLY attempts whose link did not match. The link is 64 random bytes: no
    /// count protects it, and any count junk could raise would only be a lever to lock the owner out — so a request that
    /// carries the valid link is never counted and never refused (the caller compares the link FIRST). Invalid attempts are
    /// counted per client and per (client, address) when clients are told apart, per address otherwise (harmless now: the
    /// owner's valid link skips the count). True = answer as usual (400); false = 429.
    /// </summary>
    public bool TryCountInvalidLinkAttempt(string client, string? email, bool countPerClient)
        => countPerClient
            ? TryAcquire(_perClient, $"reset-password|client|{client}")
              && TryAcquire(_perAddress, $"reset-password|client-address|{client}|{Normalize(email)}")
            : TryAcquire(_perAddress, $"reset-password|address|{Normalize(email)}");

    public void Dispose()
    {
        _perAddress.Dispose();
        _perClient.Dispose();
    }

    private static PartitionedRateLimiter<string> Fixed(int permits, TimeSpan window) =>
        PartitionedRateLimiter.Create<string, string>(key => RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, permits),
                Window = window,
                QueueLimit = 0,
                AutoReplenishment = true
            }));

    private static string Normalize(string? email) => (email ?? string.Empty).Trim().ToLowerInvariant();

    // Every partition key is a fixed-length SHA-256: no caller-chosen string is kept, whatever its length.
    private static bool TryAcquire(PartitionedRateLimiter<string> limiter, string key)
    {
        using var lease = limiter.AttemptAcquire(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))));
        return lease.IsAcquired;
    }
}
