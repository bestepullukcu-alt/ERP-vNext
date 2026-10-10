using System.Security.Cryptography;
using System.Text;
using System.Threading.RateLimiting;

namespace Diten.AuthService.Infrastructure.Security;

/// <summary>
/// WP-EMAIL-LANG-01 FIX1 — a signed-in user's own writes (today: <c>PUT api/auth/me/preferences</c>) are limited PER USER,
/// with the same fixed-window primitives as <see cref="PasswordDoorRateLimiter"/>. The partition is (tenant, user) from the
/// signed token — never a value the client wrote — and is kept only as a SHA-256.
/// <para><b>Per instance (FIX2).</b> The counts live in this process's memory: with N AuthService instances behind a load
/// balancer one user can make up to N × <see cref="DefaultPreferencesLimit"/> writes per <see cref="DefaultWindow"/>. That is
/// the same as <see cref="PasswordDoorRateLimiter"/>, and enough for its purpose (a flood, not a precise quota).</para>
/// </summary>
public sealed class SelfServiceRateLimiter : IDisposable
{
    public const int DefaultPreferencesLimit = 10;
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromMinutes(5);

    private readonly PartitionedRateLimiter<string> _preferences;

    public SelfServiceRateLimiter()
        : this(DefaultPreferencesLimit, DefaultWindow)
    {
    }

    public SelfServiceRateLimiter(int preferencesLimit, TimeSpan window)
    {
        Window = window;
        _preferences = PartitionedRateLimiter.Create<string, string>(key => RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, preferencesLimit),
                Window = window,
                QueueLimit = 0,
                AutoReplenishment = true
            }));
    }

    public TimeSpan Window { get; }

    /// <summary>True = go on; false = 429, with <paramref name="retryAfter"/> from the lease's own RetryAfter metadata (the
    /// time left in this window; the whole window when the limiter gives none).</summary>
    public bool TryAcquirePreferences(Guid tenantId, Guid userId, out TimeSpan retryAfter)
    {
        var key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"preferences|{tenantId:D}|{userId:D}")));
        using var lease = _preferences.AttemptAcquire(key);
        retryAfter = lease.IsAcquired ? TimeSpan.Zero
            : lease.TryGetMetadata(MetadataName.RetryAfter, out var left) ? left : Window;
        return lease.IsAcquired;
    }

    public void Dispose() => _preferences.Dispose();
}
