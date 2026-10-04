using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

namespace Diten.AuthService.Infrastructure.Security;

/// <summary>
/// BL-529 FIX2 — THE ANONYMOUS PLATFORM PASSWORD DOORS ARE RATE-LIMITED: <c>POST api/platform-auth/forgot-password</c>
/// and <c>POST api/platform-auth/reset-password</c> (the set-password link). Both open without a tenant since BL-529, and
/// both can be asked by anyone, endlessly — a flood of "forgot password" against one administrator is also how a stale
/// write would be aimed at a reset.
/// <para>Two fixed windows, counted separately per door: per e-mail address (the target — unspoofable, the address is in
/// the body) and per client address (coarse; behind the gateway this is the forwarded address). The address is keyed by
/// its SHA-256, never stored or logged in clear. The ASP.NET rate-limiting middleware is not used because its partition
/// key cannot read the request body; the same <see cref="PartitionedRateLimiter"/> primitives are used here instead.</para>
/// </summary>
public sealed class PasswordDoorRateLimiter : IDisposable
{
    /// <summary>The stable code a refused request carries (429).</summary>
    public const string TooManyRequestsCode = "AUTH_TOO_MANY_REQUESTS";

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

    /// <summary>True when the request may go on; false = answer 429 <see cref="TooManyRequestsCode"/>.</summary>
    public bool TryAcquire(string door, string? clientAddress, string? email)
    {
        using var client = _perClient.AttemptAcquire($"{door}|{clientAddress ?? "unknown"}");
        if (!client.IsAcquired)
        {
            return false;
        }

        using var address = _perAddress.AttemptAcquire($"{door}|{Fingerprint(email)}");
        return address.IsAcquired;
    }

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

    private static string Fingerprint(string? email)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes((email ?? string.Empty).Trim().ToLowerInvariant())));
}
